namespace SUPPORT.Application.Common.Exceptions;

/// <summary>The LLM provider cannot serve the request right now. Mapped to HTTP 503 (or an SSE <c>error</c> event).</summary>
/// <param name="code">One of the <see cref="AssistantErrorCodes"/>.</param>
/// <param name="message">Reason.</param>
/// <param name="inner">Provider exception, if any.</param>
public sealed class AssistantUnavailableException(string code, string message, Exception? inner = null)
    : Exception(message, inner)
{
    /// <summary>Machine-readable error code.</summary>
    public string Code { get; } = code;
}

/// <summary>Error codes surfaced to the widget.</summary>
public static class AssistantErrorCodes
{
    /// <summary>Free-tier quota exhausted (provider returned 429).</summary>
    public const string QuotaExceeded = "NMATE_QUOTA_EXCEEDED";

    /// <summary>No API key configured, or the provider failed.</summary>
    public const string Unavailable = "NMATE_UNAVAILABLE";

    /// <summary>The user already has an answer being generated.</summary>
    public const string StreamInProgress = "NMATE_STREAM_IN_PROGRESS";
}
