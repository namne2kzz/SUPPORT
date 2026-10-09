using Microsoft.EntityFrameworkCore;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Domain.Enums;
using SUPPORT.Infrastructure.Persistence;

namespace SUPPORT.Infrastructure.Knowledge;

/// <summary>Route-based lookups over knowledge documents.</summary>
/// <param name="db">Scoped context.</param>
internal sealed class KnowledgeCatalog(SupportDbContext db) : IKnowledgeCatalog
{
    private const string GeneralModule = "general";

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetSuggestionsAsync(string product, string? route, int take, CancellationToken ct)
    {
        var documents = await ActiveDocuments(product)
            .Select(d => new { d.Module, d.RouteHint, d.Suggestions })
            .ToListAsync(ct);

        var match = BestRouteMatch(documents.Select(d => (d.RouteHint, d)), route);
        var source = match is not null
            ? match.Suggestions
            : documents.Where(d => d.Module == GeneralModule).SelectMany(d => d.Suggestions).ToList();

        return [.. source.Distinct().Take(take)];
    }

    /// <inheritdoc />
    public async Task<string?> ResolveModuleAsync(string product, string? route, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(route)) return null;

        var documents = await ActiveDocuments(product)
            .Where(d => d.RouteHint != null)
            .Select(d => new { d.Module, d.RouteHint })
            .ToListAsync(ct);

        return BestRouteMatch(documents.Select(d => (d.RouteHint, d)), route)?.Module;
    }

    /// <inheritdoc />
    public async Task<(int ActiveDocuments, DateTime? LastIngestionAt)> GetStatsAsync(string product, CancellationToken ct)
    {
        var count = await ActiveDocuments(product).CountAsync(ct);
        var last = await db.IngestionRuns.AsNoTracking()
            .Where(r => r.Product == product && r.FinishedAt != null && r.Error == null)
            .MaxAsync(r => r.FinishedAt, ct);
        return (count, last);
    }

    private IQueryable<Domain.Entities.KnowledgeDocument> ActiveDocuments(string product) =>
        db.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.Product == product && d.OrgId == null && d.Status == KnowledgeDocumentStatus.Active);

    /// <summary>
    /// A document route matches when its segments appear, contiguous and whole, anywhere in the user's path — product
    /// routes carry tenant prefixes (<c>/acme/DASH/sprint-planning</c>), so documents declare only the stable part
    /// (<c>/sprint-planning</c>). The most specific (most segments) match wins; query strings are ignored and
    /// <c>/sprint-planning-old</c> does not match <c>/sprint-planning</c>.
    /// </summary>
    internal static T? BestRouteMatch<T>(IEnumerable<(string? Route, T Item)> candidates, string? route) where T : class
    {
        if (string.IsNullOrWhiteSpace(route)) return null;
        var path = Segments(route);

        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c.Route))
            .Select(c => (Segments: Segments(c.Route!), c.Item))
            .Where(c => c.Segments.Length > 0 && ContainsRun(path, c.Segments))
            .OrderByDescending(c => c.Segments.Length)
            .Select(c => c.Item)
            .FirstOrDefault();
    }

    private static string[] Segments(string route) =>
        route.Split('?', '#')[0].ToLowerInvariant().Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool ContainsRun(string[] path, string[] run)
    {
        for (var start = 0; start + run.Length <= path.Length; start++)
        {
            if (path.AsSpan(start, run.Length).SequenceEqual(run)) return true;
        }

        return false;
    }
}
