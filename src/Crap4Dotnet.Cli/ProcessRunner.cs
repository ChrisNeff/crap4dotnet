using System.Diagnostics;

namespace Crap4Dotnet.Cli;

public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

public static class ProcessRunner
{
    public static ProcessResult Run(string file, string args, string workingDir)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        Task<string> stdoutTask = p.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();
        return new ProcessResult(p.ExitCode, stdout, stderr);
    }
}
