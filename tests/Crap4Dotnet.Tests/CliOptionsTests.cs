using Crap4Dotnet.Cli;

namespace Crap4Dotnet.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Parse_help_flag()
    {
        Assert.True(CliOptions.Parse(["--help"]).Help);
    }

    [Fact]
    public void Parse_coverage_and_targets_and_changed()
    {
        var o = CliOptions.Parse(["--coverage", "cov.xml", "src/Foo.cs", "--changed", "src/Bar"]);
        Assert.Equal("cov.xml", o.CoverageFile);
        Assert.True(o.Changed);
        Assert.Equal(new[] { "src/Foo.cs", "src/Bar" }, o.Targets);
        Assert.Null(o.Error);
    }

    [Fact]
    public void Parse_coverage_without_value_is_error()
    {
        Assert.NotNull(CliOptions.Parse(["--coverage"]).Error);
    }

    [Fact]
    public void Parse_unknown_flag_is_error()
    {
        Assert.NotNull(CliOptions.Parse(["--nope"]).Error);
    }
}
