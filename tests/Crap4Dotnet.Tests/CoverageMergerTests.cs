using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class CoverageMergerTests
{
    [Fact]
    public void Merge_keeps_highest_coverage_for_duplicate_method()
    {
        var input = new[]
        {
            new MethodCoverage("T", "M", "()", "f.cs", 0.2, 5),
            new MethodCoverage("T", "M", "()", "f.cs", 0.9, 5),
        };
        var merged = CoverageMerger.Merge(input);
        var m = Assert.Single(merged);
        Assert.Equal(0.9, m.LineRate);
        Assert.Equal(5, m.Complexity);
    }

    [Fact]
    public void Merge_treats_different_signatures_as_distinct()
    {
        var input = new[]
        {
            new MethodCoverage("T", "M", "(System.Int32)", "f.cs", 0.5, 3),
            new MethodCoverage("T", "M", "(System.String)", "f.cs", 0.5, 4),
        };
        Assert.Equal(2, CoverageMerger.Merge(input).Count);
    }
}
