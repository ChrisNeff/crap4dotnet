using System.Globalization;
using System.Xml.Linq;

namespace Crap4Dotnet.Core;

public static class CoberturaParser
{
    public static IReadOnlyList<MethodCoverage> Parse(Stream xml)
    {
        var doc = XDocument.Load(xml);
        var methods = new List<MethodCoverage>();
        foreach (var cls in doc.Descendants("class"))
        {
            string typeName = (string?)cls.Attribute("name") ?? "";
            string fileName = (string?)cls.Attribute("filename") ?? "";
            foreach (var m in cls.Descendants("method"))
            {
                methods.Add(new MethodCoverage(
                    typeName,
                    (string?)m.Attribute("name") ?? "",
                    (string?)m.Attribute("signature") ?? "",
                    fileName,
                    ParseDouble(m.Attribute("line-rate")),
                    ParseInt(m.Attribute("complexity"))));
            }
        }
        return methods;
    }

    private static double? ParseDouble(XAttribute? a) =>
        a is not null && double.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : null;

    private static int? ParseInt(XAttribute? a) =>
        a is not null && int.TryParse(a.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v : null;
}
