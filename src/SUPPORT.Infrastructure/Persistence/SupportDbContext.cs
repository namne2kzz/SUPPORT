using Microsoft.EntityFrameworkCore;
using SUPPORT.Domain.Entities;

namespace SUPPORT.Infrastructure.Persistence;

/// <summary>EF Core context for the NMate database (PostgreSQL + pgvector).</summary>
/// <param name="options">Context options (provider, naming convention, vector plugin).</param>
public sealed class SupportDbContext(DbContextOptions<SupportDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Size of every stored embedding. Fixed in the schema because pgvector needs a declared dimension
    /// to build an HNSW index; changing it means a migration plus a full re-index.
    /// </summary>
    public const int EmbeddingDimensions = 768;

    /// <summary>Immutable wrapper around <c>unaccent()</c>, created by the Init migration.</summary>
    /// <remarks>
    /// <c>unaccent()</c> itself is only STABLE (it reads a dictionary), and PostgreSQL refuses non-immutable
    /// functions in a generated column. Pinning the dictionary explicitly makes the wrapper safe to mark IMMUTABLE.
    /// </remarks>
    public const string ImmutableUnaccentFunction = "support_unaccent";

    /// <summary>Knowledge source documents.</summary>
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();

    /// <summary>Embedded chunks of knowledge documents.</summary>
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    /// <summary>Re-index audit log.</summary>
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();

    /// <summary>Chat threads.</summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>Questions and answers.</summary>
    public DbSet<ConversationMessage> Messages => Set<ConversationMessage>();

    /// <summary>Thumbs-up/down on answers.</summary>
    public DbSet<MessageFeedback> Feedback => Set<MessageFeedback>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("unaccent");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDbContext).Assembly);

        // Ids are assigned in the domain (UUIDv7), never by the database. Telling EF so matters: otherwise a new
        // chunk added through KnowledgeDocument.Chunks already carries a key, EF assumes it exists, and issues an
        // UPDATE that matches no row instead of an INSERT.
        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(e => typeof(Domain.Common.Entity).IsAssignableFrom(e.ClrType)))
            entity.FindProperty(nameof(Domain.Common.Entity.Id))!.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
    }
}
