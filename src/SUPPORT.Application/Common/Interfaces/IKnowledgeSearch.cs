namespace SUPPORT.Application.Common.Interfaces;

/// <summary>Hybrid (vector + full-text) retrieval over the knowledge base.</summary>
public interface IKnowledgeSearch
{
    /// <summary>Finds the chunks most relevant to a question.</summary>
    /// <param name="query">Question and scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Best hits first; empty when nothing matched at all.</returns>
    Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct);
}

/// <summary>A retrieval request.</summary>
/// <param name="Product">Product whose knowledge to search.</param>
/// <param name="OrgId">Caller's org — org-specific documents of other orgs are never returned.</param>
/// <param name="Text">The question.</param>
/// <param name="Module">Module of the screen the user is on, boosted slightly; null for none.</param>
/// <param name="TopK">Maximum hits to return.</param>
public sealed record KnowledgeQuery(string Product, Guid OrgId, string Text, string? Module, int TopK);

/// <summary>One retrieved chunk.</summary>
/// <param name="ChunkId">Chunk id.</param>
/// <param name="Title">Document title.</param>
/// <param name="HeadingPath">Section breadcrumb.</param>
/// <param name="Content">Chunk text.</param>
/// <param name="RouteHint">Front-end route of the document.</param>
/// <param name="CosineSimilarity">Similarity to the question, or null when the chunk was found by full-text only.</param>
/// <param name="IsFullTextHit">Whether full-text search matched the chunk.</param>
/// <param name="Score">Fused (RRF) ranking score.</param>
public sealed record KnowledgeHit(
    Guid ChunkId,
    string Title,
    string HeadingPath,
    string Content,
    string? RouteHint,
    float? CosineSimilarity,
    bool IsFullTextHit,
    double Score);
