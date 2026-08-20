using Crap4Dotnet.Cli;
namespace Crap4Dotnet.Tests;

public class CoberturaLocatorTests
{
    [Fact]
    public void Locate_finds_files_in_guid_subfolders()
    {
        string root = Path.Combine(Path.GetTempPath(), "crap4dotnet-test-" + Guid.NewGuid().ToString("N"));
        string sub = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "coverage.cobertura.xml"), "<coverage/>");
        try
        {
            var found = CoberturaLocator.Locate(root);
            Assert.Single(found);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Locate_returns_empty_for_missing_dir()
    {
        Assert.Empty(CoberturaLocator.Locate(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid())));
    }
}
