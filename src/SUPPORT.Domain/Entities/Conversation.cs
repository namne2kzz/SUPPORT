using SUPPORT.Domain.Common;

namespace SUPPORT.Domain.Entities;

/// <summary>One chat thread between a user of a product and NMate.</summary>
/// <remarks>
/// <see cref="OrgId"/> and <see cref="UserId"/> reference the calling product's own database, so there are
/// no foreign keys; ownership is enforced by <see cref="IsOwnedBy"/> on every read and write.
/// </remarks>
public sealed class Conversation : Entity
{
    /// <summary>Maximum title length; longer first questions are truncated.</summary>
    public const int TitleMaxLength = 200;

    private const int TitlePreviewLength = 60;

    /// <summary>Product the conversation belongs to (taken from the caller's token).</summary>
    public string Product { get; private set; } = default!;

    /// <summary>Organization of the user.</summary>
    public Guid OrgId { get; private set; }

    /// <summary>The user who owns the conversation.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Short title derived from the first question.</summary>
    public string Title { get; private set; } = default!;

    /// <summary>UTC time of the latest message.</summary>
    public DateTime LastMessageAt { get; private set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsDeleted { get; private set; }

    private Conversation() { }

    /// <summary>Starts a conversation titled after the first question.</summary>
    /// <param name="product">Product key.</param>
    /// <param name="orgId">Organization of the user.</param>
    /// <param name="userId">Owning user.</param>
    /// <param name="firstQuestion">The question that opens the thread.</param>
    /// <returns>The new conversation.</returns>
    public static Conversation Start(string product, Guid orgId, Guid userId, string firstQuestion)
    {
        if (string.IsNullOrWhiteSpace(firstQuestion)) throw new DomainException("A conversation needs a first question.");

        var title = firstQuestion.Trim().ReplaceLineEndings(" ");
        if (title.Length > TitlePreviewLength) title = title[..TitlePreviewLength].TrimEnd() + "…";

        return new Conversation
        {
            Product = product,
            OrgId = orgId,
            UserId = userId,
            Title = title,
            LastMessageAt = DateTime.UtcNow,
        };
    }

    /// <summary>Whether the conversation belongs to the given caller and is still visible.</summary>
    /// <param name="product">Caller's product.</param>
    /// <param name="orgId">Caller's org.</param>
    /// <param name="userId">Caller's user id.</param>
    /// <returns><c>true</c> if the caller owns it and it is not deleted.</returns>
    public bool IsOwnedBy(string product, Guid orgId, Guid userId) =>
        !IsDeleted && Product == product && OrgId == orgId && UserId == userId;

    /// <summary>Records that a new message was added.</summary>
    public void MarkActivity() => LastMessageAt = DateTime.UtcNow;

    /// <summary>Hides the conversation from the user.</summary>
    public void SoftDelete() => IsDeleted = true;
}
