using SUPPORT.Domain.ValueObjects;

namespace SUPPORT.Application.Conversations.DTOs;

/// <summary>A conversation in the history list.</summary>
/// <param name="Id">Conversation id.</param>
/// <param name="Title">Title from the first question.</param>
/// <param name="LastMessageAt">UTC time of the latest message.</param>
public sealed record ConversationSummaryDto(Guid Id, string Title, DateTime LastMessageAt);

/// <summary>One message of a conversation.</summary>
/// <param name="Id">Message id.</param>
/// <param name="Role"><c>user</c> or <c>assistant</c>.</param>
/// <param name="Content">Text (markdown for answers).</param>
/// <param name="Citations">Sources of an answer; empty for questions.</param>
/// <param name="IsInterrupted">Whether the answer was cut short.</param>
/// <param name="Rating">Caller's rating of an answer (1 / -1), or null.</param>
/// <param name="CreatedAt">UTC creation time.</param>
public sealed record MessageDto(
    Guid Id,
    string Role,
    string Content,
    IReadOnlyList<Citation> Citations,
    bool IsInterrupted,
    short? Rating,
    DateTime CreatedAt);

/// <summary>A page of results.</summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Items on this page.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Page size.</param>
/// <param name="Total">Total items across all pages.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
