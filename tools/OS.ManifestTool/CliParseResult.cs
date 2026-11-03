using System.Globalization;
using System.Text.Json;
using OS.Kernel.Build;

internal sealed class CliParseResult
{
    private CliParseResult(bool shouldValidate, bool showHelp, bool succeeded, string manifestPath, string profileName, string errorMessage)
    {
        this.ShouldValidate = shouldValidate;
        this.ShowHelp = showHelp;
        this.Succeeded = succeeded;
        this.ManifestPath = manifestPath;
        this.ProfileName = profileName;
        this.ErrorMessage = errorMessage;
    }

    public bool ShouldValidate
    {
        get;
    }
    public bool ShowHelp
    {
        get;
    }
    public bool Succeeded
    {
        get;
    }
    public string ManifestPath
    {
        get;
    }
    public string ProfileName
    {
        get;
    }
    public string ErrorMessage
    {
        get;
    }

    public static CliParseResult Help()
    {
        return new CliParseResult(false, true, true, string.Empty, WorkloadProfile.SelectedName, string.Empty);
    }

    public static CliParseResult NotValidate()
    {
        return new CliParseResult(false, false, false, string.Empty, WorkloadProfile.SelectedName, string.Empty);
    }

    public static CliParseResult ValidValidate(string manifestPath, string profileName)
    {
        return new CliParseResult(true, false, true, manifestPath, profileName, string.Empty);
    }

    public static CliParseResult InvalidValidate(string errorMessage)
    {
        return new CliParseResult(true, false, false, string.Empty, WorkloadProfile.SelectedName, errorMessage);
    }
}
