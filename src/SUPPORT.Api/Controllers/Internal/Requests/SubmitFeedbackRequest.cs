namespace SUPPORT.Api.Controllers.Internal.Requests;

/// <summary>Body of <c>PUT /internal/v1/messages/{id}/feedback</c>.</summary>
/// <param name="Rating">1 helpful, -1 not helpful.</param>
/// <param name="Comment">Optional comment.</param>
public sealed record SubmitFeedbackRequest(short Rating, string? Comment);
