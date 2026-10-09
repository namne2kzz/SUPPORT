using SUPPORT.Domain.Entities;

namespace SUPPORT.Application.Common.Interfaces;

/// <summary>Persistence for conversations, messages and feedback.</summary>
public interface IConversationRepository
{
    /// <summary>Loads a conversation for update.</summary>
    /// <param name="id">Conversation id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The conversation, or null if it does not exist.</returns>
    Task<Conversation?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Loads the latest messages of a conversation, oldest first.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="take">Maximum messages.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Up to <paramref name="take"/> most recent messages in chronological order.</returns>
    Task<IReadOnlyList<ConversationMessage>> GetRecentMessagesAsync(Guid conversationId, int take, CancellationToken ct);

    /// <summary>Loads all messages of a conversation with the caller's rating of each, oldest first.</summary>
    /// <param name="conversationId">Conversation id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Messages with their rating (null when unrated).</returns>
    Task<IReadOnlyList<(ConversationMessage Message, short? Rating)>> GetMessagesWithRatingsAsync(Guid conversationId, CancellationToken ct);

    /// <summary>Lists a user's live conversations, newest activity first.</summary>
    /// <param name="product">Product key.</param>
    /// <param name="orgId">User's org.</param>
    /// <param name="userId">User id.</param>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The page and the total count.</returns>
    Task<(IReadOnlyList<Conversation> Items, int Total)> ListAsync(string product, Guid orgId, Guid userId, int page, int pageSize, CancellationToken ct);

    /// <summary>Loads a message together with its conversation.</summary>
    /// <param name="messageId">Message id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pair, or null if the message does not exist.</returns>
    Task<(ConversationMessage Message, Conversation Conversation)?> GetMessageAsync(Guid messageId, CancellationToken ct);

    /// <summary>Loads existing feedback for update.</summary>
    /// <param name="messageId">Rated message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The feedback, or null.</returns>
    Task<MessageFeedback?> GetFeedbackAsync(Guid messageId, CancellationToken ct);

    /// <summary>Stages a new conversation.</summary>
    /// <param name="conversation">The conversation.</param>
    void Add(Conversation conversation);

    /// <summary>Stages a new message.</summary>
    /// <param name="message">The message.</param>
    void Add(ConversationMessage message);

    /// <summary>Stages new feedback.</summary>
    /// <param name="feedback">The feedback.</param>
    void Add(MessageFeedback feedback);

    /// <summary>Hard-deletes every conversation of a user in a product (messages and feedback cascade).</summary>
    /// <param name="product">Product key.</param>
    /// <param name="userId">User id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of conversations deleted.</returns>
    Task<int> PurgeUserAsync(string product, Guid userId, CancellationToken ct);

    /// <summary>Commits staged changes.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the changes are saved.</returns>
    Task SaveChangesAsync(CancellationToken ct);
}
