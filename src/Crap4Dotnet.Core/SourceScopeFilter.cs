namespace Crap4Dotnet.Core;

public static class SourceScopeFilter
{
    public static IReadOnlyList<MethodCoverage> Apply(
        IReadOnlyList<MethodCoverage> methods,
        IReadOnlyCollection<string> targetPaths,
        IReadOnlyCollection<string>? changedFiles)
    {
        IEnumerable<MethodCoverage> result = methods;

        if (targetPaths.Count > 0)
        {
            var targets = targetPaths.Select(Segments).ToList();
            result = result.Where(m => targets.Any(t => IsContiguousSubsequence(Segments(m.FileName), t)));
        }

        if (changedFiles is not null)
        {
            var changed = changedFiles.Select(Segments).ToList();
            result = result.Where(m => changed.Any(c => IsContiguousSubsequence(Segments(m.FileName), c)));
        }

        return result.ToList();
    }

    private static string[] Segments(string p) =>
        p.Replace('\\', '/')
         .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
         .Where(s => s != ".")
         .Select(s => s.ToLowerInvariant())
         .ToArray();

    private static bool IsContiguousSubsequence(string[] file, string[] target)
    {
        if (target.Length == 0 || target.Length > file.Length) return false;
        for (int i = 0; i + target.Length <= file.Length; i++)
        {
            bool all = true;
            for (int j = 0; j < target.Length; j++)
                if (file[i + j] != target[j]) { all = false; break; }
            if (all) return true;
        }
        return false;
    }
}
