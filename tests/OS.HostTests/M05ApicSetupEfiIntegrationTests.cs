using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M05ApicSetupEfiIntegrationTests
{
    internal static readonly string[] Flags = [.. M05VirtioNegotiationEfiIntegrationTests.Flags[..^1], "--EnableM05ApicSetupProbe"];

    [HostTest("m05.apic.efi defaults off and requires exclusive owned PCI profile")]
    private static void Profile()
    {
        int calls = 0;
        HostCheck.True(!Probes.EnableM05ApicSetupProbe && !BootSequence.RunOwnedApicSetup(true, &calls, &Execute) && calls == 0);
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(EfiOptions.Parse(Flags, build).Profile().IsEnabled("M05ApicSetup"));
            foreach (string required in Flags[..^1])
                Reject(() => EfiOptions.Parse(Flags.Where(f => f != required).ToArray(), build).Profile());
            Reject(() => EfiOptions.Parse([.. Flags, "--BootPolicy", "VerifiedExit"], build).Profile());
            foreach (string other in new[] { "M05BarDma", "M05VirtqueuePolling", "M05VirtioNegotiation", "M05PhysicalMapping", "M05MonotonicClock", "M05OwnedAcpiSelection", "M03OwnedSubsystemPanic", "M03FirmwareAdapterRevocation" })
                Reject(() => EfiOptions.Parse([.. Flags, "--Enable" + other + "Probe"], build).Profile());
        }
        var plan = QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", .. Flags]));
        HostCheck.True(QemuContracts.M05ApicSetupMarkers.All(plan.Required.Contains));
        _ = new OS.Tools.QemuRunner.SerialMarkerStateMachine(QemuContracts.M05ApicSetupMarkers.ToArray(), []);
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", .. Flags])));
        Reject(() => QemuProfiles.Create(QemuSmokeOptions.Parse(["--DryRun", "--BuildEfi", "--SkipImageBuild", .. Flags])));
    }
    private static bool Execute(void* context)
    {
        (*(int*)context)++;
        return true;
    }

    [HostTest("m05.apic.efi transcript binds CPU controller masking and disabled interrupts")]
    private static void Transcript()
    {
        string[] lines = Capture();
        string path = Path.Combine(Path.GetTempPath(), "clros-apic-" + Guid.NewGuid().ToString("N") + ".log");
        void Check(string[] rows)
        {
            File.WriteAllText(path, M03OwnedSubsystemSurvivalEfiIntegrationTests.Frame(rows));
            QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, pciEnumeration: true, apicSetup: true);
        }
        try
        {
            Check(lines);
            Check(lines.Select(s => s.Replace("0x0000000000170011", "0x0000000000170020", StringComparison.Ordinal)).ToArray());
            Reject(() => QemuTranscripts.AssertM03AcpiBootstrapTranscript(path, firstRealExit: true, ownedSurvival: true, pciEnumeration: true));
            Mutations(lines, "M05_APIC ", [(0,0,0),(0,0,0xfee00001),(0,1,0xfee00000),(0,2,1),(0,3,2),
                (1,0,0xfee00800),(1,1,0x200),(1,2,0x50016),(1,2,0x70014),(1,3,0x180011),(1,3,0x170012),(1,3,0x170021),(1,3,0x180020),(1,3,0x170120),
                (2,0,5),(2,1,23),(2,2,0),(2,3,24),(3,0,0),(3,1,0xff),(3,2,0x202),(3,3,uint.MaxValue)], Check);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static string[] Capture()
    {
        string[] lines = QemuContracts.M05ApicSetupMarkers.ToArray();
        M05VirtioNegotiationEfiIntegrationTests.Capture()[..QemuContracts.M05PciEnumerationMarkers.Count].CopyTo(lines, 0);
        Fill(lines, "M05_APIC", [[0xfee00000, 0xfec00000, 0, 1], [0xfee00900, 0x220, 0x50014, 0x170011], [6, 24, 1, 9], [0xd0, 0x1ff, 2, 0]]);
        return lines;
    }

    internal static void Fill(string[] lines, string prefix, ulong[][] values)
    {
        int stage = 0;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith(prefix + " ", StringComparison.Ordinal))
            {
                lines[i] = prefix + " " + stage + string.Concat(values[stage].Select(v => " 0x" + v.ToString("X16")));
                stage++;
            }
        HostCheck.Equal(values.Length, stage);
    }

    internal static void Mutations(string[] lines, string prefix, (int Row, int Column, ulong Wrong)[] changes, Action<string[]> check)
    {
        int[] indices = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        foreach (var change in changes)
        {
            string[] changed = [.. lines];
            string[] parts = changed[indices[change.Row]].Split(' ');
            parts[change.Column + 2] = "0x" + change.Wrong.ToString("X16");
            changed[indices[change.Row]] = string.Join(' ', parts);
            Reject(() => check(changed));
        }
        string[] swapped = [.. lines];
        (swapped[^1], swapped[^2]) = (swapped[^2], swapped[^1]);
        Reject(() => check(swapped));
        Reject(() => check([.. lines, "extra"]));
        Reject(() => check(lines[..^1]));
    }

    internal static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected, "invalid interrupt profile or evidence must be rejected");
    }
}
