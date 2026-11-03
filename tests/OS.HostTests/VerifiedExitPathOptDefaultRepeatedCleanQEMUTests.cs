using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static unsafe partial class VerifiedExitPathOptDefaultRepeatedCleanQEMUTests
{
    private static string Expected => "TEST_START m03.011\r\nCLROS_M03_011_READY\r\nTEST_PASS m03.011\r\n" + SerialSmokeMarkers.BootOk + "\r\nTEST_PASS boot.sequence\r\n" + SerialSmokeMarkers.OsSmokePass + "\r\n";

    [HostTest("m03.011 completion preserves the exact owned success tail", HostLane.Fast, 0)]
    private static void Completion()
    {
        Capture capture = default;
        capture.Limit = 256;
        Need(VerifiedExitPathOptDefaultRepeatedCleanQEMU.WriteCompletion(&capture, &Write), "complete writer");
        Need(Text(&capture) == Expected && capture.Calls == 6, "ordered exact CRLF records");
    }

    [HostTest("m03.011 every partial write stops before later success records", HostLane.Fast, 1)]
    private static void PartialWrites()
    {
        string expected = Expected;
        for (int stop = 0; stop < expected.Length; stop++)
        {
            Capture capture = default;
            capture.Limit = stop;
            Need(!VerifiedExitPathOptDefaultRepeatedCleanQEMU.WriteCompletion(&capture, &Write), "partial accepted at " + stop);
            Need(Text(&capture) == expected[..stop], "later output after failed byte " + stop);
            int expectedCalls = 1 + Regex.Matches(expected[..stop], "\r\n").Count;
            Need(capture.Calls == expectedCalls, "writer retried after failure at " + stop);
        }
    }

    [HostTest("m03.011 missing writer and host policy cannot emit success", HostLane.Fast, 2)]
    private static void MissingWriter()
    {
        Capture capture = default;
        capture.Limit = 256;
        Need(!VerifiedExitPathOptDefaultRepeatedCleanQEMU.WriteCompletion(null, &Write), "missing context");
        Need(!VerifiedExitPathOptDefaultRepeatedCleanQEMU.WriteCompletion(&capture, null), "missing writer");
        Need(!BootSequence.ReportVerifiedExitCompletion(&capture, &Write) && capture.Calls == 0, "managed host build is not the ordinary EFI policy");
        Need(!Probes.EnableM03FirstRealExitBootServicesProbe && !Probes.EnableM03OwnedSubsystemSurvivalProbe && !Probes.EnableM03OwnedSubsystemPanicProbe, "dangerous probes remain default-off");
    }

    [HostTest("m03.011 every diagnostic participates in default and conflict policy inventory", HostLane.Fast, 5)]
    private static void PolicyInventory()
    {
        XDocument xml = XDocument.Load(Path.Combine(HostToolProcess.FindRepositoryRoot(), "src/OS/OS.csproj"));
        XElement[] policy = [xml.Descendants("ClrosBootPolicy").Single(node => node.Value == "Diagnostic"), xml.Descendants("Error").Single(node => node.Attribute("Text")?.Value == "clros_boot_policy_probe_conflict")];
        string[] expected = OS.Tooling.EfiContracts.DiagnosticProbes.Order(StringComparer.Ordinal).ToArray();
        foreach (XElement node in policy)
        {
            string[] actual = Regex.Matches(node.Attribute("Condition")?.Value ?? "", @"\$\((Enable\w+Probe)\)").Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            Need(actual.SequenceEqual(expected), "complete diagnostic policy inventory missing=" + string.Join(',', expected.Except(actual)) + " extra=" + string.Join(',', actual.Except(expected)));
        }
    }

    [HostTest("m03.011 ordinary project policy is independent of diagnostic switches", HostLane.Native, 3)]
    private static void ProjectPolicy()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string project = Path.Combine(root, "src/OS/OS.csproj");
        XDocument xml = XDocument.Load(project);
        string[] probes = xml.Descendants("DefineConstants").Select(node => node.Parent?.Attribute("Condition")?.Value ?? "").SelectMany(condition => Regex.Matches(condition, @"\$\((Enable\w+Probe)\)").Select(match => match.Groups[1].Value)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Need(probes.Length == OS.Tooling.EfiContracts.DiagnosticProbes.Count, "review the complete diagnostic inventory before adding a new profile");
        using (JsonDocument ordinary = Evaluate(root, []))
        {
            JsonElement props = ordinary.RootElement.GetProperty("Properties");
            Need(props.GetProperty("ClrosBootPolicy").GetString() == "VerifiedExit", "ordinary boot exits");
            string symbols = props.GetProperty("DefineConstants").GetString()!;
            Need(symbols.Split(';').Contains("CLROS_NORMAL_VERIFIED_EXIT"), "ordinary compile admission");
            Need(!symbols.Split(';').Any(symbol => symbol.EndsWith("_PROBE", StringComparison.Ordinal)), "no probe enabled by policy");
        }

        foreach (string probe in probes)
        {
            using JsonDocument diagnostic = Evaluate(root, ["-p:" + probe + "=true"]);
            JsonElement props = diagnostic.RootElement.GetProperty("Properties");
            Need(props.GetProperty("ClrosBootPolicy").GetString() == "Diagnostic", "explicit profile retained: " + probe);
            Need(!props.GetProperty("DefineConstants").GetString()!.Split(';').Contains("CLROS_NORMAL_VERIFIED_EXIT"), "profile does not silently cross ordinary exit: " + probe);
            var conflict = MsBuild(root, ["-t:ValidateClrosBootPolicy", "-p:ClrosBootPolicy=VerifiedExit", "-p:" + probe + "=true"]);
            Need(conflict.Exit != 0 && conflict.Output.Contains("clros_boot_policy_probe_conflict", StringComparison.Ordinal), "mixed policy rejected: " + probe);
        }

        using (JsonDocument compatibility = Evaluate(root, ["-p:ClrosBootPolicy=Diagnostic"]))
            Need(!compatibility.RootElement.GetProperty("Properties").GetProperty("DefineConstants").GetString()!.Contains("CLROS_NORMAL_VERIFIED_EXIT", StringComparison.Ordinal), "explicit firmware-visible compatibility");
        var invalid = MsBuild(root, ["-t:ValidateClrosBootPolicy", "-p:ClrosBootPolicy=typo"]);
        Need(invalid.Exit != 0 && invalid.Output.Contains("clros_boot_policy_invalid", StringComparison.Ordinal), "unknown policy rejected");
    }

    private static JsonDocument Evaluate(string root, string[] properties)
    {
        var result = MsBuild(root, ["-getProperty:ClrosBootPolicy,DefineConstants", ..properties]);
        Need(result.Exit == 0, "project evaluation: " + result.Output);
        return JsonDocument.Parse(result.Output);
    }

    [HostTest("m03.011 CSharp commands reject policy conflicts and preserve complete default markers", HostLane.Native, 4)]
    private static void ScriptPolicy()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (string[] command in new string[][]
        {
            ["efi", "build"],
            ["efi", "verify"],
            ["qemu", "smoke"]
        }

        )
        {
            var invalid = InvokeTool(root, command, ["-BootPolicy", "Unknown"]);
            Need(invalid.Exit == 1 && invalid.Output.Contains("clros_boot_policy_invalid", StringComparison.Ordinal), string.Join(' ', command) + " invalid policy");
            var conflict = InvokeTool(root, command, ["-BootPolicy", "VerifiedExit", "-EnableM03FirstRealExitBootServicesProbe"]);
            Need(conflict.Exit == 1 && conflict.Output.Contains("clros_boot_policy_probe_conflict", StringComparison.Ordinal), string.Join(' ', command) + " mixed policy");
        }

        var ordinary = InvokeTool(root, ["qemu", "smoke"], ["-DryRun"]);
        Need(ordinary.Exit == 0 && ordinary.Output.Contains("QEMU_SCRIPT_BOOT_POLICY policy=VerifiedExit", StringComparison.Ordinal), "unqualified ordinary dry-run: " + ordinary.Output);
        string[] ordered = ordinary.Output.Split('\n').Single(line => line.StartsWith("ORDERED_MARKERS ", StringComparison.Ordinal)).TrimEnd('\r')["ORDERED_MARKERS ".Length..].Split(';');
        string[] expectedTail = Expected.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Need(ordered.SequenceEqual(OS.Tooling.QemuContracts.M03NormalVerifiedExitMarkers) && ordered.TakeLast(expectedTail.Length).SequenceEqual(expectedTail) && ordered.Distinct(StringComparer.Ordinal).Count() == ordered.Length, "complete unique post-exit marker ordering");
        foreach (string option in new[]
        {
            "-RequireMarker",
            "-RequireOrderedMarker",
            "-RequireUniqueMarker",
            "-ForbidMarker"
        }

        )
        {
            var rejected = InvokeTool(root, ["qemu", "smoke"], ["-DryRun", option, "OS_SMOKE_PASS"]);
            Need(rejected.Exit == 1 && rejected.Output.Contains("m03_normal_forbids_override", StringComparison.Ordinal), "closed marker contract " + option);
        }

        var reused = InvokeTool(root, ["qemu", "smoke"], ["-DryRun", "-SkipImageBuild"]);
        Need(reused.Exit == 1 && reused.Output.Contains("m03_normal_requires_original_capture", StringComparison.Ordinal), "normal image reuse rejected");
        var legacy = InvokeTool(root, ["qemu", "smoke"], ["-DryRun", "-BootPolicy", "Diagnostic"]);
        Need(legacy.Exit == 0 && legacy.Output.Contains("QEMU_SCRIPT_BOOT_POLICY policy=Diagnostic", StringComparison.Ordinal) && !legacy.Output.Contains("CLROS_M03_011_READY", StringComparison.Ordinal), "explicit diagnostic compatibility: " + legacy.Output);
    }

    private static (int Exit, string Output) InvokeTool(string root, string[] command, string[] arguments)
    {
        var result = HostToolProcess.RunTool([..command, ..arguments]);
        return (result.ExitCode, result.Output + result.Error);
    }

    private static (int Exit, string Output) MsBuild(string root, string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string argument in new[]
        {
            "msbuild",
            "src/OS/OS.csproj",
            "-nologo",
            "-v:quiet"
        }.Concat(arguments))
            start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(90_000))
        {
            process.Kill(entireProcessTree: true);
            throw new Exception("policy evaluation timeout");
        }

        string error = stderr.GetAwaiter().GetResult();
        Need(error.Length == 0, "unexpected stderr: " + error);
        return (process.ExitCode, stdout.GetAwaiter().GetResult());
    }

    private static bool Write(void* context, byte* text, uint bytes)
    {
        Capture* capture = (Capture*)context;
        capture->Calls++;
        Need(bytes <= 32, "bounded stack buffer");
        for (uint i = 0; i < bytes; i++)
        {
            if (capture->Count == capture->Limit)
                return false;
            capture->Bytes[capture->Count++] = text[i];
        }

        return true;
    }

    private static string Text(Capture* capture) => Encoding.ASCII.GetString(capture->Bytes, capture->Count);
    private static void Need(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
