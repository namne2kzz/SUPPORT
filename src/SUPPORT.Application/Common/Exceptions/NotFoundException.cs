namespace SUPPORT.Application.Common.Exceptions;

/// <summary>
/// Thrown when a resource does not exist <em>or belongs to someone else</em> — both map to HTTP 404 so a caller
/// cannot probe for other users' conversation ids.
/// </summary>
/// <param name="message">Reason.</param>
public sealed class NotFoundException(string message = "Resource not found.") : Exception(message);
