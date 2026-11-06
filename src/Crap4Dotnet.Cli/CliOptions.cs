namespace Crap4Dotnet.Cli;

public sealed record CliOptions(
    bool Help,
    string? CoverageFile,
    bool Changed,
    IReadOnlyList<string> Targets,
    string? Error)
{
    public static CliOptions Parse(string[] args)
    {
        bool help = false, changed = false;
        string? coverage = null;
        var targets = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    help = true;
                    break;
                case "--changed":
                    changed = true;
                    break;
                case "--coverage":
                    if (i + 1 >= args.Length)
                        return new CliOptions(false, null, false, [], "--coverage requires a file path");
                    coverage = args[++i];
                    break;
                default:
                    if (args[i].StartsWith('-'))
                        return new CliOptions(false, null, false, [], $"unknown option: {args[i]}");
                    targets.Add(args[i]);
                    break;
            }
        }

        return new CliOptions(help, coverage, changed, targets, null);
    }
}
