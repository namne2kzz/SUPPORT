using SUPPORT.Domain.Common;

namespace SUPPORT.Domain.Entities;

/// <summary>A user's thumbs-up/down on one answer. One per message; resubmitting overwrites it.</summary>
public sealed class MessageFeedback
{
    /// <summary>Rated assistant message (also the primary key).</summary>
    public Guid MessageId { get; private set; }

    /// <summary>User who rated it.</summary>
    public Guid UserId { get; private set; }

    /// <summary><c>1</c> helpful, <c>-1</c> not helpful.</summary>
    public short Rating { get; private set; }

    /// <summary>Optional free-text comment.</summary>
    public string? Comment { get; private set; }

    /// <summary>UTC time of the latest submission.</summary>
    public DateTime CreatedAt { get; private set; }

    private MessageFeedback() { }

    /// <summary>Creates feedback for a message.</summary>
    /// <param name="messageId">Rated message.</param>
    /// <param name="userId">Rating user.</param>
    /// <param name="rating">1 or -1.</param>
    /// <param name="comment">Optional comment.</param>
    /// <returns>The new feedback.</returns>
    public static MessageFeedback Create(Guid messageId, Guid userId, short rating, string? comment)
    {
        var feedback = new MessageFeedback { MessageId = messageId, UserId = userId };
        feedback.Update(rating, comment);
        return feedback;
    }

    /// <summary>Overwrites the rating and comment.</summary>
    /// <param name="rating">1 or -1.</param>
    /// <param name="comment">Optional comment.</param>
    public void Update(short rating, string? comment)
    {
        if (rating is not (1 or -1)) throw new DomainException("Rating must be 1 or -1.");
        Rating = rating;
        Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        CreatedAt = DateTime.UtcNow;
    }
}
