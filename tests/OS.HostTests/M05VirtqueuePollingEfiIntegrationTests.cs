using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M05VirtqueuePollingEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M05VirtioNegotiationEfiIntegrationTests.Flags, "--EnableM05VirtqueuePollingProbe"];

    [HostTest("m05.queue.efi requires negotiation and owned diagnostic boot")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM05VirtqueuePollingProbe && !BootSequence.RunOwnedVirtqueuePolling(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M05VirtqueuePolling"));
            foreach (string required in Flags[..^1]) Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string other in new[] { "M05BarDma", "M05PhysicalMapping", "M05MonotonicClock", "M05OwnedAcpiSelection", "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + other + "Probe"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. Flags]));
        HostCheck.True(QemuContracts.M05VirtqueuePollingMarkers.All(plan.Required.Contains));
        _ = new OS.Tools.QemuRunner.SerialMarkerStateMachine(QemuContracts.M05VirtqueuePollingMarkers.ToArray(), []);
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. Flags])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--SkipImageBuild", .. Flags])));
    }
    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m05.queue.efi transcript binds DMA notify completion ordering and reset cleanup")]
    private static void Transcript()
    {
        string[] lines = Capture();
        string path = Path.Combine(Path.GetTempPath(), "clros-queue-" + Guid.NewGuid().ToString("N") + ".log");
        void Check(string[] rows)
        {
            File.WriteAllText(path, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(rows));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioNegotiation: true, virtqueuePolling: true);
        }
        try
        {
            Check(lines);
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true,
                pciEnumeration: true, virtioNegotiation: true));
            int[] indices = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith("M05_QUEUE ", StringComparison.Ordinal)).ToArray();
            foreach ((int row, int col, ulong wrong) in new (int,int,ulong)[]
            { (0,0,0),(0,1,0),(0,1,3),(0,1,4),(0,1,65536),(0,2,16),(0,3,1),
              (1,0,1),(1,1,0),(1,2,1),(1,3,1),(2,0,0),(2,0,1),(2,1,0),(2,2,0),(2,3,8192),
              (3,0,0),(3,1,65536),(3,1,1024),(3,2,3),(3,3,70),
              (4,0,0),(4,1,0),(4,1,2),(4,2,65536),(4,3,1),(5,0,0),(5,0,1000001),(5,1,4),(5,2,0),(5,3,0),
              (6,0,0),(6,1,0),(6,2,999),(6,3,0),(7,0,0),(7,1,0),(7,2,0),(7,3,0x407) })
            {
                string[] changed = [.. lines]; string[] parts = changed[indices[row]].Split(' ');
                parts[col + 2] = "0x" + wrong.ToString("X16"); changed[indices[row]] = string.Join(' ', parts);
                Reject(() => Check(changed));
            }
            string[] swapped = [.. lines]; (swapped[^1], swapped[^2]) = (swapped[^2], swapped[^1]); Reject(() => Check(swapped));
            Reject(() => Check([.. lines, "extra"])); Reject(() => Check(lines[..^1]));
        }
        finally { File.Delete(path); }
    }

    internal static string[] Capture()
    {
        string[] lines = QemuContracts.M05VirtqueuePollingMarkers.ToArray();
        M05VirtioNegotiationEfiIntegrationTests.Capture().CopyTo(lines, 0);
        ulong[] owner = lines[67].Split(' ').Skip(2).Select(s => Convert.ToUInt64(s[2..], 16)).ToArray();
        ulong[][] values = [[1,256,8,2],[0,1,8,11],[owner[2],owner[2],owner[2],4096],
            [0xc000003004,1,4,72],[1,1,0,0],[1,5,1,1],[owner[4],owner[4],owner[6],1],[5,1,1,0x403]];
        int stage = 0;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("M05_QUEUE ", StringComparison.Ordinal))
            { lines[i] = "M05_QUEUE " + stage + string.Concat(values[stage].Select(v => " 0x" + v.ToString("X16"))); stage++; }
        return lines;
    }
    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid queue profile or evidence must be rejected");
    }
}
