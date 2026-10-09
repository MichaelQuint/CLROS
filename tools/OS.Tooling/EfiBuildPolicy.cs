namespace OS.Tooling;

/// <summary>One reviewed set of public process bounds for build and preflight.</summary>
public static class EfiBuildPolicy
{
    public const int PostRestoreEvaluationTimeoutSeconds = 120;
    public const int BuildTimeoutSeconds = 300, MinimumBuildTimeoutSeconds = 5, MaximumBuildTimeoutSeconds = 3600;
    public const int VerifierTimeoutSeconds = 60, MinimumVerifierTimeoutSeconds = 5, MaximumVerifierTimeoutSeconds = 300;
    public const int OutputCharacters = 4194304, MinimumOutputCharacters = 4096, MaximumOutputCharacters = 67108864;
    public const int VersionTimeoutMilliseconds = 5000, MinimumVersionTimeoutMilliseconds = 100, MaximumVersionTimeoutMilliseconds = 60000;
    public const string LogPath = "artifacts/logs/efi-shell-publish.log";
    public static int BuildTimeout(EfiOptions options) => options.Number("BuildProcessTimeoutSeconds", BuildTimeoutSeconds, MinimumBuildTimeoutSeconds, MaximumBuildTimeoutSeconds);
    public static int VerifierTimeout(EfiOptions options) => options.Number("ArtifactVerifierTimeoutSeconds", VerifierTimeoutSeconds, MinimumVerifierTimeoutSeconds, MaximumVerifierTimeoutSeconds);
    public static int OutputLimit(EfiOptions options) => options.Number("BuildProcessMaximumOutputCharacters", OutputCharacters, MinimumOutputCharacters, MaximumOutputCharacters);
    public static int VersionTimeout(EfiOptions options) => options.Number("VersionProbeTimeoutMilliseconds", VersionTimeoutMilliseconds, MinimumVersionTimeoutMilliseconds, MaximumVersionTimeoutMilliseconds);
}
