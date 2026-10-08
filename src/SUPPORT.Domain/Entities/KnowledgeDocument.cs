using SUPPORT.Domain.Common;
using SUPPORT.Domain.Enums;

namespace SUPPORT.Domain.Entities;

/// <summary>
/// One knowledge source file (e.g. <c>guides/sprints</c>) of one product. Aggregate root over its
/// <see cref="KnowledgeChunk"/>s: chunks are always replaced as a set, never edited one by one.
/// </summary>
public sealed class KnowledgeDocument : Entity
{
    private readonly List<KnowledgeChunk> _chunks = [];

    /// <summary>Owning product key, e.g. <c>dashboard</c>.</summary>
    public string Product { get; private set; } = default!;

    /// <summary>Organization for org-specific knowledge; <c>null</c> = system documentation shared by every org.</summary>
    public Guid? OrgId { get; private set; }

    /// <summary>Kind of document.</summary>
    public KnowledgeSourceType SourceType { get; private set; }

    /// <summary>Stable identity of the source (front-matter <c>key</c>), unique per product + org.</summary>
    public string SourceKey { get; private set; } = default!;

    /// <summary>Display title shown in citations.</summary>
    public string Title { get; private set; } = default!;

    /// <summary>Product module the document belongs to (e.g. <c>sprints</c>), used to boost on-screen results.</summary>
    public string Module { get; private set; } = default!;

    /// <summary>Content language (ISO code).</summary>
    public string Language { get; private set; } = default!;

    /// <summary>Front-end route the document describes, used as the citation deep link.</summary>
    public string? RouteHint { get; private set; }

    /// <summary>Suggested starter questions shown when the user is on <see cref="RouteHint"/>.</summary>
    public List<string> Suggestions { get; private set; } = [];

    /// <summary>SHA-256 of the raw source, so unchanged files are skipped on re-index.</summary>
    public string ContentHash { get; private set; } = default!;

    /// <summary>Lifecycle status.</summary>
    public KnowledgeDocumentStatus Status { get; private set; }

    /// <summary>UTC time the document was last (re)indexed.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Current chunks of the document.</summary>
    public IReadOnlyList<KnowledgeChunk> Chunks => _chunks;

    private KnowledgeDocument() { }

    /// <summary>Creates a new active document with no chunks yet.</summary>
    /// <param name="product">Owning product key.</param>
    /// <param name="orgId">Org for org-specific knowledge; null for system docs.</param>
    /// <param name="sourceKey">Stable source identity.</param>
    /// <param name="metadata">Descriptive metadata from the source file.</param>
    /// <returns>The new document.</returns>
    public static KnowledgeDocument Create(string product, Guid? orgId, string sourceKey, KnowledgeDocumentMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(product)) throw new DomainException("Product is required.");
        if (string.IsNullOrWhiteSpace(sourceKey)) throw new DomainException("Source key is required.");

        var document = new KnowledgeDocument { Product = product, OrgId = orgId, SourceKey = sourceKey };
        document.Apply(metadata);
        return document;
    }

    /// <summary>Whether the stored content and embeddings are already current, so re-indexing can skip this document.</summary>
    /// <param name="contentHash">Hash of the source as it is now.</param>
    /// <param name="embeddingModel">Embedding model currently configured.</param>
    /// <returns><c>true</c> when nothing needs re-embedding.</returns>
    public bool IsUpToDate(string contentHash, string embeddingModel) =>
        Status == KnowledgeDocumentStatus.Active
        && ContentHash == contentHash
        && _chunks.Count > 0
        && _chunks.All(c => c.EmbeddingModel == embeddingModel);

    /// <summary>Replaces metadata and the whole chunk set with a freshly embedded version, reactivating the document.</summary>
    /// <param name="metadata">Descriptive metadata from the source file.</param>
    /// <param name="chunks">New chunks, in order.</param>
    public void Reindex(KnowledgeDocumentMetadata metadata, IReadOnlyList<KnowledgeChunkDraft> chunks)
    {
        if (chunks.Count == 0) throw new DomainException($"Document '{SourceKey}' produced no chunks.");

        Apply(metadata);
        _chunks.Clear();
        for (var i = 0; i < chunks.Count; i++)
            _chunks.Add(KnowledgeChunk.Create(this, i, chunks[i]));
    }

    /// <summary>Marks the document archived because its source file no longer exists.</summary>
    public void Archive()
    {
        Status = KnowledgeDocumentStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    private void Apply(KnowledgeDocumentMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.Title)) throw new DomainException("Title is required.");
        if (string.IsNullOrWhiteSpace(metadata.Module)) throw new DomainException("Module is required.");

        SourceType = metadata.SourceType;
        Title = metadata.Title.Trim();
        Module = metadata.Module.Trim().ToLowerInvariant();
        Language = string.IsNullOrWhiteSpace(metadata.Language) ? "vi" : metadata.Language.Trim();
        RouteHint = string.IsNullOrWhiteSpace(metadata.RouteHint) ? null : metadata.RouteHint.Trim();
        Suggestions = [.. metadata.Suggestions.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim())];
        ContentHash = metadata.ContentHash;
        Status = KnowledgeDocumentStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>Descriptive metadata of a knowledge source file.</summary>
/// <param name="SourceType">Kind of document.</param>
/// <param name="Title">Display title.</param>
/// <param name="Module">Product module.</param>
/// <param name="Language">Content language.</param>
/// <param name="RouteHint">Front-end route the document describes.</param>
/// <param name="Suggestions">Starter questions.</param>
/// <param name="ContentHash">SHA-256 of the raw source.</param>
public sealed record KnowledgeDocumentMetadata(
    KnowledgeSourceType SourceType,
    string Title,
    string Module,
    string Language,
    string? RouteHint,
    IReadOnlyList<string> Suggestions,
    string ContentHash);

/// <summary>A chunk ready to be attached to a document: text plus its embedding.</summary>
/// <param name="HeadingPath">Breadcrumb of headings, e.g. <c>Đóng sprint › Task chưa xong</c>.</param>
/// <param name="Content">Chunk text sent to the model as context.</param>
/// <param name="TokenCount">Approximate token count.</param>
/// <param name="Embedding">Normalized embedding vector.</param>
/// <param name="EmbeddingModel">Identifier of the model + dimension that produced <paramref name="Embedding"/>.</param>
public sealed record KnowledgeChunkDraft(
    string HeadingPath,
    string Content,
    int TokenCount,
    float[] Embedding,
    string EmbeddingModel);
