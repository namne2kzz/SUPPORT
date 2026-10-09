namespace SUPPORT.Application.Common.Interfaces;

/// <summary>Read-only facts about the indexed knowledge (no vector search).</summary>
public interface IKnowledgeCatalog
{
    /// <summary>Starter questions for a screen: from documents whose route matches, else from general documents.</summary>
    /// <param name="product">Product key.</param>
    /// <param name="route">Current front-end route, e.g. <c>/acme/DASH/sprint-planning</c>.</param>
    /// <param name="take">Maximum suggestions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Suggested questions.</returns>
    Task<IReadOnlyList<string>> GetSuggestionsAsync(string product, string? route, int take, CancellationToken ct);

    /// <summary>Module of the document that best matches a route, used to boost on-screen results.</summary>
    /// <param name="product">Product key.</param>
    /// <param name="route">Current front-end route.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The module, or null when no document claims the route.</returns>
    Task<string?> ResolveModuleAsync(string product, string? route, CancellationToken ct);

    /// <summary>Size of the active knowledge base and when it was last indexed.</summary>
    /// <param name="product">Product key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Active document count and the latest finished ingestion time.</returns>
    Task<(int ActiveDocuments, DateTime? LastIngestionAt)> GetStatsAsync(string product, CancellationToken ct);
}
