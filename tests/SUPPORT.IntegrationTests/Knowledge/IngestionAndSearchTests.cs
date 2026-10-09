using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SUPPORT.Application.Common.Interfaces;
using SUPPORT.Domain.Enums;
using SUPPORT.Infrastructure.AI;
using SUPPORT.Infrastructure.Knowledge;
using SUPPORT.Infrastructure.Settings;
using SUPPORT.IntegrationTests.Fakes;
using SUPPORT.IntegrationTests.Infrastructure;

namespace SUPPORT.IntegrationTests.Knowledge;

[Collection(PostgresCollection.Name)]
public sealed class IngestionAndSearchTests(PostgresFixture fixture) : IDisposable
{
    // A product per test class keeps these documents invisible to other tests sharing the database.
    private readonly string _product = "p" + Guid.NewGuid().ToString("N")[..8];
    private readonly KnowledgeFolder _folder = new();
    private readonly BagOfWordsEmbeddingGenerator _generator = new();

    [Fact]
    public async Task Reindex_IsIdempotent_AndTracksChangesAndRemovals()
    {
        var ingestion = CreateIngestion();

        var first = await ingestion.ReindexAsync(_product, "test", CancellationToken.None);
        first.DocsScanned.ShouldBe(3);
        first.DocsChanged.ShouldBe(3);
        first.ChunksWritten.ShouldBe(6);

        var embeddedAfterFirst = _generator.EmbeddedTexts;
        var second = await ingestion.ReindexAsync(_product, "test", CancellationToken.None);
        second.DocsChanged.ShouldBe(0);
        _generator.EmbeddedTexts.ShouldBe(embeddedAfterFirst, "unchanged files must not be re-embedded");

        // Edit one document: its chunks are replaced in place (the unique (document_id, chunk_index) index must allow it).
        _folder.Write("guides/backlog.md", KnowledgeFolder.Backlog.Replace("Kéo thả item", "Kéo thả các item"));
        _folder.Delete("faq.md");
        var third = await ingestion.ReindexAsync(_product, "test", CancellationToken.None);
        third.DocsChanged.ShouldBe(1);
        third.DocsArchived.ShouldBe(1);

        await using var db = fixture.CreateContext();
        var documents = await db.KnowledgeDocuments.Include(d => d.Chunks).Where(d => d.Product == _product).ToListAsync();
        documents.Single(d => d.SourceKey == "faq").Status.ShouldBe(KnowledgeDocumentStatus.Archived);
        documents.Single(d => d.SourceKey == "guides/backlog").Chunks.ShouldContain(c => c.Content.Contains("các item"));
        (await db.IngestionRuns.CountAsync(r => r.Product == _product && r.FinishedAt != null && r.Error == null)).ShouldBe(3);
    }

    [Fact]
    public async Task Reindex_InvalidFile_IsSkippedNotFatal()
    {
        _folder.Write("broken.md", "no front-matter here");

        var result = await CreateIngestion().ReindexAsync(_product, "test", CancellationToken.None);

        result.DocsChanged.ShouldBe(3);
        result.SkippedFiles.ShouldHaveSingleItem().ShouldStartWith("broken.md");
    }

    [Fact]
    public async Task Search_FindsTheRightSection_WithAndWithoutDiacritics()
    {
        await CreateIngestion().ReindexAsync(_product, "test", CancellationToken.None);
        await using var db = fixture.CreateContext();
        var search = new HybridKnowledgeSearch(db, CreateEmbeddings());

        // A natural question: ranked first by both retrievers, but not every word appears, so no exact full-text hit.
        var accented = await search.SearchAsync(Query("Đóng sprint thì task chưa xong đi đâu?"), CancellationToken.None);
        accented[0].HeadingPath.ShouldBe("Đóng sprint › Task chưa xong");
        accented[0].IsFullTextHit.ShouldBeFalse();
        accented[0].CosineSimilarity.ShouldNotBeNull().ShouldBeGreaterThan(0.3f);

        // Typed without diacritics — still the same section, and now every word matches.
        var plain = await search.SearchAsync(Query("dong sprint task chua xong"), CancellationToken.None);
        plain[0].HeadingPath.ShouldBe("Đóng sprint › Task chưa xong");
        plain[0].IsFullTextHit.ShouldBeTrue();

        // An exact button name is the case full-text exists for.
        var button = await search.SearchAsync(Query("Promote to Sprint"), CancellationToken.None);
        button[0].Title.ShouldBe("Quản lý Backlog");
        button[0].IsFullTextHit.ShouldBeTrue();
    }

    [Fact]
    public async Task Search_NeverReturnsArchivedOrOtherProductsDocuments()
    {
        var ingestion = CreateIngestion();
        await ingestion.ReindexAsync(_product, "test", CancellationToken.None);
        _folder.Delete("faq.md");
        await ingestion.ReindexAsync(_product, "test", CancellationToken.None);

        await using var db = fixture.CreateContext();
        var search = new HybridKnowledgeSearch(db, CreateEmbeddings());

        var hits = await search.SearchAsync(Query("quên mật khẩu"), CancellationToken.None);
        hits.ShouldNotContain(h => h.Title == "Câu hỏi thường gặp");

        var otherProduct = await search.SearchAsync(Query("đóng sprint") with { Product = "someone-else" }, CancellationToken.None);
        otherProduct.ShouldBeEmpty();
    }

    [Fact]
    public async Task Catalog_SuggestsByRoute_AndFallsBackToGeneral()
    {
        await CreateIngestion().ReindexAsync(_product, "test", CancellationToken.None);
        await using var db = fixture.CreateContext();
        var catalog = new KnowledgeCatalog(db);

        (await catalog.GetSuggestionsAsync(_product, "/acme/DASH/sprint-planning", 4, CancellationToken.None))
            .ShouldBe(["Làm sao đóng sprint?", "Task chưa xong khi đóng sprint đi đâu?"]);
        (await catalog.GetSuggestionsAsync(_product, "/members", 4, CancellationToken.None))
            .ShouldBe(["Tôi quên mật khẩu thì làm sao?"]);
        (await catalog.ResolveModuleAsync(_product, "/acme/DASH/backlog", CancellationToken.None)).ShouldBe("backlog");

        var (documents, last) = await catalog.GetStatsAsync(_product, CancellationToken.None);
        documents.ShouldBe(3);
        last.ShouldNotBeNull();
    }

    private KnowledgeQuery Query(string text) => new(_product, Guid.NewGuid(), text, null, 5);

    private EmbeddingService CreateEmbeddings() =>
        new(_generator, Options.Create(new LlmSettings { ApiKey = "test", ProviderName = "fake" }));

    private KnowledgeIngestionService CreateIngestion() => new(
        new FixtureContextFactory(fixture),
        CreateEmbeddings(),
        Options.Create(new KnowledgeSettings { Sources = new() { [_product] = _folder.Root } }),
        NullLogger<KnowledgeIngestionService>.Instance);

    public void Dispose() => _folder.Dispose();
}
