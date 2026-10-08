using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static unsafe class VerifiedExitPathOptDefaultRepeatedCleanQEMUTests
{
    private struct Capture
    {
        public fixed byte Bytes[256];
        public int Count, Limit, Calls;
    }

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("completion preserves the exact owned success tail", Completion);
        Run("every partial write stops before later success records", PartialWrites);
        Run("missing writer and host policy cannot emit success", MissingWriter);
        Run("ordinary project policy is independent of diagnostic switches", ProjectPolicy);
        Run("entry scripts reject policy conflicts and preserve complete default markers", ScriptPolicy);
    }

    private static string Expected => "TEST_START m03.011\r\nCLROS_M03_011_READY\r\nTEST_PASS m03.011\r\n" +
        SerialSmokeMarkers.BootOk + "\r\nTEST_PASS boot.sequence\r\n" + SerialSmokeMarkers.OsSmokePass + "\r\n";

    private static void Completion()
    {
        Capture capture = default;
        capture.Limit = 256;
        Need(VerifiedExitPathOptDefaultRepeatedCleanQEMU.WriteCompletion(&capture, &Write), "complete writer");
        Need(Text(&capture) == Expected && capture.Calls == 6, "ordered exact CRLF records");
    }

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

    private static void MissingWriter()
    {
        Capture capture = default;
        capture.Limit = 256;
        Need(!VerifiedExitPathOptDefaultRepeatedCleanQEMU.WriteCompletion(null, &Write), "missing context");
        Need(!VerifiedExitPathOptDefaultRepeatedCleanQEMU.WriteCompletion(&capture, null), "missing writer");
        Need(!BootSequence.ReportVerifiedExitCompletion(&capture, &Write) && capture.Calls == 0,
            "managed host build is not the ordinary EFI policy");
        Need(!Probes.EnableM03FirstRealExitBootServicesProbe && !Probes.EnableM03OwnedSubsystemSurvivalProbe &&
            !Probes.EnableM03OwnedSubsystemPanicProbe, "dangerous probes remain default-off");
    }

    private static void ProjectPolicy()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string project = Path.Combine(root, "src/OS/OS.csproj");
        XDocument xml = XDocument.Load(project);
        string[] probes = xml.Descendants("DefineConstants")
            .Select(node => node.Parent?.Attribute("Condition")?.Value ?? "")
            .SelectMany(condition => Regex.Matches(condition, @"\$\((Enable\w+Probe)\)").Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Need(probes.Length == 52, "review the complete diagnostic inventory before adding a new profile");
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
            Need(!props.GetProperty("DefineConstants").GetString()!.Split(';').Contains("CLROS_NORMAL_VERIFIED_EXIT"),
                "profile does not silently cross ordinary exit: " + probe);
            var conflict = MsBuild(root, ["-t:ValidateClrosBootPolicy", "-p:ClrosBootPolicy=VerifiedExit", "-p:" + probe + "=true"]);
            Need(conflict.Exit != 0 && conflict.Output.Contains("clros_boot_policy_probe_conflict", StringComparison.Ordinal),
                "mixed policy rejected: " + probe);
        }
        using (JsonDocument compatibility = Evaluate(root, ["-p:ClrosBootPolicy=Diagnostic"]))
            Need(!compatibility.RootElement.GetProperty("Properties").GetProperty("DefineConstants").GetString()!
                .Contains("CLROS_NORMAL_VERIFIED_EXIT", StringComparison.Ordinal), "explicit firmware-visible compatibility");
        var invalid = MsBuild(root, ["-t:ValidateClrosBootPolicy", "-p:ClrosBootPolicy=typo"]);
        Need(invalid.Exit != 0 && invalid.Output.Contains("clros_boot_policy_invalid", StringComparison.Ordinal), "unknown policy rejected");
    }

    private static JsonDocument Evaluate(string root, string[] properties)
    {
        var result = MsBuild(root, ["-getProperty:ClrosBootPolicy,DefineConstants", .. properties]);
        Need(result.Exit == 0, "project evaluation: " + result.Output);
        return JsonDocument.Parse(result.Output);
    }

    private static void ScriptPolicy()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (string script in new[] { "build-efi-shell.ps1", "test-efi-artifact.ps1", "run-qemu-smoke.ps1" })
        {
            var invalid = PowerShell(root, script, ["-BootPolicy", "Unknown"]);
            Need(invalid.Exit == 1 && invalid.Output.Contains("clros_boot_policy_invalid", StringComparison.Ordinal), script + " invalid policy");
            var conflict = PowerShell(root, script, ["-BootPolicy", "VerifiedExit", "-EnableM03FirstRealExitBootServicesProbe"]);
            Need(conflict.Exit == 1 && conflict.Output.Contains("clros_boot_policy_probe_conflict", StringComparison.Ordinal), script + " mixed policy");
        }
        var ordinary = PowerShell(root, "run-qemu-smoke.ps1", ["-DryRun"]);
        Need(ordinary.Exit == 0 && ordinary.Output.Contains("QEMU_SCRIPT_BOOT_POLICY policy=VerifiedExit", StringComparison.Ordinal),
            "unqualified ordinary dry-run: " + ordinary.Output);
        string[] ordered = ordinary.Output.Split('\n').Single(line => line.StartsWith("ORDERED_MARKERS ", StringComparison.Ordinal))
            .TrimEnd('\r')["ORDERED_MARKERS ".Length..].Split(';');
        string[] expectedTail = Expected.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Need(ordered.Length == 79 && ordered.TakeLast(6).SequenceEqual(expectedTail) && ordered.Distinct(StringComparer.Ordinal).Count() == 79,
            "complete unique post-exit marker ordering");
        foreach (string option in new[] { "-RequireMarker", "-RequireOrderedMarker", "-RequireUniqueMarker", "-ForbidMarker" })
        {
            var rejected = PowerShell(root, "run-qemu-smoke.ps1", ["-DryRun", option, "OS_SMOKE_PASS"]);
            Need(rejected.Exit == 1 && rejected.Output.Contains("m03_normal_forbids_override", StringComparison.Ordinal), "closed marker contract " + option);
        }
        var reused = PowerShell(root, "run-qemu-smoke.ps1", ["-DryRun", "-SkipImageBuild"]);
        Need(reused.Exit == 1 && reused.Output.Contains("m03_normal_requires_original_capture", StringComparison.Ordinal), "normal image reuse rejected");
        var legacy = PowerShell(root, "run-qemu-smoke.ps1", ["-DryRun", "-BootPolicy", "Diagnostic"]);
        Need(legacy.Exit == 0 && legacy.Output.Contains("QEMU_SCRIPT_BOOT_POLICY policy=Diagnostic", StringComparison.Ordinal) &&
            !legacy.Output.Contains("CLROS_M03_011_READY", StringComparison.Ordinal), "explicit diagnostic compatibility: " + legacy.Output);
    }

    private static (int Exit, string Output) PowerShell(string root, string script, string[] arguments)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts/" + script }.Concat(arguments))
            start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(90_000)) { process.Kill(entireProcessTree: true); throw new Exception("script policy timeout: " + script); }
        string error = stderr.GetAwaiter().GetResult();
        Need(error.Length == 0, "script policy stderr: " + error);
        return (process.ExitCode, stdout.GetAwaiter().GetResult());
    }

    private static (int Exit, string Output) MsBuild(string root, string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string argument in new[] { "msbuild", "src/OS/OS.csproj", "-nologo", "-v:quiet" }.Concat(arguments))
            start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(90_000)) { process.Kill(entireProcessTree: true); throw new Exception("policy evaluation timeout"); }
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
            if (capture->Count == capture->Limit) return false;
            capture->Bytes[capture->Count++] = text[i];
        }
        return true;
    }

    private static string Text(Capture* capture) => Encoding.ASCII.GetString(capture->Bytes, capture->Count);
    private static void Need(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Run(string name, Action test)
    {
        string group = "m03.011 " + name;
        Console.WriteLine("TEST_START " + group);
        try { test(); Console.WriteLine("TEST_PASS " + group); }
        catch (Exception error) { Console.WriteLine("TEST_FAIL " + group + " " + error.Message); throw; }
    }
}
