using SUPPORT.Domain.Enums;
using SUPPORT.Infrastructure.Knowledge;

namespace SUPPORT.UnitTests.Knowledge;

public sealed class FrontMatterParserTests
{
    private const string Valid = """
        ---
        key: guides/sprints
        title: Quản lý Sprint
        module: sprints
        type: faq
        route: /sprints
        suggestions:
          - Làm sao đóng sprint?
          - Task chưa xong đi đâu?
        ---
        ## Đóng sprint
        Bấm Close sprint.
        """;

    [Fact]
    public void Parse_ReadsAllFields()
    {
        var file = FrontMatterParser.Parse("guides/sprints.md", Valid);

        file.SourceKey.ShouldBe("guides/sprints");
        file.Title.ShouldBe("Quản lý Sprint");
        file.Module.ShouldBe("sprints");
        file.SourceType.ShouldBe(KnowledgeSourceType.Faq);
        file.Language.ShouldBe("vi");
        file.Route.ShouldBe("/sprints");
        file.Suggestions.ShouldBe(["Làm sao đóng sprint?", "Task chưa xong đi đâu?"]);
        file.Body.ShouldStartWith("## Đóng sprint");
        file.ContentHash.Length.ShouldBe(64);
    }

    [Fact]
    public void Parse_WithoutKey_DerivesKeyFromPath()
    {
        // Normalize first: with git autocrlf the source file (and so this raw literal) may be CRLF.
        var raw = Valid.ReplaceLineEndings("\n").Replace("key: guides/sprints\n", "");

        FrontMatterParser.Parse(@"Guides\Sprints.md", raw).SourceKey.ShouldBe("guides/sprints");
    }

    [Fact]
    public void Parse_CrLfLineEndings_AreAccepted()
    {
        var crlf = FrontMatterParser.Parse("a.md", Valid.ReplaceLineEndings("\r\n"));
        var lf = FrontMatterParser.Parse("a.md", Valid.ReplaceLineEndings("\n"));

        crlf.Title.ShouldBe("Quản lý Sprint");
        crlf.Body.ShouldBe(lf.Body);
        crlf.ContentHash.ShouldBe(lf.ContentHash, "a line-ending flip must not trigger a re-embed");
    }

    [Theory]
    [InlineData("## no front-matter")]
    [InlineData("---\ntitle: x\nmodule: y\n## never closed")]
    [InlineData("---\nmodule: y\n---\nbody")]
    [InlineData("---\ntitle: x\n---\nbody")]
    [InlineData("---\ntitle: x\nmodule: y\ntype: nope\n---\nbody")]
    [InlineData("---\ntitle: x\nmodule: y\n---\n   ")]
    public void Parse_InvalidFile_Throws(string raw)
    {
        Should.Throw<FormatException>(() => FrontMatterParser.Parse("a.md", raw));
    }

    [Fact]
    public void Parse_FrontMatterChange_ChangesHash()
    {
        var original = FrontMatterParser.Parse("a.md", Valid);
        var retitled = FrontMatterParser.Parse("a.md", Valid.Replace("Quản lý Sprint", "Sprint"));

        retitled.ContentHash.ShouldNotBe(original.ContentHash);
    }
}
