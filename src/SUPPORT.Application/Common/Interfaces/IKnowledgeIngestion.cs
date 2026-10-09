namespace SUPPORT.Application.Common.Interfaces;

/// <summary>Re-indexes a product's knowledge folder into the vector store.</summary>
public interface IKnowledgeIngestion
{
    /// <summary>
    /// Scans the product's knowledge source, re-embeds documents whose content or embedding model changed,
    /// and archives documents whose file disappeared. Safe to call repeatedly: unchanged files cost nothing.
    /// </summary>
    /// <param name="product">Product to index.</param>
    /// <param name="trigger">What started the run (<c>startup</c>, <c>reindex-api</c>), recorded in the audit log.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Counts for the run.</returns>
    Task<IngestionResult> ReindexAsync(string product, string trigger, CancellationToken ct);
}

/// <summary>Outcome of one re-index run.</summary>
/// <param name="RunId">Audit record id.</param>
/// <param name="DocsScanned">Source files found.</param>
/// <param name="DocsChanged">Documents (re)embedded.</param>
/// <param name="DocsArchived">Documents archived.</param>
/// <param name="ChunksWritten">Chunks written.</param>
/// <param name="SkippedFiles">Files ignored because their front-matter was invalid, with the reason.</param>
public sealed record IngestionResult(
    Guid RunId,
    int DocsScanned,
    int DocsChanged,
    int DocsArchived,
    int ChunksWritten,
    IReadOnlyList<string> SkippedFiles);
