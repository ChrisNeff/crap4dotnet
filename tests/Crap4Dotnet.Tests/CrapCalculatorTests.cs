using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class CrapCalculatorTests
{
    [Theory]
    [InlineData(1, 1.0, 1.0)]      // fully covered, trivial
    [InlineData(10, 0.0, 110.0)]   // 100*1 + 10
    [InlineData(5, 0.5, 8.125)]    // 25*0.125 + 5, just over 8.0
    [InlineData(2, 1.0, 2.0)]
    public void Crap_matches_reference_values(int cc, double cov, double expected)
    {
        Assert.Equal(expected, CrapCalculator.Crap(cc, cov), precision: 3);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]  // clamped low -> treated as 0 coverage
    [InlineData(1.5, 1.0)]   // clamped high -> treated as full coverage
    public void Crap_clamps_coverage(double cov, double clampedCov)
    {
        Assert.Equal(CrapCalculator.Crap(4, clampedCov), CrapCalculator.Crap(4, cov), precision: 6);
    }

    [Fact]
    public void Score_computes_crap_and_flags_over_threshold()
    {
        var m = new MethodCoverage("Ns.Foo", "Bar", "(System.Int32)", "src/Foo.cs", 0.5, 5);
        var s = CrapCalculator.Score(m, threshold: 8.0);
        Assert.Equal(8.125, s.Crap!.Value, precision: 3);
        Assert.True(s.IsCrappy);
        Assert.Equal(5, s.Cc);
        Assert.Equal(0.5, s.Coverage);
    }

    [Fact]
    public void Score_returns_na_when_complexity_or_coverage_missing()
    {
        var noCc = CrapCalculator.Score(new MethodCoverage("T", "M", "()", "f.cs", 0.5, null), 8.0);
        var noCov = CrapCalculator.Score(new MethodCoverage("T", "M", "()", "f.cs", null, 5), 8.0);
        Assert.Null(noCc.Crap);
        Assert.False(noCc.IsCrappy);
        Assert.Null(noCov.Crap);
        Assert.False(noCov.IsCrappy);
    }
}
