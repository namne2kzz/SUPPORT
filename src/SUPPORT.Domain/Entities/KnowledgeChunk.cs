using SUPPORT.Domain.Common;

namespace SUPPORT.Domain.Entities;

/// <summary>A retrievable slice of a <see cref="KnowledgeDocument"/> with its embedding.</summary>
/// <remarks>
/// <see cref="Product"/>, <see cref="OrgId"/> and <see cref="Module"/> are copied from the document so the
/// vector search can filter on them without a join. They stay in sync because chunks are only ever
/// rewritten as a set by <see cref="KnowledgeDocument.Reindex"/>.
/// </remarks>
public sealed class KnowledgeChunk : Entity
{
    /// <summary>Owning document id.</summary>
    public Guid DocumentId { get; private set; }

    /// <summary>Owning product key (denormalized).</summary>
    public string Product { get; private set; } = default!;

    /// <summary>Owning org, null for system docs (denormalized).</summary>
    public Guid? OrgId { get; private set; }

    /// <summary>Product module (denormalized).</summary>
    public string Module { get; private set; } = default!;

    /// <summary>Position within the document.</summary>
    public int ChunkIndex { get; private set; }

    /// <summary>Breadcrumb of headings leading to this chunk.</summary>
    public string HeadingPath { get; private set; } = default!;

    /// <summary>Chunk text.</summary>
    public string Content { get; private set; } = default!;

    /// <summary>Approximate token count.</summary>
    public int TokenCount { get; private set; }

    /// <summary>Normalized embedding vector.</summary>
    public float[] Embedding { get; private set; } = [];

    /// <summary>Model + dimension that produced the embedding; a mismatch with config forces a re-embed.</summary>
    public string EmbeddingModel { get; private set; } = default!;

    private KnowledgeChunk() { }

    /// <summary>Creates a chunk belonging to <paramref name="document"/>.</summary>
    /// <param name="document">Owning document.</param>
    /// <param name="index">Position within the document.</param>
    /// <param name="draft">Chunk text and embedding.</param>
    /// <returns>The new chunk.</returns>
    internal static KnowledgeChunk Create(KnowledgeDocument document, int index, KnowledgeChunkDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Content)) throw new DomainException("Chunk content is required.");
        if (draft.Embedding.Length == 0) throw new DomainException("Chunk embedding is required.");

        return new KnowledgeChunk
        {
            DocumentId = document.Id,
            Product = document.Product,
            OrgId = document.OrgId,
            Module = document.Module,
            ChunkIndex = index,
            HeadingPath = draft.HeadingPath,
            Content = draft.Content,
            TokenCount = draft.TokenCount,
            Embedding = draft.Embedding,
            EmbeddingModel = draft.EmbeddingModel,
        };
    }
}
