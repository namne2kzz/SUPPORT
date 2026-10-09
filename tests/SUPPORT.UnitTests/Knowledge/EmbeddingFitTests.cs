using SUPPORT.Infrastructure.AI;

namespace SUPPORT.UnitTests.Knowledge;

public sealed class EmbeddingFitTests
{
    [Fact]
    public void Fit_LongerVector_TruncatesAndNormalizes()
    {
        var fitted = EmbeddingService.Fit([3f, 4f, 100f], dimensions: 2);

        fitted.ShouldBe([0.6f, 0.8f], tolerance: 1e-6);
    }

    [Fact]
    public void Fit_ShorterVector_Throws()
    {
        Should.Throw<InvalidOperationException>(() => EmbeddingService.Fit([1f], dimensions: 2));
    }

    [Fact]
    public void Fit_ZeroVector_Throws()
    {
        Should.Throw<InvalidOperationException>(() => EmbeddingService.Fit([0f, 0f], dimensions: 2));
    }
}
