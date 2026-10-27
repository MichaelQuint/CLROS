namespace OS.Tooling;

/// <summary>Publish the isolated static-file application using the original
/// static musl recipe. Publication supplies no process or CLROS guest authority.</summary>
public static class NativeAotStaticFilePublisher
{
    public const string Application = "MinimalApiStaticFiles";
    private static readonly NativeAotSocketFixturePublisher Publisher = new(Application, "static-files", [
        ("MinimalApiStaticFiles.csproj", "9A199F1D3E19D5341164DDDAA05B9FDF13080FDC54962B541B81A91DF5D54FE9"),
        ("Program.cs", "D5AFC6DDBD12CCA14CF0999F17F76B3E2B0992913742F7C73BC97414080543E6"),
        ("StaticFileApplication.cs", "9F5A8248D1188A694A1BC057BB174D69A0F04CE5BF8C82EFE7BB074FFB5DD774"),
        ("StaticFileConfigurationResponse.cs", "B87AB54AE90ACB87F812AFEC3D209C9E072F4D72D7CF0CCD7A7717556821F7DC"),
        ("StaticFileItemResponse.cs", "B080A648B24ECE79792FAE6D1E6B409580601CBD2357B71544F3FAC30DE77AF9"),
        ("StaticFileItemsResponse.cs", "409F9D720B33CCA9E15B5BDAE6555A5BAB7428AF342E79C62B2575E289EBA59A"),
        ("StaticFileJsonContext.cs", "3D388EDFBB2C7552D549D10CB0C953810CF12D923306A51A1E7283EDF9DCFDFF"),
        ("StaticFileStatusResponse.cs", "A735197E169246DDFE7703952C36CE039789F27E0E5AB0D18E719DCAE1C84161")]);

    /// <summary>Only application/output may differ from the original compiler recipe.</summary>
    public static string[] PublishArguments(string originalCommands, string destination) => Publisher.PublishArguments(originalCommands, destination);

    /// <summary>Check the fixed self-contained app source inventory and identities;
    /// missing, extra, redirected or modified inputs fail before publication.</summary>
    public static void RequireCurrentSources(string root) => Publisher.RequireCurrentSources(root);

    internal static string[] SourcePaths(string root) => ToolingBuildInputs.SourceInputs(root)
        .Concat(Directory.GetFiles(Path.Combine(root, "apps", Application)))
        .Distinct().Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Publish on the pinned Linux builder under fresh ownership,
    /// retaining exact source/native/output identities and all failed outputs.</summary>
    public static Task<int> RunAsync(string root, string[] args, TextWriter output) => Publisher.RunAsync(root, args, output);
}
