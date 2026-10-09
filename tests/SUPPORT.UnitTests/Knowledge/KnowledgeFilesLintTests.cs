using SUPPORT.Infrastructure.Knowledge;

namespace SUPPORT.UnitTests.Knowledge;

/// <summary>
/// Lints a real knowledge folder with the production parser and chunker. Runs only when
/// <c>SUPPORT_KNOWLEDGE_DIR</c> points at one (e.g. the DASHBOARD checkout's <c>Knowledge/</c>), so the DASHBOARD
/// CI can reuse it to reject a broken file before it is synced.
/// </summary>
public sealed class KnowledgeFilesLintTests
{
    private static readonly ChunkingOptions Options = new(500, 400, 60);

    [Fact]
    public void EveryKnowledgeFile_ParsesAndChunks()
    {
        var root = Environment.GetEnvironmentVariable("SUPPORT_KNOWLEDGE_DIR");
        if (string.IsNullOrWhiteSpace(root)) return;

        Directory.Exists(root).ShouldBeTrue($"SUPPORT_KNOWLEDGE_DIR does not exist: {root}");
        var files = Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).ToList();
        files.ShouldNotBeEmpty();

        var keys = new HashSet<string>();
        foreach (var path in files)
        {
            var relative = Path.GetRelativePath(root, path);
            var file = FrontMatterParser.Parse(relative, File.ReadAllText(path));
            keys.Add(file.SourceKey).ShouldBeTrue($"{relative}: duplicate key '{file.SourceKey}'");
            file.Suggestions.ShouldNotBeEmpty($"{relative}: add at least one suggestion");

            var chunks = MarkdownChunker.Chunk(file.Body, file.Title, Options);
            chunks.ShouldNotBeEmpty($"{relative}: no chunks");
            chunks.ShouldAllBe(c => c.TokenCount <= Options.MaxTokens);
        }
    }
}
