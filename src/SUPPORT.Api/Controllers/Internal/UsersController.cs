using MediatR;
using Microsoft.AspNetCore.Mvc;
using SUPPORT.Application.Conversations.Commands.PurgeUserData;

namespace SUPPORT.Api.Controllers.Internal;

/// <summary>Data-removal requests from the product backend.</summary>
/// <param name="mediator">Mediator.</param>
[ApiController]
[Route("internal/v1/users")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    /// <summary>Permanently deletes every conversation of a user in the calling product.</summary>
    /// <param name="userId">User whose data to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many conversations were deleted.</returns>
    [HttpDelete("{userId:guid}/data")]
    public async Task<IActionResult> Purge(Guid userId, CancellationToken ct) =>
        Ok(new { deletedConversations = await mediator.Send(new PurgeUserDataCommand(userId), ct) });
}
