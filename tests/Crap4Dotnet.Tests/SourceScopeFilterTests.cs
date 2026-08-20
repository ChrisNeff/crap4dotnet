// tests/Crap4Dotnet.Tests/SourceScopeFilterTests.cs
using Crap4Dotnet.Core;
namespace Crap4Dotnet.Tests;

public class SourceScopeFilterTests
{
    private static MethodCoverage M(string file) => new("T", "M", "()", file, 0.5, 3);
    private static readonly IReadOnlyList<MethodCoverage> Methods =
    [
        M("/repo/src/Foo.cs"),
        M("src/Bar/Baz.cs"),
        M(@"C:\repo\src\Qux.cs"),
    ];

    [Fact]
    public void No_targets_no_changed_returns_all()
    {
        Assert.Equal(3, SourceScopeFilter.Apply(Methods, [], null).Count);
    }

    [Fact]
    public void File_target_matches_regardless_of_absolute_or_relative()
    {
        var r = SourceScopeFilter.Apply(Methods, ["src/Foo.cs"], null);
        Assert.Single(r);
        Assert.Equal("/repo/src/Foo.cs", r[0].FileName);
    }

    [Fact]
    public void Directory_target_matches_files_under_it()
    {
        var r = SourceScopeFilter.Apply(Methods, ["src/Bar"], null);
        Assert.Single(r);
        Assert.Equal("src/Bar/Baz.cs", r[0].FileName);
    }

    [Fact]
    public void Backslash_paths_normalize()
    {
        var r = SourceScopeFilter.Apply(Methods, ["src/Qux.cs"], null);
        Assert.Single(r);
    }

    [Fact]
    public void Changed_and_targets_intersect()
    {
        var r = SourceScopeFilter.Apply(
            Methods,
            targetPaths: ["src"],
            changedFiles: ["src/Foo.cs"]);
        Assert.Single(r);
        Assert.Equal("/repo/src/Foo.cs", r[0].FileName);
    }
}
