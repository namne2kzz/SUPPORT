using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SUPPORT.Domain.Entities;

namespace SUPPORT.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="KnowledgeDocument"/> to <c>kb_document</c>.</summary>
internal sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("kb_document");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Product).HasMaxLength(30).IsRequired();
        builder.Property(d => d.SourceKey).HasMaxLength(300).IsRequired();
        builder.Property(d => d.Title).HasMaxLength(300).IsRequired();
        builder.Property(d => d.Module).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Language).HasMaxLength(10).IsRequired();
        builder.Property(d => d.RouteHint).HasMaxLength(200);
        builder.Property(d => d.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(d => d.Suggestions).HasColumnType("text[]");

        // One document per source key within a product + org. NULLS NOT DISTINCT so two system documents
        // (org_id NULL) with the same key also collide, which a plain unique index would let through.
        builder.HasIndex(d => new { d.Product, d.OrgId, d.SourceKey })
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasMany(d => d.Chunks)
            .WithOne()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(d => d.Chunks).HasField("_chunks").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
