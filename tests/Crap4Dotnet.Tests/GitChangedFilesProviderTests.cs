using Crap4Dotnet.Cli;
namespace Crap4Dotnet.Tests;

public class GitChangedFilesProviderTests
{
    [Fact]
    public void GetChangedFiles_includes_untracked_and_modified()
    {
        string dir = Path.Combine(Path.GetTempPath(), "crap4dotnet-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            ProcessRunner.Run("git", "init -q", dir);
            ProcessRunner.Run("git", "config user.email t@t.t", dir);
            ProcessRunner.Run("git", "config user.name t", dir);
            File.WriteAllText(Path.Combine(dir, "committed.cs"), "// v1");
            ProcessRunner.Run("git", "add -A", dir);
            ProcessRunner.Run("git", "commit -q -m init", dir);

            File.WriteAllText(Path.Combine(dir, "committed.cs"), "// v2");   // modified
            File.WriteAllText(Path.Combine(dir, "untracked.cs"), "// new"); // untracked

            var changed = GitChangedFilesProvider.GetChangedFiles(dir);
            Assert.Contains("committed.cs", changed);
            Assert.Contains("untracked.cs", changed);
        }
        finally { ForceDelete(dir); }
    }

    private static void ForceDelete(string dir)
    {
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(dir, recursive: true);
    }
}
