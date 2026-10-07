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
        foreach (bool revocation in new[] { false, true }) CheckProfile(revocation);
    }

    private static void CheckProfile(bool revocation)
    {
        string[] arguments = ["-EnableM01LoadedImageStackProbe", "-EnableM01LiveAllocatorProbe", "-EnableAcpiPhysicalDiscoveryProbe", "-EnableM03FirstRealExitBootServicesProbe"];
        string output = Invoke(revocation ? [.. arguments, "-" + Flag] : arguments);
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required = Read("REQUIRED_MARKERS "), ordered = Read("ORDERED_MARKERS "), unique = Read("UNIQUE_MARKERS "), forbidden = Read("FORBIDDEN_MARKERS ");
        Need(ordered.Length == (revocation ? 78 : 66) && ordered.SequenceEqual(unique, StringComparer.Ordinal), "exact closed first-exit/child profile");
        Need(ordered[55] == "TEST_START m03.007", "first-exit admission order");
        int tail = revocation ? 62 : 61;
        string[] ownedTail = ["TEST_START m03.009", "UEFI_EBS_SUCCESS", "POST_FIRMWARE_DIAGNOSTICS_READY", "CLROS_M03_009_READY", "TEST_PASS m03.009"];
        Need(ordered.Skip(tail).Take(5).SequenceEqual(ownedTail, StringComparer.Ordinal), "complete owned diagnostic tail");
        Need(ordered[^1] == (revocation ? "TEST_PASS m03.008" : "TEST_PASS m03.009"), "exact terminal record");
        bool AcceptText(string text)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            return state.Feed(Encoding.ASCII.GetBytes(text)).Succeeded && state.Complete().Passed;
        }
        bool Accept(string[] records) => AcceptText(string.Join("\r\n", records) + "\r\n");
        Need(Accept(ordered), "controlled complete marker stream");
        foreach (int index in Enumerable.Range(tail, 5))
        {
            Need(!Accept(ordered.Where((_, i) => i != index).ToArray()), "missing owned diagnostic record " + index);
            Need(!Accept(ordered.Take(index).Concat([ordered[index]]).Concat(ordered.Skip(index)).ToArray()), "duplicate owned diagnostic record " + index);
            string partial = string.Join("\r\n", ordered.Take(index)) + "\r\n" + ordered[index][..^1];
            Need(!AcceptText(partial), "truncated owned diagnostic record " + index);
        }
        for (int index = tail; index < tail + 4; index++)
        {
            string[] reordered = (string[])ordered.Clone();
            (reordered[index], reordered[index + 1]) = (reordered[index + 1], reordered[index]);
            Need(!Accept(reordered), "reordered owned diagnostic records " + index);
        }
        if (!revocation) return;
        Need(ordered[56] == "M03_ADAPTER_ADMITTED 1 0" && ordered[67] == "TEST_START m03.008", "admission and owned diagnostic order");
        for (int i = 0; i < 8; i++) Need(ordered[68+i] == $"M03_ADAPTER_DENIED {i} 4 0 1", "exact denial status/count/preservation");
        foreach (int index in new[] { 56 }.Concat(Enumerable.Range(67, 11)))
        {
            Need(!Accept(ordered.Where((_, i) => i != index).ToArray()), "missing copied-capability record " + index);
            Need(!Accept(ordered.Take(index).Concat([ordered[index]]).Concat(ordered.Skip(index)).ToArray()), "duplicate copied-capability record " + index);
        }
        string[] wrong = (string[])ordered.Clone(); wrong[68] = "M03_ADAPTER_DENIED 0 1 0 1";
        Need(!Accept(wrong), "successful post-exit call cannot be accepted");
    }
}
