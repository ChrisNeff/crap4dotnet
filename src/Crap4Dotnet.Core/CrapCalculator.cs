namespace Crap4Dotnet.Core;

public static class CrapCalculator
{
    public static double Crap(int complexity, double coverage)
    {
        double c = Math.Clamp(coverage, 0.0, 1.0);
        double cc = complexity;
        return cc * cc * Math.Pow(1.0 - c, 3.0) + cc;
    }
}
