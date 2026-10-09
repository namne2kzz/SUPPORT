using SUPPORT.Infrastructure.Knowledge;

namespace SUPPORT.UnitTests.Knowledge;

public sealed class MarkdownChunkerTests
{
    private static readonly ChunkingOptions Options = new(MaxTokens: 500, TargetTokens: 400, OverlapTokens: 60);

    [Fact]
    public void Chunk_BuildsBreadcrumbFromNestedHeadings()
    {
        const string body = """
            Giới thiệu chung về sprint.

            ## Đóng sprint
            Bấm **Close sprint**.

            ### Task chưa xong
            Quay về backlog.

            ## Tạo sprint
            Bấm **New sprint**.
            """;

        var chunks = MarkdownChunker.Chunk(body, "Quản lý Sprint", Options);

        chunks.Select(c => c.HeadingPath).ShouldBe(
        [
            "Quản lý Sprint",
            "Đóng sprint",
            "Đóng sprint › Task chưa xong",
            "Tạo sprint",
        ]);
        chunks[2].Content.ShouldBe("Quay về backlog.");
    }

    [Fact]
    public void Chunk_TitleHeadingAndEmptySections_AddNoChunks()
    {
        const string body = """
            # Quản lý Sprint
            ## Đóng sprint
            ### Task chưa xong
            Quay về backlog.
            """;

        var chunks = MarkdownChunker.Chunk(body, "Quản lý Sprint", Options);

        chunks.Count.ShouldBe(1);
        chunks[0].HeadingPath.ShouldBe("Đóng sprint › Task chưa xong");
    }

    [Fact]
    public void Chunk_HashInsideCodeFence_IsNotAHeading()
    {
        const string body = """
            ## Cấu hình
            ```bash
            # đây là comment, không phải heading
            ```
            """;

        var chunks = MarkdownChunker.Chunk(body, "Doc", Options);

        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldContain("# đây là comment");
    }

    [Fact]
    public void Chunk_LongSection_SplitsByParagraphWithOverlap()
    {
        // 12 paragraphs of ~140 chars (~40 tokens) → ~480 tokens is fine, so use 30 to force a split.
        var paragraphs = Enumerable.Range(1, 30).Select(i => $"Đoạn {i:00}. " + new string('x', 130)).ToList();
        var body = "## Dài\n" + string.Join("\n\n", paragraphs);

        var chunks = MarkdownChunker.Chunk(body, "Doc", Options);

        chunks.Count.ShouldBeGreaterThan(1);
        chunks.ShouldAllBe(c => c.HeadingPath == "Dài" && c.TokenCount <= Options.MaxTokens);

        // The last paragraph of one piece opens the next one.
        var lastOfFirst = chunks[0].Content.Split("\n\n")[^1];
        chunks[1].Content.ShouldStartWith(lastOfFirst);

        // Nothing is lost.
        foreach (var paragraph in paragraphs)
            chunks.ShouldContain(c => c.Content.Contains(paragraph));
    }

    [Fact]
    public void EstimateTokens_UsesCharsPerToken()
    {
        MarkdownChunker.EstimateTokens(new string('a', 35)).ShouldBe(10);
        MarkdownChunker.EstimateTokens("a").ShouldBe(1);
    }
}
