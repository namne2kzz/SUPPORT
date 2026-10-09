using Microsoft.EntityFrameworkCore;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Domain.Entities;

namespace SUPPORT.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IConversationRepository"/>.</summary>
/// <param name="db">Scoped context.</param>
internal sealed class ConversationRepository(SupportDbContext db) : IConversationRepository
{
    /// <inheritdoc />
    public Task<Conversation?> GetAsync(Guid id, CancellationToken ct) =>
        db.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationMessage>> GetRecentMessagesAsync(Guid conversationId, int take, CancellationToken ct)
    {
        var latest = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(take)
            .ToListAsync(ct);

        latest.Reverse();
        return latest;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(ConversationMessage Message, short? Rating)>> GetMessagesWithRatingsAsync(Guid conversationId, CancellationToken ct)
    {
        var rows = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new
            {
                Message = m,
                Rating = db.Feedback.Where(f => f.MessageId == m.Id).Select(f => (short?)f.Rating).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => (r.Message, r.Rating))];
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Conversation> Items, int Total)> ListAsync(
        string product, Guid orgId, Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Conversations.AsNoTracking()
            .Where(c => c.Product == product && c.OrgId == orgId && c.UserId == userId && !c.IsDeleted);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.LastMessageAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(ConversationMessage Message, Conversation Conversation)?> GetMessageAsync(Guid messageId, CancellationToken ct)
    {
        var row = await (
                from m in db.Messages.AsNoTracking()
                join c in db.Conversations.AsNoTracking() on m.ConversationId equals c.Id
                where m.Id == messageId
                select new { Message = m, Conversation = c })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : (row.Message, row.Conversation);
    }

    /// <inheritdoc />
    public Task<MessageFeedback?> GetFeedbackAsync(Guid messageId, CancellationToken ct) =>
        db.Feedback.FirstOrDefaultAsync(f => f.MessageId == messageId, ct);

    /// <inheritdoc />
    public void Add(Conversation conversation) => db.Conversations.Add(conversation);

    /// <inheritdoc />
    public void Add(ConversationMessage message) => db.Messages.Add(message);

    /// <inheritdoc />
    public void Add(MessageFeedback feedback) => db.Feedback.Add(feedback);

    /// <inheritdoc />
    public Task<int> PurgeUserAsync(string product, Guid userId, CancellationToken ct) =>
        db.Conversations.Where(c => c.Product == product && c.UserId == userId).ExecuteDeleteAsync(ct);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
