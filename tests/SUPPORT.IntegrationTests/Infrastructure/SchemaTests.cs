using Microsoft.EntityFrameworkCore;
using SUPPORT.Domain.Entities;
using SUPPORT.Domain.Enums;
using SUPPORT.Domain.ValueObjects;
using SUPPORT.Infrastructure.Persistence;

namespace SUPPORT.IntegrationTests.Infrastructure;

[Collection(PostgresCollection.Name)]
public sealed class SchemaTests(PostgresFixture fixture)
{
    private const string Model = "test/fake@768";

    [Fact]
    public async Task Document_WithChunks_RoundTripsEmbeddingAndSuggestions()
    {
        var document = NewDocument("guides/roundtrip");
        document.Reindex(Metadata("hash-1"), [Draft("Đóng sprint", "Task chưa xong quay về backlog.", Axis(0))]);

        await using (var db = fixture.CreateContext())
        {
            db.KnowledgeDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        var loaded = await read.KnowledgeDocuments.Include(d => d.Chunks).SingleAsync(d => d.Id == document.Id);

        loaded.Suggestions.ShouldBe(["Làm sao đóng sprint?"]);
        loaded.Chunks.Count.ShouldBe(1);
        loaded.Chunks[0].Embedding.Length.ShouldBe(SupportDbContext.EmbeddingDimensions);
        loaded.Chunks[0].Embedding[0].ShouldBe(1f);
        loaded.IsUpToDate("hash-1", Model).ShouldBeTrue();
    }

    [Fact]
    public async Task SearchTsv_MatchesQueryWithoutVietnameseDiacritics()
    {
        var document = NewDocument("guides/unaccent");
        document.Reindex(Metadata("hash-2"), [Draft("Đóng sprint", "Khi đóng sprint, các task chưa xong sẽ quay về backlog.", Axis(1))]);

        await using var db = fixture.CreateContext();
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync();

        var hits = await db.Database
            .SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM kb_chunk
                WHERE document_id = {document.Id}
                  AND search_tsv @@ websearch_to_tsquery('simple', support_unaccent({"dong sprint"}))
                """)
            .ToListAsync();

        hits.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CosineDistance_OrdersNearestChunkFirst()
    {
        var document = NewDocument("guides/cosine");
        document.Reindex(Metadata("hash-3"),
        [
            Draft("Xa", "Không liên quan.", Axis(5)),
            Draft("Gần", "Rất liên quan.", Axis(4)),
        ]);

        await using var db = fixture.CreateContext();
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync();

        var query = new Pgvector.Vector(Axis(4));
        var nearest = await db.Database
            .SqlQuery<string>($"""
                SELECT heading_path AS "Value" FROM kb_chunk
                WHERE document_id = {document.Id}
                ORDER BY embedding <=> {query}
                LIMIT 1
                """)
            .SingleAsync();

        nearest.ShouldBe("Gần");
    }

    [Fact]
    public async Task SystemDocuments_WithSameKey_AreRejected()
    {
        await using var db = fixture.CreateContext();
        db.KnowledgeDocuments.Add(NewDocument("guides/duplicate"));
        await db.SaveChangesAsync();

        await using var second = fixture.CreateContext();
        second.KnowledgeDocuments.Add(NewDocument("guides/duplicate"));

        // org_id is NULL on both rows; NULLS NOT DISTINCT is what makes this collide.
        await Should.ThrowAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Message_Citations_RoundTripAsJson_AndCascadeWithConversation()
    {
        var conversation = Conversation.Start("dashboard", Guid.NewGuid(), Guid.NewGuid(), "Làm sao đóng sprint?");
        var citation = new Citation(Guid.NewGuid(), "Quản lý Sprint", "Đóng sprint", "/sprints", 0.71f);
        var answer = ConversationMessage.AssistantAnswer(conversation.Id,
            new AssistantAnswer("Vào Sprint Planning…", [citation], "gemini", 10, 20, 1200, 0.71f, false));

        await using (var db = fixture.CreateContext())
        {
            db.Conversations.Add(conversation);
            db.Messages.Add(answer);
            db.Feedback.Add(MessageFeedback.Create(answer.Id, conversation.UserId, 1, "Hữu ích"));
            await db.SaveChangesAsync();
        }

        await using (var read = fixture.CreateContext())
        {
            var loaded = await read.Messages.AsNoTracking().SingleAsync(m => m.Id == answer.Id);
            loaded.Citations.ShouldBe([citation]);

            await read.Conversations.Where(c => c.Id == conversation.Id).ExecuteDeleteAsync();
        }

        await using var check = fixture.CreateContext();
        (await check.Messages.AnyAsync(m => m.Id == answer.Id)).ShouldBeFalse();
        (await check.Feedback.AnyAsync(f => f.MessageId == answer.Id)).ShouldBeFalse();
    }

    private static KnowledgeDocument NewDocument(string key) =>
        KnowledgeDocument.Create("dashboard", null, key, Metadata("hash-0"));

    private static KnowledgeDocumentMetadata Metadata(string hash) =>
        new(KnowledgeSourceType.UserGuide, "Quản lý Sprint", "sprints", "vi", "/sprints", ["Làm sao đóng sprint?"], hash);

    private static KnowledgeChunkDraft Draft(string heading, string content, float[] embedding) =>
        new(heading, content, content.Length / 4, embedding, Model);

    /// <summary>Unit vector along one axis — makes cosine distances predictable.</summary>
    private static float[] Axis(int index)
    {
        var vector = new float[SupportDbContext.EmbeddingDimensions];
        vector[index] = 1f;
        return vector;
    }
}
