namespace Crap4Dotnet.Core;

public static class CrapCalculator
{
    public static double Crap(int complexity, double coverage)
    {
        double c = Math.Clamp(coverage, 0.0, 1.0);
        double cc = complexity;
        return cc * cc * Math.Pow(1.0 - c, 3.0) + cc;
    }

    public static MethodCrap Score(MethodCoverage m, double threshold)
    {
        if (m.Complexity is int cc && m.LineRate is double cov)
        {
            double crap = Crap(cc, cov);
            return new MethodCrap(m.TypeName, m.MethodName, m.Signature, m.FileName,
                cc, cov, crap, crap > threshold);
        }
        return new MethodCrap(m.TypeName, m.MethodName, m.Signature, m.FileName,
            m.Complexity, m.LineRate, null, false);
    }
}
