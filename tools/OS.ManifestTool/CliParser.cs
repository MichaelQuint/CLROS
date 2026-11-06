using System.Globalization;
using System.Text.Json;
using OS.Kernel.Build;

internal static class CliParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return CliParseResult.NotValidate();
        }

        if (args.Length == 1 && IsHelp(args[0]))
        {
            return CliParseResult.Help();
        }

        if (!string.Equals(args[0], "validate", StringComparison.Ordinal))
        {
            return CliParseResult.NotValidate();
        }

        string? manifestPath = null;
        string profileName = WorkloadProfile.SelectedName;
        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];
            if (IsHelp(argument))
            {
                return CliParseResult.Help();
            }

            if (string.Equals(argument, "--manifest", StringComparison.Ordinal))
            {
                if (manifestPath is not null)
                {
                    return CliParseResult.InvalidValidate("Duplicate --manifest argument.");
                }

                if (index + 1 >= args.Length)
                {
                    return CliParseResult.InvalidValidate("Missing value for --manifest.");
                }

                index++;
                manifestPath = args[index];
                continue;
            }

            if (string.Equals(argument, "--profile", StringComparison.Ordinal))
            {
                if (index + 1 >= args.Length)
                {
                    return CliParseResult.InvalidValidate("Missing value for --profile.");
                }

                index++;
                profileName = args[index];
                continue;
            }

            return CliParseResult.InvalidValidate("Unknown argument.");
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return CliParseResult.InvalidValidate("Manifest path is required.");
        }

        return CliParseResult.ValidValidate(manifestPath, profileName);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("OS.ManifestTool validate --manifest <path> [--profile <SingleMinimalApi|MultiMinimalApi>]");
        Console.WriteLine("OS.ManifestTool validate-kestrel-package --manifest <path> --package-root <dir> [--profile <SingleMinimalApi|MultiMinimalApi>] [--contract kestrel.smoke.package.v0]");
    }

    private static bool IsHelp(string argument)
    {
        return string.Equals(argument, "--help", StringComparison.Ordinal) || string.Equals(argument, "-h", StringComparison.Ordinal);
    }
}
