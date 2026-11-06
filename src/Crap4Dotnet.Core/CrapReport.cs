namespace Crap4Dotnet.Core;

public sealed record CrapReport(
    IReadOnlyList<MethodCrap> Ordered,
    double Threshold,
    int Total,
    int OverThreshold)
{
    public static CrapReport Build(IReadOnlyList<MethodCoverage> methods, double threshold)
    {
        var scored = methods.Select(m => CrapCalculator.Score(m, threshold)).ToList();
        var ordered = scored
            .OrderByDescending(x => x.Crap.HasValue)
            .ThenByDescending(x => x.Crap ?? 0.0)
            .ToList();
        return new CrapReport(ordered, threshold, scored.Count, scored.Count(x => x.IsCrappy));
    }
}
