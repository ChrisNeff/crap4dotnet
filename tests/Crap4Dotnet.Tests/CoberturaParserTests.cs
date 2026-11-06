using System.Xml;
using Crap4Dotnet.Core;

namespace Crap4Dotnet.Tests;

public class CoberturaParserTests
{
    private static Stream Fixture() =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "fixtures", "sample.cobertura.xml"));

    [Fact]
    public void Parse_reads_methods_with_class_context()
    {
        var methods = CoberturaParser.Parse(Fixture());
        var bar = methods.Single(m => m.MethodName == "Bar");
        Assert.Equal("MyAssembly.Foo", bar.TypeName);
        Assert.Equal("(System.Int32)", bar.Signature);
        Assert.Equal("src/Foo.cs", bar.FileName);
        Assert.Equal(0.5, bar.LineRate);
        Assert.Equal(5, bar.Complexity);
    }

    [Fact]
    public void Parse_returns_null_for_missing_attributes()
    {
        var m = CoberturaParser.Parse(Fixture()).Single(x => x.MethodName == "NoComplexity");
        Assert.Null(m.Complexity);
        Assert.Equal(1.0, m.LineRate);
    }

    [Fact]
    public void Parse_throws_on_malformed_xml()
    {
        using var bad = new MemoryStream("<coverage><oops"u8.ToArray());
        Assert.Throws<XmlException>(() => CoberturaParser.Parse(bad));
    }
}
