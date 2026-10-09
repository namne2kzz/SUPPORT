using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Pgvector;
using SUPPORT.Domain.Entities;

namespace SUPPORT.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="KnowledgeChunk"/> to <c>kb_chunk</c>, with the vector and full-text indexes.</summary>
internal sealed class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    /// <summary>Shadow property holding the generated full-text vector (never read by the domain).</summary>
    public const string SearchVectorProperty = "SearchTsv";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("kb_chunk");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Product).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Module).HasMaxLength(50).IsRequired();
        builder.Property(c => c.HeadingPath).HasMaxLength(500).IsRequired();
        builder.Property(c => c.Content).IsRequired();
        builder.Property(c => c.EmbeddingModel).HasMaxLength(100).IsRequired();

        // The domain keeps a plain float[] so it never depends on pgvector; the conversion lives here.
        builder.Property(c => c.Embedding)
            .HasColumnType($"vector({SupportDbContext.EmbeddingDimensions})")
            .HasConversion(v => new Vector(v), v => v.ToArray())
            .Metadata.SetValueComparer(new ValueComparer<float[]>(
                (a, b) => a!.SequenceEqual(b!),
                v => v.Length,
                v => v.ToArray()));

        // 'simple' config: PostgreSQL has no Vietnamese dictionary, and stemming would mangle product
        // terms anyway. Unaccenting lets "dong sprint" match "đóng sprint".
        builder.Property<NpgsqlTsVector>(SearchVectorProperty)
            .HasColumnName("search_tsv")
            .HasComputedColumnSql(
                $"to_tsvector('simple', {SupportDbContext.ImmutableUnaccentFunction}(heading_path || ' ' || content))",
                stored: true);

        builder.HasIndex(c => new { c.DocumentId, c.ChunkIndex }).IsUnique();
        builder.HasIndex(c => new { c.Product, c.OrgId, c.Module });

        // HNSW over cosine distance: no training step, so it stays correct as chunks are added or replaced.
        builder.HasIndex(c => c.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64);

        builder.HasIndex(SearchVectorProperty).HasMethod("gin");
    }
}
