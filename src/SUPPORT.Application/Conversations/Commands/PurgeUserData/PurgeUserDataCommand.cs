using FluentValidation;
using MediatR;
using SUPPORT.Application.Common.Interfaces;

namespace SUPPORT.Application.Conversations.Commands.PurgeUserData;

/// <summary>
/// Permanently deletes every conversation a user had in the calling product — for data-removal requests.
/// Called by the product backend itself, so the target user is a parameter rather than the caller.
/// </summary>
/// <param name="UserId">User whose data to remove.</param>
public sealed record PurgeUserDataCommand(Guid UserId) : IRequest<int>;

/// <summary>Validates <see cref="PurgeUserDataCommand"/>.</summary>
internal sealed class PurgeUserDataValidator : AbstractValidator<PurgeUserDataCommand>
{
    /// <summary>Rejects the empty id.</summary>
    public PurgeUserDataValidator() => RuleFor(c => c.UserId).NotEmpty();
}

/// <summary>Handles <see cref="PurgeUserDataCommand"/>.</summary>
/// <param name="caller">Calling product (only its own data can be purged).</param>
/// <param name="conversations">Conversation persistence.</param>
internal sealed class PurgeUserDataHandler(ICallerContext caller, IConversationRepository conversations)
    : IRequestHandler<PurgeUserDataCommand, int>
{
    /// <inheritdoc />
    public Task<int> Handle(PurgeUserDataCommand request, CancellationToken cancellationToken) =>
        conversations.PurgeUserAsync(caller.Product, request.UserId, cancellationToken);
}
