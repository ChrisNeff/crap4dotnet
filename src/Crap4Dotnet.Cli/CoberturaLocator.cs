namespace Crap4Dotnet.Cli;

public static class CoberturaLocator
{
    public static IReadOnlyList<string> Locate(string resultsDir) =>
        Directory.Exists(resultsDir)
            ? Directory.GetFiles(resultsDir, "coverage.cobertura.xml", SearchOption.AllDirectories)
            : [];
}
