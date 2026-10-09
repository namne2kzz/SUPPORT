namespace SUPPORT.Api.Controllers.Internal.Requests;

/// <summary>Body of <c>POST /internal/v1/chat</c>.</summary>
/// <param name="ConversationId">Conversation to continue, or null to start one.</param>
/// <param name="Message">The question.</param>
/// <param name="Context">Where the user is in the product.</param>
public sealed record AskQuestionRequest(Guid? ConversationId, string Message, AskQuestionContext? Context);

/// <summary>Page context of a question.</summary>
/// <param name="Route">Front-end route, e.g. <c>/acme/DASH/sprint-planning</c>.</param>
/// <param name="RepositoryId">Current repository (reserved for permission-aware tools).</param>
public sealed record AskQuestionContext(string? Route, Guid? RepositoryId);
