namespace SUPPORT.Domain.ValueObjects;

/// <summary>A knowledge source an answer was grounded on. Stored as JSON on the message.</summary>
/// <param name="ChunkId">Retrieved chunk (soft reference; the chunk may since have been re-indexed).</param>
/// <param name="Title">Document title at answer time.</param>
/// <param name="HeadingPath">Section breadcrumb at answer time.</param>
/// <param name="Route">Front-end route to deep-link to, if any.</param>
/// <param name="Score">Cosine similarity of the chunk to the question.</param>
public sealed record Citation(Guid ChunkId, string Title, string HeadingPath, string? Route, float Score);
