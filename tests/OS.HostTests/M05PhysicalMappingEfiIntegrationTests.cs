using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static unsafe class M05PhysicalMappingEfiIntegrationTests
{
    [HostTest("m05.001.efi disabled boot gate")]
    private static void Disabled()
    {
        int invocations = 0;
        HostCheck.True(!Probes.EnableM05PhysicalMappingProbe &&
            !BootSequence.RunPostFirmwarePhysicalMapping(true, &invocations, &Execute) &&
            !BootSequence.RunPostFirmwarePhysicalMapping(false, &invocations, &Execute) &&
            !BootSequence.RunPostFirmwarePhysicalMapping(true, null, &Execute) &&
            !BootSequence.RunPostFirmwarePhysicalMapping(true, &invocations, null) && invocations == 0);
    }

    private static bool Execute(void* context) { (*(int*)context)++; return true; }

    [HostTest("m05.001.efi transcript associations")]
    private static void TranscriptControls()
    {
        const ulong table = 0x500000, scratch = 0x4000000, parent = 0x501100, pt = 0x504000;
        const ulong nx = 1UL << 63, oldPat = 0x0007040600070406, newPat = 0x0007040100070406;
        ulong large = nx | scratch | 0xe3, uc = nx | (scratch + 4096) | 0x7b;
        ulong wc = nx | (scratch + 4096) | 0xe3, wb = nx | (scratch + 4096) | 0x63;
        string Row(int stage, params ulong[] values) => "M05_MAP " + stage + string.Concat(values.Select(v => " 0x" + v.ToString("x16")));
        string[] lines = Enumerable.Repeat("fixture", 86).ToArray();
        lines[73] = "M03_OWNED 6" + string.Concat(new ulong[] { 0x1000000, table, 0x2000000, 0, 0, 0, 0, 0 }.Select(v => " 0x" + v.ToString("X16")));
        lines[76] = "TEST_START m05.001";
        lines[77] = Row(0, table, scratch, 0x508, 0x800);
        lines[78] = Row(1, oldPat, newPat, table, 0x11000);
        lines[79] = Row(2, parent, pt | 0x23, large, 4);
        lines[80] = Row(3, parent, large, pt | 0x23, uc);
        lines[81] = Row(4, pt + 8, uc, wc, newPat);
        lines[82] = Row(5, pt + 8, wc, wb, 0x5743000000000001);
        lines[83] = Row(6, table, scratch, oldPat, wb);
        lines[84] = "CLROS_M05_001_READY"; lines[85] = "TEST_PASS m05.001";
        QemuTranscripts.AssertM05PhysicalMappingRecords(lines);
        foreach (var (at, before, after) in new (int, string, string)[]
        {
            (77, "0x0000000000500000", "0x0000000000501000"),
            (77, "0x0000000004000000", "0x0000000001000000"),
            (77, "0x0000000000000508", "0x0000000000000d08"),
            (78, "0x0007040100070406", "0x0007040100070006"),
            (79, "0x0000000000000004", "0x0000000000000037"),
            (79, "0x0000000000504023", "0x0000000000504063"),
            (80, "0x800000000400107b", "0x000000000400107b"),
            (81, "0x80000000040010e3", "0x8000000004001063"),
            (83, "0x0007040600070406", "0x0007040100070406")
        })
        {
            string[] changed = [.. lines]; changed[at] = changed[at].Replace(before, after, StringComparison.Ordinal);
            HostCheck.True(changed[at] != lines[at], "mutation must change the vector");
            Reject(() => QemuTranscripts.AssertM05PhysicalMappingRecords(changed));
        }
        Reject(() => QemuTranscripts.AssertM05PhysicalMappingRecords([.. lines, "unexpected trailing row"]));
    }

    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected, "invalid mapping evidence must be rejected");
    }
}
