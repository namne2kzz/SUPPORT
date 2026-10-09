using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SUPPORT.Application.Common.Exceptions;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Domain.Entities;
using SUPPORT.Domain.Enums;
using SUPPORT.Infrastructure.AI;
using SUPPORT.Infrastructure.Persistence;
using SUPPORT.Infrastructure.Settings;

namespace SUPPORT.Infrastructure.Knowledge;

/// <summary>
/// Indexes each product's knowledge folder: unchanged files are skipped by hash, changed files are re-chunked and
/// re-embedded in one transaction per document, and documents whose file vanished are archived.
/// Also runs once at startup for every configured product.
/// </summary>
/// <param name="contextFactory">Context factory (this is a singleton, so it creates its own short-lived contexts).</param>
/// <param name="embeddings">Batched embedding service.</param>
/// <param name="settings">Knowledge settings.</param>
/// <param name="logger">Logger.</param>
internal sealed class KnowledgeIngestionService(
    IDbContextFactory<SupportDbContext> contextFactory,
    EmbeddingService embeddings,
    IOptions<KnowledgeSettings> settings,
    ILogger<KnowledgeIngestionService> logger) : BackgroundService, IKnowledgeIngestion
{
    // One run at a time across all products: runs share the free-tier embedding quota.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly KnowledgeSettings _settings = settings.Value;

    /// <inheritdoc />
    public async Task<IngestionResult> ReindexAsync(string product, string trigger, CancellationToken ct)
    {
        if (!_settings.Sources.TryGetValue(product, out var root) || string.IsNullOrWhiteSpace(root))
            throw new NotFoundException($"No knowledge source is configured for product '{product}'.");
        if (!Directory.Exists(root))
            throw new NotFoundException($"Knowledge folder for '{product}' does not exist: {root}");
        if (!embeddings.IsConfigured)
            throw new AssistantUnavailableException(AssistantErrorCodes.Unavailable, "Embedding API key is not configured.");

        await _gate.WaitAsync(ct);
        try
        {
            return await RunAsync(product, root, trigger, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.IngestOnStartup) return;
        if (!embeddings.IsConfigured)
        {
            logger.LogWarning("Skipping startup knowledge ingestion: no LLM API key configured");
            return;
        }

        foreach (var product in _settings.Sources.Keys)
        {
            try
            {
                await ReindexAsync(product, "startup", stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Startup indexing must never take the API down; the next reindex call retries.
                logger.LogError(ex, "Startup knowledge ingestion failed for {Product}", product);
            }
        }
    }

    private async Task<IngestionResult> RunAsync(string product, string root, string trigger, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var run = IngestionRun.Start(product, trigger);
        db.IngestionRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var skipped = new List<string>();
        try
        {
            // Projection only: whether each stored document is current, without pulling its embeddings into memory.
            var stored = await db.KnowledgeDocuments
                .Where(d => d.Product == product && d.OrgId == null)
                .Select(d => new
                {
                    d.Id,
                    d.SourceKey,
                    d.ContentHash,
                    d.Status,
                    ChunkCount = d.Chunks.Count,
                    StaleChunks = d.Chunks.Count(c => c.EmbeddingModel != embeddings.ModelId),
                })
                .ToDictionaryAsync(d => d.SourceKey, ct);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(root, path);
                KnowledgeFile file;
                try
                {
                    file = FrontMatterParser.Parse(relative, await File.ReadAllTextAsync(path, ct));
                }
                catch (FormatException ex)
                {
                    skipped.Add($"{relative}: {ex.Message}");
                    logger.LogWarning("Skipping knowledge file {File}: {Reason}", relative, ex.Message);
                    continue;
                }

                run.CountScanned();
                if (!seen.Add(file.SourceKey))
                {
                    skipped.Add($"{relative}: duplicate key '{file.SourceKey}'");
                    continue;
                }

                if (stored.TryGetValue(file.SourceKey, out var current)
                    && current.Status == KnowledgeDocumentStatus.Active
                    && current.ContentHash == file.ContentHash
                    && current.ChunkCount > 0
                    && current.StaleChunks == 0)
                    continue;

                var chunks = await BuildChunksAsync(file, ct);
                var metadata = new KnowledgeDocumentMetadata(
                    file.SourceType, file.Title, file.Module, file.Language, file.Route, file.Suggestions, file.ContentHash);

                var document = current is null
                    ? null
                    : await db.KnowledgeDocuments.Include(d => d.Chunks).SingleAsync(d => d.Id == current.Id, ct);

                if (document is null)
                {
                    document = KnowledgeDocument.Create(product, null, file.SourceKey, metadata);
                    db.KnowledgeDocuments.Add(document);
                }

                // Old chunks are orphaned and deleted, new ones inserted — one SaveChanges, so one transaction.
                document.Reindex(metadata, chunks);
                run.CountChanged(chunks.Count);
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                db.Attach(run);
            }

            var vanished = stored.Values
                .Where(d => d.Status == KnowledgeDocumentStatus.Active && !seen.Contains(d.SourceKey))
                .Select(d => d.Id)
                .ToList();
            if (vanished.Count > 0)
            {
                foreach (var document in await db.KnowledgeDocuments.Where(d => vanished.Contains(d.Id)).ToListAsync(ct))
                {
                    document.Archive();
                    run.CountArchived();
                }
            }

            run.Finish();
            await db.SaveChangesAsync(CancellationToken.None);

            logger.LogInformation(
                "Knowledge ingestion {Trigger} for {Product}: {Scanned} scanned, {Changed} re-embedded, {Archived} archived, {Chunks} chunks",
                trigger, product, run.DocsScanned, run.DocsChanged, run.DocsArchived, run.ChunksWritten);

            return new IngestionResult(run.Id, run.DocsScanned, run.DocsChanged, run.DocsArchived, run.ChunksWritten, skipped);
        }
        catch (Exception ex)
        {
            // Documents saved before the failure stay indexed; the next run picks up the rest by hash.
            db.ChangeTracker.Clear();
            db.Attach(run);
            run.Finish(ex.Message);
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<IReadOnlyList<KnowledgeChunkDraft>> BuildChunksAsync(KnowledgeFile file, CancellationToken ct)
    {
        var options = new ChunkingOptions(_settings.MaxChunkTokens, _settings.TargetChunkTokens, _settings.OverlapTokens);
        var chunks = MarkdownChunker.Chunk(file.Body, file.Title, options);

        // The breadcrumb travels with the text being embedded: a short chunk like "Chúng quay về backlog."
        // only becomes findable once it carries "Quản lý Sprint › Đóng sprint" with it.
        var vectors = await embeddings.EmbedAsync(
            [.. chunks.Select(c => $"{file.Title}{MarkdownChunker.PathSeparator}{c.HeadingPath}\n\n{c.Content}")], ct);

        return [.. chunks.Select((c, i) => new KnowledgeChunkDraft(c.HeadingPath, c.Content, c.TokenCount, vectors[i], embeddings.ModelId))];
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }
}
