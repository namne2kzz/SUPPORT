using SUPPORT.Domain.Enums;
using SUPPORT.Domain.ValueObjects;

namespace SUPPORT.Application.Chat.DTOs;

/// <summary>One server-sent event of a streamed answer: <see cref="Name"/> becomes <c>event:</c>, <see cref="Payload"/> the JSON <c>data:</c>.</summary>
/// <param name="Name">SSE event name.</param>
/// <param name="Payload">Event body.</param>
public sealed record ChatStreamEvent(string Name, object Payload)
{
    /// <summary>First event: which conversation the answer belongs to (new conversations get their id here).</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <returns>The event.</returns>
    public static ChatStreamEvent Meta(Guid conversationId) => new("meta", new { conversationId });

    /// <summary>A piece of answer text.</summary>
    /// <param name="text">Text delta.</param>
    /// <returns>The event.</returns>
    public static ChatStreamEvent Delta(string text) => new("delta", new { text });

    /// <summary>The sources the answer was grounded on.</summary>
    /// <param name="citations">Citations, best first.</param>
    /// <returns>The event.</returns>
    public static ChatStreamEvent Citations(IReadOnlyList<Citation> citations) => new("citations", citations);

    /// <summary>Last event of a completed answer.</summary>
    /// <param name="messageId">Saved answer id (used for feedback).</param>
    /// <param name="latencyMs">Elapsed time.</param>
    /// <param name="mode">How the answer was produced; the widget labels general answers.</param>
    /// <returns>The event.</returns>
    public static ChatStreamEvent Done(Guid messageId, int latencyMs, AnswerMode mode) => new("done", new
    {
        messageId,
        latencyMs,
        answered = mode != AnswerMode.Declined,
        mode = ModeName(mode),
    });

    /// <summary>Wire name of an answer mode: <c>docs</c>, <c>general</c> or <c>declined</c>.</summary>
    /// <param name="mode">Answer mode.</param>
    /// <returns>Lower-case name used by the widget.</returns>
    public static string ModeName(AnswerMode mode) => mode switch
    {
        AnswerMode.Documentation => "docs",
        AnswerMode.General => "general",
        _ => "declined",
    };

    /// <summary>The answer failed mid-stream; whatever was generated so far is kept.</summary>
    /// <param name="code">Machine-readable error code.</param>
    /// <param name="message">Human-readable reason.</param>
    /// <param name="messageId">Saved partial answer id, if any.</param>
    /// <returns>The event.</returns>
    public static ChatStreamEvent Error(string code, string message, Guid? messageId) => new("error", new { code, message, messageId });
}
