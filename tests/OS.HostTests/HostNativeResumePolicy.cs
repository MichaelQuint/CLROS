using System.Text.RegularExpressions;
using OS.Tooling;

// Only reviewed publishers that create every artifact they read belong here.
// This registry does not confer guest/native acceptance on a host-test receipt.
internal static class HostNativeResumePolicy
{
    internal static bool OwnsOutputs(HostTestRunner.Fixture[] fixtures) =>
        fixtures[0].Method.DeclaringType == typeof(M02OwnedIdtGatePopulationEfiIntegrationTests) ||
        fixtures[0].Method.DeclaringType == typeof(M02IdtrContinuedExecutionEfiIntegrationTests) &&
        fixtures.Length == 1 && fixtures[0].Method.Name == "TestDefaultAndEnabledEfiArtifacts";

    internal static IEnumerable<string> Outputs(string root, HostTestRunner.Fixture[] fixtures, HostTestRunner.FixtureResult result)
    {
        if (!OwnsOutputs(fixtures) || !fixtures.Any(f => f.Lane == HostLane.Native) || !HostRunCheckpoint.Passed(result)) yield break;
        if (result.ProcessId <= 0) throw new InvalidOperationException("checkpoint_worker_identity_missing");
        string number = fixtures[0].Method.DeclaringType == typeof(M02OwnedIdtGatePopulationEfiIntegrationTests) ? "011" : "012";
        foreach (string variant in new[] { "DEFAULT", "ENABLED" })
        {
            string artifact = Path.Combine(root, "artifacts", "efi", $"BOOTX64-M02-{number}-HOSTTEST-{result.ProcessId}-{variant}.EFI");
            foreach (string suffix in new[] { "", ".link.map", ".link.rsp", ".link.json", ".serializer.obj" }) yield return artifact + suffix;
            yield return Path.Combine(root, "artifacts", "logs", $"m02-{number}-hosttest-{result.ProcessId}-{variant.ToLowerInvariant()}.log");
        }
    }

    internal static string? InputFingerprint(string root)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            List<string> discovery = [];
            var tools = EfiPreflight.Inspect(EfiOptions.Parse(["--root", root], build: true), discovery.Add);
            HashSet<string> files = new(StringComparer.OrdinalIgnoreCase)
            { tools.DotnetHostPath, tools.LinkerPath, tools.WindowsSdkLibraryPath, tools.IlCompilerDiscoverySourcePath, HostToolProcess.FindLlvmObjdump() };
            foreach (string line in discovery)
                foreach (Match match in Regex.Matches(line, "\\bpath=\"([^\"]+)\""))
                    if (File.Exists(match.Groups[1].Value)) files.Add(match.Groups[1].Value);
            // Actual SDK/linker support bytes and package archives, not only version
            // strings. The publisher restores packages into fresh isolated output.
            string global = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is string custom && !string.IsNullOrWhiteSpace(custom)
                ? custom : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
            foreach (string directory in new[] { tools.DotnetSdkDirectory, Path.GetDirectoryName(tools.LinkerPath)!,
                Path.GetDirectoryName(typeof(object).Assembly.Location)!, tools.NativeAotRuntimePackPath }
                .Concat(EfiContracts.RestoreArchives.Select(a => Path.Combine(global, a.Id, a.Version))))
                foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) files.Add(file);
            return HostRunCheckpoint.HashText(string.Join("\n", discovery) + "\n" + string.Join("\n",
                files.Order(StringComparer.Ordinal).Select(file => file + ":" + HostRunCheckpoint.HashFile(file))));
        }
        catch (Exception error) when (error is ToolFailure or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Discovery failure disables reuse; the actual fixture remains selected
            // and reports its ordinary tool/prerequisite failure when executed.
            return null;
        }
    }
}
