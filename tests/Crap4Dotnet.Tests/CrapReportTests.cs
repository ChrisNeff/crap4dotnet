using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class CrapReportTests
{
    [Fact]
    public void Build_orders_worst_first_with_na_last_and_counts_breaches()
    {
        var methods = new[]
        {
            new MethodCoverage("T", "Low", "()", "f.cs", 1.0, 1),   // crap 1
            new MethodCoverage("T", "High", "()", "f.cs", 0.0, 10),  // crap 110, breach
            new MethodCoverage("T", "Na", "()", "f.cs", null, 3),    // N/A
        };
        var report = CrapReport.Build(methods, threshold: 8.0);

        Assert.Equal("High", report.Ordered[0].MethodName);
        Assert.Equal("Low", report.Ordered[1].MethodName);
        Assert.Equal("Na", report.Ordered[2].MethodName);   // N/A sorts last
        Assert.Equal(3, report.Total);
        Assert.Equal(1, report.OverThreshold);
        Assert.Equal(8.0, report.Threshold);
    }
}
