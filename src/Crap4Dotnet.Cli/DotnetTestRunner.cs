namespace Crap4Dotnet.Cli;

public sealed record TestRunResult(int ExitCode, string ResultsDir, string StdErr);

public static class DotnetTestRunner
{
    public static TestRunResult Run(string workingDir)
    {
        string resultsDir = Path.Combine(Path.GetTempPath(), "crap4dotnet", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(resultsDir);
        var r = ProcessRunner.Run(
            "dotnet",
            $"test --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDir}\"",
            workingDir);
        return new TestRunResult(r.ExitCode, resultsDir, r.StdErr);
    }
}
