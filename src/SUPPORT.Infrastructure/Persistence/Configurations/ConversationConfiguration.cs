using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUPPORT.Domain.Entities;

namespace SUPPORT.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Conversation"/> to <c>chat_conversation</c>.</summary>
internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("chat_conversation");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Product).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(Conversation.TitleMaxLength).IsRequired();

        // The history list: one user's live threads, newest first. Deleted rows never appear in it.
        builder.HasIndex(c => new { c.Product, c.OrgId, c.UserId, c.LastMessageAt })
            .IsDescending(false, false, false, true)
            .HasFilter("is_deleted = false");
    }
}
