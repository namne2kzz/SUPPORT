namespace SUPPORT.Application.Common.Exceptions;

/// <summary>Thrown when the request clashes with work already in progress. Mapped to HTTP 409.</summary>
/// <param name="code">Machine-readable error code.</param>
/// <param name="message">Reason.</param>
public sealed class ConflictException(string code, string message) : Exception(message)
{
    /// <summary>Machine-readable error code.</summary>
    public string Code { get; } = code;
}
