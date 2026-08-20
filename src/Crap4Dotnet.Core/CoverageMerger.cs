namespace Crap4Dotnet.Core;

public static class CoverageMerger
{
    public static IReadOnlyList<MethodCoverage> Merge(IEnumerable<MethodCoverage> methods) =>
        methods
            .GroupBy(m => (m.TypeName, m.MethodName, m.Signature))
            .Select(g => new MethodCoverage(
                g.Key.TypeName,
                g.Key.MethodName,
                g.Key.Signature,
                g.First().FileName,
                g.Max(x => x.LineRate),
                g.Select(x => x.Complexity).FirstOrDefault(x => x is not null) ?? g.First().Complexity))
            .ToList();
}
