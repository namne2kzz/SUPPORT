using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUPPORT.Domain.Entities;

namespace SUPPORT.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="MessageFeedback"/> to <c>chat_feedback</c> (one row per rated message).</summary>
internal sealed class MessageFeedbackConfiguration : IEntityTypeConfiguration<MessageFeedback>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<MessageFeedback> builder)
    {
        builder.ToTable("chat_feedback", t => t.HasCheckConstraint("ck_chat_feedback_rating", "rating IN (-1, 1)"));
        builder.HasKey(f => f.MessageId);

        builder.Property(f => f.Comment).HasMaxLength(1000);

        builder.HasOne<ConversationMessage>()
            .WithOne()
            .HasForeignKey<MessageFeedback>(f => f.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
