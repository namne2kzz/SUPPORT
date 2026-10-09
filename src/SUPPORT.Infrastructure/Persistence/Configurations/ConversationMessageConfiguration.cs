using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUPPORT.Domain.Entities;
using SUPPORT.Domain.ValueObjects;

namespace SUPPORT.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ConversationMessage"/> to <c>chat_message</c>; citations are stored as jsonb.</summary>
internal sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.ToTable("chat_message");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content).IsRequired();
        builder.Property(m => m.PageRoute).HasMaxLength(200);
        builder.Property(m => m.Model).HasMaxLength(100);

        // Citations are written once with the answer and only ever read back whole, so a jsonb column
        // is simpler than a child table and keeps the answer + its sources in one row.
        builder.Property(m => m.Citations)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<Citation>>(v, JsonOptions) ?? new List<Citation>())
            .Metadata.SetValueComparer(new ValueComparer<List<Citation>>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Aggregate(0, (hash, c) => HashCode.Combine(hash, c.GetHashCode())),
                v => v.ToList()));

        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt });
    }
}
