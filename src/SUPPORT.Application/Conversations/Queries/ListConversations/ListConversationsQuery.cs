using FluentValidation;
using MediatR;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Application.Conversations.DTOs;

namespace SUPPORT.Application.Conversations.Queries.ListConversations;

/// <summary>Lists the caller's conversations, newest activity first.</summary>
/// <param name="Page">1-based page.</param>
/// <param name="PageSize">Page size (max 50).</param>
public sealed record ListConversationsQuery(int Page = 1, int PageSize = 20) : IRequest<PagedResult<ConversationSummaryDto>>;

/// <summary>Validates <see cref="ListConversationsQuery"/>.</summary>
internal sealed class ListConversationsValidator : AbstractValidator<ListConversationsQuery>
{
    /// <summary>Paging bounds.</summary>
    public ListConversationsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 50);
    }
}

/// <summary>Handles <see cref="ListConversationsQuery"/>.</summary>
/// <param name="caller">Calling product and user.</param>
/// <param name="conversations">Conversation persistence.</param>
internal sealed class ListConversationsHandler(ICallerContext caller, IConversationRepository conversations)
    : IRequestHandler<ListConversationsQuery, PagedResult<ConversationSummaryDto>>
{
    /// <inheritdoc />
    public async Task<PagedResult<ConversationSummaryDto>> Handle(ListConversationsQuery request, CancellationToken cancellationToken)
    {
        var (items, total) = await conversations.ListAsync(
            caller.Product, caller.OrgId, caller.UserId, request.Page, request.PageSize, cancellationToken);

        return new PagedResult<ConversationSummaryDto>(
            [.. items.Select(c => new ConversationSummaryDto(c.Id, c.Title, c.LastMessageAt))],
            request.Page, request.PageSize, total);
    }
}
