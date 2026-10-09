using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SUPPORT.Application.Common.Exceptions;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Infrastructure.Settings;

namespace SUPPORT.Infrastructure.AI;

/// <summary>
/// <see cref="ISupportAssistant"/> built on Microsoft Agent Framework: a <see cref="ChatClientAgent"/> over the
/// configured <see cref="IChatClient"/>, with a <see cref="KnowledgeContextProvider"/> supplying the documentation.
/// </summary>
/// <param name="chatClient">Chat client (Gemini via the OpenAI-compatible API, no automatic retries).</param>
/// <param name="settings">LLM settings.</param>
/// <param name="loggerFactory">Logger factory passed to the agent.</param>
internal sealed class NMateAssistant(IChatClient chatClient, IOptions<LlmSettings> settings, ILoggerFactory loggerFactory)
    : ISupportAssistant
{
    private readonly LlmSettings _settings = settings.Value;

    /// <inheritdoc />
    public bool IsConfigured => _settings.IsConfigured;

    /// <inheritdoc />
    public string ChatModel => _settings.ChatModel;

    /// <inheritdoc />
    public async IAsyncEnumerable<AssistantUpdate> StreamAnswerAsync(
        AssistantRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var productName = _settings.ProductDisplayNames.GetValueOrDefault(request.Product, request.Product);

        // A new agent per answer is cheap (no I/O) and keeps the knowledge provider bound to this request only.
        var agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "NMate",
            ChatOptions = new ChatOptions
            {
                Instructions = NMatePrompts.System(productName, request.Route),
                Temperature = _settings.Temperature,
                MaxOutputTokens = _settings.MaxOutputTokens,
            },
            AIContextProviders = [new KnowledgeContextProvider(request.Knowledge)],
        }, loggerFactory);

        // History is passed explicitly from our own table rather than kept in an AgentSession: the conversation
        // must be queryable (history page, feedback analysis), which a serialized session blob is not.
        var messages = request.History
            .Select(t => new ChatMessage(t.IsUser ? ChatRole.User : ChatRole.Assistant, t.Content))
            .Append(new ChatMessage(ChatRole.User, request.Question))
            .ToList();

        var updates = agent.RunStreamingAsync(messages, cancellationToken: ct).GetAsyncEnumerator(ct);
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await updates.MoveNextAsync();
                }
                catch (ClientResultException ex)
                {
                    throw ProviderErrors.Translate(ex);
                }
                catch (HttpRequestException ex)
                {
                    throw new AssistantUnavailableException(AssistantErrorCodes.Unavailable, "Không kết nối được tới model.", ex);
                }

                if (!hasNext) yield break;

                var update = updates.Current;
                var usage = update.Contents.OfType<UsageContent>().FirstOrDefault()?.Details;
                if (!string.IsNullOrEmpty(update.Text) || usage is not null)
                    yield return new AssistantUpdate(update.Text, ToInt(usage?.InputTokenCount), ToInt(usage?.OutputTokenCount));
            }
        }
        finally
        {
            await updates.DisposeAsync();
        }
    }

    private static int? ToInt(long? value) => value is null ? null : (int)Math.Min(value.Value, int.MaxValue);
}
