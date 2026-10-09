using SUPPORT.Infrastructure.Knowledge;

namespace SUPPORT.UnitTests.Knowledge;

public sealed class RouteMatchTests
{
    private static readonly (string? Route, string Item)[] Documents =
    [
        ("/sprint-planning", "sprints"),
        ("/backlog", "backlog"),
        ("/settings", "settings"),
        ("/settings/members", "members"),
        (null, "general"),
    ];

    [Theory]
    [InlineData("/acme/DASH/sprint-planning", "sprints")]
    [InlineData("/acme/DASH/backlog?type=epic", "backlog")]
    [InlineData("/acme/settings/members", "members")]
    [InlineData("/acme/settings/general", "settings")]
    [InlineData("backlog", "backlog")]
    public void BestRouteMatch_MostSpecificSegmentRunWins(string route, string expected)
    {
        KnowledgeCatalog.BestRouteMatch(Documents, route).ShouldBe(expected);
    }

    [Theory]
    [InlineData("/acme/DASH/sprint-planning-old")]
    [InlineData("/acme/DASH/boards")]
    [InlineData("")]
    [InlineData(null)]
    public void BestRouteMatch_NoWholeSegmentMatch_ReturnsNull(string? route)
    {
        KnowledgeCatalog.BestRouteMatch(Documents, route).ShouldBeNull();
    }
}
