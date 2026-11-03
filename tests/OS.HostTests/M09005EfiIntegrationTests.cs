using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M09005EfiIntegrationTests
{
    private static readonly string[] Parents = ["M01LoadedImageStack", "M01LiveAllocator", "AcpiPhysicalDiscovery",
        "M03FirstRealExitBootServices", "M03OwnedSubsystemSurvival", "M05PciEnumeration", "M05ApicSetup"];
    [HostTest("m09.005 default-off hardware profile selects only authenticated APIC and exit parents", HostLane.Fast, 0)]
    private static void Defaults()
    {
        HostCheck.True(!Probes.EnableM09005Probe && !Probes.GetM09005Probe().EnabledByDefault);
        HostCheck.True(!BootSequence.RunOwnedSchedulerTimer(true, true, true, null, null));
        foreach (bool build in new[] { false, true })
        {
            HostCheck.True(!EfiOptions.Parse([], build).Profile().IsEnabled("M09005"));
            var p = EfiOptions.Parse(["--EnableM09005Probe"], build).Profile();
            HostCheck.True(p.BootPolicy == "Diagnostic" && p.IsEnabled("M09005") && Parents.All(p.IsEnabled));
            HostCheck.Equal(8, p.Flags.Count(pair => pair.Value));
            foreach (string parent in Parents)
                Reject(() => EfiOptions.Parse(["--EnableM09005Probe", "--Enable" + parent + "Probe:false"], build).Profile());
            Reject(() => EfiOptions.Parse(["--BootPolicy", "VerifiedExit", "--EnableM09005Probe"], build).Profile());
            foreach (string conflict in EfiContracts.DiagnosticProbes.Where(name => name != "EnableM09005Probe" && !Parents.Any(parent => name == "Enable" + parent + "Probe")))
                Reject(() => EfiOptions.Parse(["--EnableM09005Probe", "--" + conflict], build).Profile());
        }
    }
    [HostTest("m09.005 serial admission rejects missed publication, stale wake and incomplete restoration", HostLane.Fast, 1)]
    private static void Transcript()
    {
        const ulong pool = 0x100000;
        string Row(string marker, params ulong[] values) => marker + string.Concat(values.Select(value => " 0x" + value.ToString("X16")));
        string[] lines = ["ancestor", "TEST_START m09.005",
            Row("M09_005_PREPARED",pool,22,pool+13*4096,pool+0xc00),
            Row("M09_005_ARMED",pool^0x4d30393030354f57UL,2000000,0xe1,16),
            Row("M09_005_TICK",1,1,1,20), Row("M09_005_TICK",2,2,2,40),
            Row("M09_005_TICK",3,3,3,60), Row("M09_005_TICK",4,0,0,80),
            Row("M09_005_WAKE",4,0x100000001,1,pool+0x28b),
            Row("M09_005_RESTORED",4,22,32,1), "TEST_PASS m09.005", "CLROS_M09_005_READY"];
        HostCheck.True(QemuTranscripts.ValidateSchedulerTimerTail(lines).SequenceEqual(new[] { "ancestor" }));
        foreach (var (index, replacement) in new (int, string)[] {
            (4,Row("M09_005_TICK",1,0,0,20)), (5,Row("M09_005_TICK",2,3,2,40)),
            (6,Row("M09_005_TICK",3,2,3,60)), (7,Row("M09_005_TICK",4,0,1,80)),
            (7,Row("M09_005_TICK",4,0,0,10)), (8,Row("M09_005_WAKE",4,0x100000002,1,pool+0x28b)),
            (8,Row("M09_005_WAKE",4,0x100000001,5,pool+0x28b)), (9,Row("M09_005_RESTORED",4,22,31,1)),
            (11,"TEST_PASS m09.005") })
        {
            string[] invalid = (string[])lines.Clone();
            invalid[index] = replacement;
            Reject(() => QemuTranscripts.ValidateSchedulerTimerTail(invalid));
        }
    }
    private static void Reject(Action action)
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
        HostCheck.True(rejected);
    }
}
