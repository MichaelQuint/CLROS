namespace OS.Tooling;

/// <summary>Publish the fixed continuing-service fixture through the accepted
/// static musl compiler recipe and shared publication owner. Separate source
/// pins preserve the original web producer. No service or guest executes here.</summary>
public static class NativeAotServicePublisher
{
    public const string Application = NativeAotServicePackage.Application;
    private static readonly NativeAotSocketFixturePublisher Publisher = new(Application, "service", [
        ("Program.cs", "EED32C37A92678411540591673389B30EF50F406BE7AD37AAC385D4801F00DBC"),
        ("ServiceLifecycleExercise.cs", "288045DF1F807D89BA96D3D6F953B9ED10ED06CF11A9D8C4523CB148B41D433E"),
        ("ServiceExercise.cs", "971B8D504D3EA66A28576A89E221C9EB25E1D161BE2DAE4E150C06F15929EC80"),
        ("appsettings.json", "B2F356A9CD23F432F5E19AD6AD72844E5C5331587DAB81C67722016CE6447AB5"),
        (Application + ".csproj", "0B267A2A4EE1B931BF1CAD5DC025356345EE74AE8463D1A16B8C27A8D6C5B5D7")]);

    /// <summary>Require the exact original compiler command; substitute only
    /// application/output. Returned fresh arguments grant planning authority,
    /// not publication or execution authority. Changed/duplicate recipes throw.</summary>
    public static string[] PublishArguments(string originalCommands, string destination) => Publisher.PublishArguments(originalCommands, destination);

    /// <summary>Read-only check of five exact app files and absent policy/source
    /// overrides. Caller retains plain stable files; bin/obj are excluded.
    /// Source identity alone grants neither publication nor executable authority.</summary>
    public static void RequireCurrentSources(string root) => Publisher.RequireCurrentSources(root);

    /// <summary>Linux-only publish, with fresh app/output/cache, two source leases,
    /// pinned SDK/runtime/tools/recipe and retained source/native/output failures.
    /// Borrows arguments/output until completion. Owns no process after return.
    /// Shared-engine markers/schema use the existing socket-service prefix.</summary>
    public static Task<int> RunAsync(string root, string[] args, TextWriter output) => Publisher.RunAsync(root, args, output);
}
