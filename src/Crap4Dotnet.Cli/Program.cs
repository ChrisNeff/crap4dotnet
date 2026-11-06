using Crap4Dotnet.Core;

namespace Crap4Dotnet.Cli;

public static class Program
{
    private const double Threshold = 8.0;

    private const string HelpText =
        """
        crap4dotnet — CRAP score per method (CC² × (1 − coverage)³ + CC)

        Usage:
          crap4dotnet [targets...]         run tests, score all (or matching) methods
          crap4dotnet --changed            score only git-modified files
          crap4dotnet --coverage <file>    score an existing Cobertura XML (skip tests)
          crap4dotnet --help               show this help

        Exit codes: 0 clean · 1 error · 2 threshold (8.0) exceeded
        """;

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    public static int Run(string[] args, TextWriter outw, TextWriter errw)
    {
        var opts = CliOptions.Parse(args);
        if (opts.Error is not null) { errw.WriteLine(opts.Error); return ExitCodes.Error; }
        if (opts.Help) { outw.WriteLine(HelpText); return ExitCodes.Ok; }

        string cwd = Directory.GetCurrentDirectory();

        try
        {
            IReadOnlyList<string> files;
            if (opts.CoverageFile is not null)
            {
                if (!File.Exists(opts.CoverageFile))
                {
                    errw.WriteLine($"coverage file not found: {opts.CoverageFile}");
                    return ExitCodes.Error;
                }
                files = [opts.CoverageFile];
            }
            else
            {
                var run = DotnetTestRunner.Run(cwd);
                if (run.ExitCode != 0)
                {
                    errw.WriteLine(run.StdErr.Length > 0 ? run.StdErr : "dotnet test failed");
                    return ExitCodes.Error;
                }
                files = CoberturaLocator.Locate(run.ResultsDir);
                if (files.Count == 0) { errw.WriteLine("no coverage produced"); return ExitCodes.Error; }
            }

            var parsed = new List<MethodCoverage>();
            foreach (var f in files)
            {
                try
                {
                    using var s = File.OpenRead(f);
                    parsed.AddRange(CoberturaParser.Parse(s));
                }
                catch (Exception ex)
                {
                    errw.WriteLine($"failed to parse {f}: {ex.Message}");
                    return ExitCodes.Error;
                }
            }

            var merged = CoverageMerger.Merge(parsed);
            var changed = opts.Changed ? GitChangedFilesProvider.GetChangedFiles(cwd) : null;
            var scoped = SourceScopeFilter.Apply(merged, opts.Targets, changed);
            var report = CrapReport.Build(scoped, Threshold);

            outw.Write(TextReportRenderer.Render(report));
            return report.OverThreshold > 0 ? ExitCodes.ThresholdExceeded : ExitCodes.Ok;
        }
        catch (Exception ex)
        {
            errw.WriteLine(ex.Message);
            return ExitCodes.Error;
        }
    }
}
