using SUPPORT.Infrastructure.Knowledge;

namespace SUPPORT.UnitTests.Knowledge;

public sealed class AnyWordQueryTests
{
    [Fact]
    public void AnyWordQuery_JoinsDistinctWordsWithOr()
    {
        HybridKnowledgeSearch.AnyWordQuery("Đóng sprint thì task, Sprint đi đâu?")
            .ShouldBe("Đóng or sprint or thì or task or đi or đâu");
    }

    [Fact]
    public void AnyWordQuery_StripsWebsearchOperators()
    {
        HybridKnowledgeSearch.AnyWordQuery("\"close\" -sprint or (drop)")
            .ShouldBe("close or sprint or or or drop");
    }

    [Fact]
    public void AnyWordQuery_NoUsableWord_ReturnsInput()
    {
        HybridKnowledgeSearch.AnyWordQuery("?!").ShouldBe("?!");
    }
}
