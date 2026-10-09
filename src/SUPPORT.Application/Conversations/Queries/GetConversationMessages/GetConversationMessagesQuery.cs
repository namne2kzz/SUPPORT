using MediatR;
using SUPPORT.Application.Common.Exceptions;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Application.Conversations.DTOs;
using SUPPORT.Domain.Enums;

namespace SUPPORT.Application.Conversations.Queries.GetConversationMessages;

/// <summary>Loads every message of one of the caller's conversations.</summary>
/// <param name="ConversationId">Conversation id.</param>
public sealed record GetConversationMessagesQuery(Guid ConversationId) : IRequest<IReadOnlyList<MessageDto>>;

/// <summary>Handles <see cref="GetConversationMessagesQuery"/>.</summary>
/// <param name="caller">Calling product and user.</param>
/// <param name="conversations">Conversation persistence.</param>
internal sealed class GetConversationMessagesHandler(ICallerContext caller, IConversationRepository conversations)
    : IRequestHandler<GetConversationMessagesQuery, IReadOnlyList<MessageDto>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<MessageDto>> Handle(GetConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.IsOwnedBy(caller.Product, caller.OrgId, caller.UserId))
            throw new NotFoundException("Conversation not found.");

        var messages = await conversations.GetMessagesWithRatingsAsync(conversation.Id, cancellationToken);
        return [.. messages.Select(m => new MessageDto(
            m.Message.Id,
            m.Message.Role == MessageRole.User ? "user" : "assistant",
            m.Message.Content,
            m.Message.Citations,
            m.Message.IsInterrupted,
            m.Rating,
            m.Message.CreatedAt))];
    }
}
