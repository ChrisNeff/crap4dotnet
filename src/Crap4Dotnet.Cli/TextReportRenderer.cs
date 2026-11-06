using System.Globalization;
using System.Text;
using Crap4Dotnet.Core;

namespace Crap4Dotnet.Cli;

public static class TextReportRenderer
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Render(CrapReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"  {"Method",-50} {"CC",4} {"Coverage",9} {"CRAP",8}");

        foreach (var m in report.Ordered)
        {
            string flag = m.IsCrappy ? "*" : " ";
            string name = $"{m.TypeName}.{m.MethodName}{m.Signature}";
            string cc = m.Cc?.ToString(Inv) ?? "N/A";
            string cov = m.Coverage is double c ? string.Format(Inv, "{0:0.0}%", c * 100) : "N/A";
            string crap = m.Crap is double cr ? cr.ToString("0.00", Inv) : "N/A";
            sb.AppendLine($"{flag} {Truncate(name, 50),-50} {cc,4} {cov,9} {crap,8}");
        }

        sb.AppendLine();
        sb.AppendLine(string.Format(Inv, "{0} methods · {1} over {2:0.0}",
            report.Total, report.OverThreshold, report.Threshold));
        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : "…" + s[^(max - 1)..];
}
