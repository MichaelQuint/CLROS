using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

internal static class M03FirmwareAdapterRevocationEfiIntegrationTests
{
    private const string Flag = "EnableM03FirmwareAdapterRevocationProbe";
    private static string Root => HostToolProcess.FindRepositoryRoot();

    [ModuleInitializer]
    internal static void RunAll()
    {
        Run("retained original authority and default-off child profile", Source);
        Run("complete marker admission and missing copied-capability refusals", Profile);
    }

    private static void Run(string name, Action action)
    {
        Console.WriteLine("TEST_START m03.008.efi " + name);
        try { action(); Console.WriteLine("TEST_PASS m03.008.efi " + name); }
        catch (Exception error) { Console.WriteLine("TEST_FAIL m03.008.efi " + name + " " + error); throw; }
    }
    private static void Need(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private static void Source()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/OS/OS.csproj"));
        Need(project.Descendants(Flag).Single().Value == "false", "disabled default");
        Need(project.Descendants("Error").Count(x => (string?)x.Attribute("Condition") ==
            "'$(EnableM03FirmwareAdapterRevocationProbe)' == 'true' and '$(EnableM03FirstRealExitBootServicesProbe)' != 'true'") == 1, "exact first-exit dependency");
        string entry = File.ReadAllText(Path.Combine(Root, "src/OS/Boot/EfiEntry.cs"));
        int prepare = entry.IndexOf("private static bool PrepareAdapters(", StringComparison.Ordinal);
        int report = entry.IndexOf("private static bool ReportRevokedAdapters(", StringComparison.Ordinal);
        int marker = entry.IndexOf("private static bool WriteAdapterMarker(", StringComparison.Ordinal);
        Need(prepare > 0 && report > prepare && marker > report, "bounded profile implementation");
        Need(entry[prepare..report].Contains("&state->Arena, state->Memory.References, backends)", StringComparison.Ordinal), "original owner binding");
        Need(entry[prepare..report].Contains("state->CopiedHandles = state->OriginalHandles;", StringComparison.Ordinal), "retained handle copies");
        Need(entry[prepare..report].Contains("state->OriginalHandles.Console.Write(&empty, 1)", StringComparison.Ordinal), "actual admitted console positive");
        string diagnostic = entry[report..marker];
        foreach (string method in new[] { "Console.Write", "MemoryMap.Capture", "FileSystem.Read", "Timer.Read", "Random.Fill", "ConfigurationTable.Find", "PhysicalMemory.Read", "Framebuffer.Snapshot" })
            Need(diagnostic.Contains("state->CopiedHandles." + method, StringComparison.Ordinal), "typed copied capability " + method);
        Need(diagnostic.Contains("FirmwareAdapterStatus.ServicesUnavailable", StringComparison.Ordinal) &&
            diagnostic.Contains("AdapterEntryCount(state) == 1", StringComparison.Ordinal) &&
            diagnostic.Contains("output[j] != 0xa5", StringComparison.Ordinal), "denial before backend or output mutation");
        Need(!diagnostic.Contains("Write(console", StringComparison.Ordinal) &&
            entry[marker..entry.IndexOf("#endif", marker, StringComparison.Ordinal)].Contains("state->Arena.Fault->Backend", StringComparison.Ordinal), "owned diagnostic transport");
    }

    private static string Invoke(params string[] arguments)
    {
        var start = new ProcessStartInfo("powershell.exe") { WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string item in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(Root, "scripts/run-qemu-smoke.ps1"), "-DryRun", "-BuildEfi" }.Concat(arguments)) start.ArgumentList.Add(item);
        start.Environment["PSModulePath"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32/WindowsPowerShell/v1.0/Modules");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        bool ended = process.WaitForExit(120_000);
        if (!ended) { process.Kill(true); process.WaitForExit(); }
        string stdout = output.GetAwaiter().GetResult(), stderr = error.GetAwaiter().GetResult();
        Need(ended && process.ExitCode == 0 && stderr.Length == 0, stdout + stderr);
        return stdout;
    }

    private static void Profile()
    {
        string output = Invoke("-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableAcpiPhysicalDiscoveryProbe", "-EnableM03FirstRealExitBootServicesProbe", "-" + Flag);
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required = Read("REQUIRED_MARKERS "), ordered = Read("ORDERED_MARKERS "), unique = Read("UNIQUE_MARKERS "), forbidden = Read("FORBIDDEN_MARKERS ");
        Need(ordered.Length == 73 && ordered.SequenceEqual(unique, StringComparer.Ordinal), "exact closed 73-record profile");
        Need(ordered[55] == "TEST_START m03.007" && ordered[56] == "M03_ADAPTER_ADMITTED 1 0" && ordered[62] == "TEST_START m03.008", "admission and owned diagnostic order");
        for (int i = 0; i < 8; i++) Need(ordered[63+i] == $"M03_ADAPTER_DENIED {i} 4 0 1", "exact denial status/count/preservation");
        bool Accept(string[] records)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            return state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", records) + "\r\n")).Succeeded && state.Complete().Passed;
        }
        Need(Accept(ordered), "controlled complete marker stream");
        foreach (int index in new[] { 56 }.Concat(Enumerable.Range(62, 11)))
        {
            Need(!Accept(ordered.Where((_, i) => i != index).ToArray()), "missing new record " + index);
            Need(!Accept(ordered.Take(index).Concat([ordered[index]]).Concat(ordered.Skip(index)).ToArray()), "duplicate new record " + index);
        }
        string[] wrong = (string[])ordered.Clone(); wrong[63] = "M03_ADAPTER_DENIED 0 1 0 1";
        Need(!Accept(wrong), "successful post-exit call cannot be accepted");
    }
}
