using MediatR;
using Microsoft.AspNetCore.Mvc;
using SUPPORT.Api.Controllers.Internal.Requests;
using SUPPORT.Application.Conversations.Commands.DeleteConversation;
using SUPPORT.Application.Conversations.DTOs;
using SUPPORT.Application.Conversations.Queries.GetConversationMessages;
using SUPPORT.Application.Conversations.Queries.ListConversations;
using SUPPORT.Application.Feedback.Commands.SubmitFeedback;

namespace SUPPORT.Api.Controllers.Internal;

/// <summary>The caller's conversation history and feedback.</summary>
/// <param name="mediator">Mediator.</param>
[ApiController]
[Route("internal/v1")]
public sealed class ConversationsController(IMediator mediator) : ControllerBase
{
    /// <summary>Lists the caller's conversations, newest activity first.</summary>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">Page size (1–50).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of conversation summaries.</returns>
    [HttpGet("conversations")]
    public Task<PagedResult<ConversationSummaryDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        mediator.Send(new ListConversationsQuery(page, pageSize), ct);

    /// <summary>Loads every message of one of the caller's conversations.</summary>
    /// <param name="id">Conversation id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Messages oldest first; 404 when the conversation is not the caller's.</returns>
    [HttpGet("conversations/{id:guid}/messages")]
    public Task<IReadOnlyList<MessageDto>> Messages(Guid id, CancellationToken ct) =>
        mediator.Send(new GetConversationMessagesQuery(id), ct);

    /// <summary>Hides one of the caller's conversations.</summary>
    /// <param name="id">Conversation id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DeleteConversationCommand(id), ct);
        return NoContent();
    }

    /// <summary>Rates one of NMate's answers.</summary>
    /// <param name="id">Answer message id.</param>
    /// <param name="request">Rating and comment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 on success.</returns>
    [HttpPut("messages/{id:guid}/feedback")]
    public async Task<IActionResult> Feedback(Guid id, [FromBody] SubmitFeedbackRequest request, CancellationToken ct)
    {
        await mediator.Send(new SubmitFeedbackCommand(id, request.Rating, request.Comment), ct);
        return NoContent();
    }
}
