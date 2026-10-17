using System.Text.Json;
using OS.Tooling;

internal static class NativeAotSocketContainmentPublisherTests
{
    [HostTest("socket containment publisher preserves exact original compiler policy", HostLane.Fast, 0)]
    private static void OriginalRecipe()
    {
        string destination = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "containment-plan-only");
        string[] original = Original(destination);
        string[] plan = NativeAotSocketContainmentPublisher.PublishArguments(Json(original), destination);
        HostCheck.Equal("apps/NativeAotSocketContainment/NativeAotSocketContainment.csproj", plan[1]);
        HostCheck.Equal(Path.GetFullPath(destination), plan[Array.IndexOf(plan, "-o") + 1]);
        plan[1] = original[1]; HostCheck.True(plan.SequenceEqual(original));
        HostCheck.True(!Directory.Exists(destination));
    }

    [HostTest("socket containment publisher rejects changed pins flags and duplicate recipes", HostLane.Fast, 1)]
    private static void ChangedRecipes()
    {
        string destination = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "containment-invalid-plan-only");
        string[] original = Original(destination);
        foreach (string[] changed in new[] {
            original.Select(value => value == "-p:StaticExecutable=true" ? "-p:StaticExecutable=false" : value).ToArray(),
            original.Select(value => value == "-p:RuntimeFrameworkVersion=" + NativeAotPayload.RuntimeVersion ? "-p:RuntimeFrameworkVersion=11.0.0" : value).ToArray(),
            original.Select(value => value == "linux-musl-x64" ? "linux-x64" : value).ToArray(),
            original.Concat(["-p:NoWarn=IL2026"]).ToArray(), original.Concat(["-o", destination]).ToArray(),
            original.Where(value => value != "-p:UseSharedCompilation=false").ToArray() })
            Reject(Json(changed), destination);
        Reject(Json(original, original), destination);
        Reject("[]", destination);
        HostCheck.True(!Directory.Exists(destination));
    }

    [HostTest("socket containment publisher argument failure grants no publication", HostLane.Fast, 2)]
    private static void Arguments()
    {
        using StringWriter output = new();
        int result = NativeAotSocketContainmentPublisher.RunAsync(HostToolProcess.FindRepositoryRoot(), ["qualify"], output).GetAwaiter().GetResult();
        HostCheck.Equal(1, result);
        HostCheck.True(output.ToString().Contains("TEST_FAIL nativeaot.socket.containment.publish reason=socket_containment_publish_arguments", StringComparison.Ordinal) &&
            !output.ToString().Contains("TEST_PASS", StringComparison.Ordinal));
    }

    private static string[] Original(string destination)
    {
        string[] args = NativeAotThreadingPayload.PublishArguments("NativeAotSuspensionSmoke", destination).ToArray();
        args[1] = "apps/NativeAotSocketSmoke/NativeAotSocketSmoke.csproj"; return args;
    }

    private static string Json(params string[][] commands) => JsonSerializer.Serialize(commands.Select(args => new { executable = "dotnet", arguments = args }));

    private static void Reject(string recipe, string destination)
    {
        bool rejected = false;
        try { _ = NativeAotSocketContainmentPublisher.PublishArguments(recipe, destination); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
