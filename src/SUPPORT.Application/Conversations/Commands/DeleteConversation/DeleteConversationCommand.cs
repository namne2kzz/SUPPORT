using MediatR;
using SUPPORT.Application.Common.Exceptions;
using SUPPORT.Application.Common.Interfaces;

namespace SUPPORT.Application.Conversations.Commands.DeleteConversation;

/// <summary>Hides one of the caller's conversations (soft delete; retention removes it for good later).</summary>
/// <param name="ConversationId">Conversation id.</param>
public sealed record DeleteConversationCommand(Guid ConversationId) : IRequest;

/// <summary>Handles <see cref="DeleteConversationCommand"/>.</summary>
/// <param name="caller">Calling product and user.</param>
/// <param name="conversations">Conversation persistence.</param>
internal sealed class DeleteConversationHandler(ICallerContext caller, IConversationRepository conversations)
    : IRequestHandler<DeleteConversationCommand>
{
    /// <inheritdoc />
    public async Task Handle(DeleteConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.IsOwnedBy(caller.Product, caller.OrgId, caller.UserId))
            throw new NotFoundException("Conversation not found.");

        conversation.SoftDelete();
        await conversations.SaveChangesAsync(cancellationToken);
    }
}
