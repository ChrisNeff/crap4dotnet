namespace Crap4Dotnet.Cli;

public static class GitChangedFilesProvider
{
    public static IReadOnlyList<string> GetChangedFiles(string workingDir)
    {
        var modified = Lines(ProcessRunner.Run("git", "diff --name-only HEAD", workingDir).StdOut);
        var untracked = Lines(ProcessRunner.Run("git", "ls-files --others --exclude-standard", workingDir).StdOut);
        return modified.Concat(untracked).Distinct().ToList();
    }

    private static IEnumerable<string> Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
