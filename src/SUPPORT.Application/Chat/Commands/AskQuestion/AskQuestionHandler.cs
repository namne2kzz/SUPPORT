using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using MediatR;
using Microsoft.Extensions.Options;
using SUPPORT.Application.Chat.DTOs;
using SUPPORT.Application.Common.Exceptions;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Application.Common.Settings;
using SUPPORT.Domain.Entities;
using SUPPORT.Domain.Enums;
using SUPPORT.Domain.ValueObjects;

namespace SUPPORT.Application.Chat.Commands.AskQuestion;

/// <summary>
/// Answers a question: save it, retrieve knowledge, decline when nothing relevant was found, otherwise stream
/// the model's answer and save it (even when cut short).
/// </summary>
/// <param name="caller">Calling product and user.</param>
/// <param name="conversations">Conversation persistence.</param>
/// <param name="search">Knowledge retrieval.</param>
/// <param name="catalog">Route → module resolution.</param>
/// <param name="assistant">LLM answer generation.</param>
/// <param name="gate">One-stream-per-user guard.</param>
/// <param name="settings">Chat settings.</param>
internal sealed class AskQuestionHandler(
    ICallerContext caller,
    IConversationRepository conversations,
    IKnowledgeSearch search,
    IKnowledgeCatalog catalog,
    ISupportAssistant assistant,
    IStreamGate gate,
    IOptions<ChatSettings> settings) : IStreamRequestHandler<AskQuestionCommand, ChatStreamEvent>
{
    private readonly ChatSettings _settings = settings.Value;

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatStreamEvent> Handle(
        AskQuestionCommand request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!assistant.IsConfigured)
            throw new AssistantUnavailableException(AssistantErrorCodes.Unavailable, "NMate chưa được cấu hình model.");

        using var lease = gate.TryAcquire($"{caller.Product}:{caller.UserId}")
            ?? throw new ConflictException(AssistantErrorCodes.StreamInProgress, "NMate đang trả lời câu hỏi trước của bạn.");

        var stopwatch = Stopwatch.StartNew();
        var question = request.Message.Trim();

        var conversation = await LoadOrStartAsync(request.ConversationId, question, cancellationToken);
        var history = request.ConversationId is null
            ? []
            : await conversations.GetRecentMessagesAsync(conversation.Id, _settings.HistoryWindow, cancellationToken);

        conversations.Add(ConversationMessage.UserQuestion(conversation.Id, question, request.Route));
        conversation.MarkActivity();
        await conversations.SaveChangesAsync(cancellationToken);

        yield return ChatStreamEvent.Meta(conversation.Id);

        var module = await catalog.ResolveModuleAsync(caller.Product, request.Route, cancellationToken);
        var hits = await search.SearchAsync(
            new KnowledgeQuery(caller.Product, caller.OrgId, question, module, _settings.TopK), cancellationToken);
        var topScore = hits.Max(h => h.CosineSimilarity);

        if (!IsGrounded(hits, topScore))
        {
            // Declining costs one embedding and no chat call — and never invents an answer.
            var declined = await SaveAnswerAsync(conversation, new AssistantAnswer(
                _settings.NoAnswerText, [], null, null, null, Elapsed(stopwatch), topScore, false));

            yield return ChatStreamEvent.Delta(_settings.NoAnswerText);
            yield return ChatStreamEvent.Done(declined.Id, Elapsed(stopwatch), answered: false);
            yield break;
        }

        var citations = hits
            .Select(h => new Citation(h.ChunkId, h.Title, h.HeadingPath, h.RouteHint, h.CosineSimilarity ?? 0f))
            .ToList();
        var assistantRequest = new AssistantRequest(
            caller.Product, question, [.. history.Select(m => new ChatTurn(m.Role == MessageRole.User, m.Content))], hits, request.Route);

        var answer = new StringBuilder();
        int? promptTokens = null, completionTokens = null;
        AssistantUnavailableException? failure = null;

        // C# forbids `yield` inside a try with a catch, so the enumerator is advanced by hand: the try only
        // guards MoveNextAsync, and every yield happens outside it.
        var updates = assistant.StreamAnswerAsync(assistantRequest, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await updates.MoveNextAsync();
                }
                catch (AssistantUnavailableException ex)
                {
                    failure = ex;
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The user closed the widget: keep what was generated, then let the cancellation surface.
                    await SaveAnswerAsync(conversation, Answer(true));
                    throw;
                }

                if (!hasNext) break;

                var update = updates.Current;
                promptTokens = update.PromptTokens ?? promptTokens;
                completionTokens = update.CompletionTokens ?? completionTokens;

                if (!string.IsNullOrEmpty(update.Text))
                {
                    answer.Append(update.Text);
                    yield return ChatStreamEvent.Delta(update.Text);
                }
            }
        }
        finally
        {
            await updates.DisposeAsync();
        }

        var saved = await SaveAnswerAsync(conversation, Answer(failure is not null));

        if (failure is not null)
        {
            yield return ChatStreamEvent.Error(failure.Code, failure.Message, saved.Id);
            yield break;
        }

        yield return ChatStreamEvent.Citations(citations);
        yield return ChatStreamEvent.Done(saved.Id, Elapsed(stopwatch), answered: true);

        AssistantAnswer Answer(bool interrupted) => new(
            answer.ToString(), citations, assistant.ChatModel, promptTokens, completionTokens,
            Elapsed(stopwatch), topScore, interrupted);
    }

    /// <summary>
    /// Grounded = at least one chunk is semantically close enough, or full-text matched an exact term
    /// (button names, item keys) that embeddings tend to miss.
    /// </summary>
    private bool IsGrounded(IReadOnlyList<KnowledgeHit> hits, float? topScore) =>
        hits.Count > 0 && (topScore >= _settings.MinCosineSimilarity || hits.Any(h => h.IsFullTextHit));

    private async Task<Conversation> LoadOrStartAsync(Guid? conversationId, string question, CancellationToken ct)
    {
        if (conversationId is null)
        {
            var started = Conversation.Start(caller.Product, caller.OrgId, caller.UserId, question);
            conversations.Add(started);
            return started;
        }

        var existing = await conversations.GetAsync(conversationId.Value, ct);
        if (existing is null || !existing.IsOwnedBy(caller.Product, caller.OrgId, caller.UserId))
            throw new NotFoundException("Conversation not found.");

        return existing;
    }

    /// <summary>Saves the answer with <see cref="CancellationToken.None"/> so a disconnect never loses it.</summary>
    private async Task<ConversationMessage> SaveAnswerAsync(Conversation conversation, AssistantAnswer answer)
    {
        var message = ConversationMessage.AssistantAnswer(conversation.Id, answer);
        conversations.Add(message);
        conversation.MarkActivity();
        await conversations.SaveChangesAsync(CancellationToken.None);
        return message;
    }

    private static int Elapsed(Stopwatch stopwatch) => (int)stopwatch.ElapsedMilliseconds;
}
