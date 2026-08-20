using Crap4Dotnet.Cli;
using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class TextReportRendererTests
{
    [Fact]
    public void Render_marks_offenders_shows_na_and_footer()
    {
        var report = CrapReport.Build(new[]
        {
            new MethodCoverage("T", "High", "()", "f.cs", 0.0, 10),
            new MethodCoverage("T", "Na", "()", "f.cs", null, 3),
        }, 8.0);

        var text = TextReportRenderer.Render(report);

        Assert.Contains("* T.High()", text);       // offender flagged
        Assert.Contains("110.00", text);            // crap value
        Assert.Contains("N/A", text);               // N/A method
        Assert.Contains("2 methods", text);
        Assert.Contains("1 over 8.0", text);
    }
}
