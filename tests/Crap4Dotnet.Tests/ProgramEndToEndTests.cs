using Crap4Dotnet.Cli;
namespace Crap4Dotnet.Tests;

public class ProgramEndToEndTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.cobertura.xml");

    [Fact]
    public void Coverage_path_scores_fixture_and_exits_two_when_over_threshold()
    {
        var outw = new StringWriter();
        var errw = new StringWriter();
        // sample fixture: Bar cc5/cov0.5 -> 8.125 (>8). So expect ThresholdExceeded.
        int code = Program.Run(["--coverage", Fixture], outw, errw);

        Assert.Equal(ExitCodes.ThresholdExceeded, code);
        Assert.Contains("MyAssembly.Foo.Bar", outw.ToString());
        Assert.Contains("over 8.0", outw.ToString());
    }

    [Fact]
    public void Missing_coverage_file_is_error()
    {
        int code = Program.Run(["--coverage", "nope.xml"], new StringWriter(), new StringWriter());
        Assert.Equal(ExitCodes.Error, code);
    }

    [Fact]
    public void Help_exits_zero()
    {
        Assert.Equal(ExitCodes.Ok, Program.Run(["--help"], new StringWriter(), new StringWriter()));
    }

    [Fact]
    public void Target_filter_limits_scope()
    {
        var outw = new StringWriter();
        int code = Program.Run(["--coverage", Fixture, "does/not/match.cs"], outw, new StringWriter());
        Assert.Equal(ExitCodes.Ok, code);           // nothing matched -> no breaches
        Assert.Contains("0 methods", outw.ToString());
    }
}
