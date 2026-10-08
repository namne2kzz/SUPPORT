using SUPPORT.Domain.Common;
using SUPPORT.Domain.Enums;
using SUPPORT.Domain.ValueObjects;

namespace SUPPORT.Domain.Entities;

/// <summary>A single question or answer within a <see cref="Conversation"/>.</summary>
public sealed class ConversationMessage : Entity
{
    /// <summary>Owning conversation.</summary>
    public Guid ConversationId { get; private set; }

    /// <summary>Author role.</summary>
    public MessageRole Role { get; private set; }

    /// <summary>Message text (markdown for answers).</summary>
    public string Content { get; private set; } = default!;

    /// <summary>Knowledge sources the answer was grounded on.</summary>
    public List<Citation> Citations { get; private set; } = [];

    /// <summary>Front-end route the user was on when asking.</summary>
    public string? PageRoute { get; private set; }

    /// <summary>Chat model that produced the answer.</summary>
    public string? Model { get; private set; }

    /// <summary>Prompt tokens reported by the provider, if any.</summary>
    public int? PromptTokens { get; private set; }

    /// <summary>Completion tokens reported by the provider, if any.</summary>
    public int? CompletionTokens { get; private set; }

    /// <summary>Wall-clock time to produce the answer.</summary>
    public int? LatencyMs { get; private set; }

    /// <summary>Best cosine similarity seen during retrieval — low values point at missing documentation.</summary>
    public float? TopScore { get; private set; }

    /// <summary>Whether the answer was cut short (client disconnected or provider failed mid-stream).</summary>
    public bool IsInterrupted { get; private set; }

    private ConversationMessage() { }

    /// <summary>Creates the user's question.</summary>
    /// <param name="conversationId">Owning conversation.</param>
    /// <param name="content">Question text.</param>
    /// <param name="pageRoute">Route the user was on.</param>
    /// <returns>The new message.</returns>
    public static ConversationMessage UserQuestion(Guid conversationId, string content, string? pageRoute)
    {
        if (string.IsNullOrWhiteSpace(content)) throw new DomainException("Question text is required.");
        return new ConversationMessage
        {
            ConversationId = conversationId,
            Role = MessageRole.User,
            Content = content.Trim(),
            PageRoute = pageRoute,
        };
    }

    /// <summary>Creates NMate's answer.</summary>
    /// <param name="conversationId">Owning conversation.</param>
    /// <param name="answer">Answer details.</param>
    /// <returns>The new message.</returns>
    public static ConversationMessage AssistantAnswer(Guid conversationId, AssistantAnswer answer) => new()
    {
        ConversationId = conversationId,
        Role = MessageRole.Assistant,
        Content = answer.Content,
        Citations = [.. answer.Citations],
        Model = answer.Model,
        PromptTokens = answer.PromptTokens,
        CompletionTokens = answer.CompletionTokens,
        LatencyMs = answer.LatencyMs,
        TopScore = answer.TopScore,
        IsInterrupted = answer.IsInterrupted,
    };
}

/// <summary>Everything recorded about one generated answer.</summary>
/// <param name="Content">Answer text.</param>
/// <param name="Citations">Grounding sources.</param>
/// <param name="Model">Chat model, or null when no model was called.</param>
/// <param name="PromptTokens">Prompt tokens, if reported.</param>
/// <param name="CompletionTokens">Completion tokens, if reported.</param>
/// <param name="LatencyMs">Elapsed time.</param>
/// <param name="TopScore">Best retrieval similarity.</param>
/// <param name="IsInterrupted">Whether generation was cut short.</param>
public sealed record AssistantAnswer(
    string Content,
    IReadOnlyList<Citation> Citations,
    string? Model,
    int? PromptTokens,
    int? CompletionTokens,
    int LatencyMs,
    float? TopScore,
    bool IsInterrupted);
