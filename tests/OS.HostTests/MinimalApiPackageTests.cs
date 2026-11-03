using System.Text.Json;
using OS.Tooling;

internal static class MinimalApiPackageTests
{
    private static string Scratch()
    {
        string path = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "kestrel-port-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static ProcessResult Result(int exit = 0, string text = "", bool timeout = false) => new(exit, text, "", timeout, false, false, TimeSpan.Zero);

    [HostTest("kestrel publisher rejects unsupported profiles before starting processes")]
    private static void Profile()
    {
        using StringWriter output = new();
        int calls = 0;
        int result = MinimalApiPackage.RunAsync(HostToolProcess.FindRepositoryRoot(), new()
        {
            WorkloadProfile = "MultiMinimalApi"
        }, output,
            _ =>
{
    calls++;
    return Task.FromResult(Result());
}).GetAwaiter().GetResult();
        HostCheck.Equal(1, result);
        HostCheck.Equal(0, calls);
        HostCheck.True(output.ToString().Contains("TEST_FAIL workload.profile.tooling", StringComparison.Ordinal));
    }

    [HostTest("kestrel publisher requires successful bounded publish and complete output files")]
    private static void FailedPublish()
    {
        foreach (var process in new[] { Result(23), Result(timeout: true), Result() })
        {
            using StringWriter output = new();
            int calls = 0;
            int result = MinimalApiPackage.RunAsync(HostToolProcess.FindRepositoryRoot(), new()
            {
                OutputRoot = Scratch()
            }, output,
                _ =>
{
    calls++;
    return Task.FromResult(process);
}).GetAwaiter().GetResult();
            HostCheck.True(result != 0);
            HostCheck.Equal(1, calls);
            HostCheck.True(!output.ToString().Split('\n').Any(line => line.TrimEnd('\r') == "TEST_PASS kestrel.smoke_app"));
        }
    }

    [HostTest("kestrel publisher writes exact manifest and requires the expected negative rejection")]
    private static void ManifestAndValidation()
    {
        foreach (var negative in new[] { Result(1, "kestrel_package_missing_expected_env"), Result(), Result(1, "different_failure"), Result(1, "kestrel_package_missing_expected_env", true),
            Result(1, "kestrel_package_missing_expected_env") with { TerminationConfirmed = false },
            Result(1, "kestrel_package_missing_expected_env") with { FailureReason = "CaptureFailed" } })
        {
            string folder = Scratch();
            using StringWriter output = new();
            int calls = 0;
            Task<ProcessResult> Invoke(ProcessRequest request)
            {
                calls++;
                HostCheck.Equal("dotnet", request.Executable);
                if (request.Arguments[0] == "publish")
                {
                    string destination = request.Arguments[^1];
                    foreach (string name in MinimalApiPackage.RequiredFiles)
                        File.WriteAllText(Path.Combine(destination, name), "controlled file");
                    return Task.FromResult(Result());
                }
                HostCheck.True(request.Arguments[0].EndsWith("OS.ManifestTool.dll", StringComparison.Ordinal));
                int manifestIndex = request.Arguments.ToList().IndexOf("--manifest") + 1;
                using var manifest = JsonDocument.Parse(File.ReadAllText(request.Arguments[manifestIndex]));
                HostCheck.Equal("SingleMinimalApi", manifest.RootElement.GetProperty("workloadProfile").GetString());
                HostCheck.Equal("apps/minimal-api/MinimalApiSmoke.dll", manifest.RootElement.GetProperty("runtime").GetProperty("assembly").GetString());
                bool hasExpected = manifest.RootElement.GetProperty("env").TryGetProperty("OS_SMOKE_EXPECTED_BODY", out var expected);
                HostCheck.Equal(calls == 2, hasExpected);
                if (hasExpected)
                    HostCheck.Equal("OS_MINIMAL_API_OK", expected.GetString());
                return Task.FromResult(calls == 2 ? Result() : negative);
            }
            int result = MinimalApiPackage.RunAsync(HostToolProcess.FindRepositoryRoot(), new()
            {
                OutputRoot = folder
            }, output, Invoke).GetAwaiter().GetResult();
            HostCheck.Equal(negative.ExitCode != 0 && !negative.TimedOut && negative.TerminationConfirmed && negative.FailureReason.Length == 0 && negative.StandardOutput == "kestrel_package_missing_expected_env" ? 0 : 1, result);
            HostCheck.Equal(3, calls);
            HostCheck.True(!File.ReadAllBytes(Path.Combine(folder, "package.json")).AsSpan().StartsWith(new byte[] { 239, 187, 191 }));
        }
    }
}
