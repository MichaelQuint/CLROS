using OS.Tooling;

internal static class NativeAotWebVectorCallTests
{
    // Borrowed minimal call rows grant no memory, scheduler, packet or guest authority.
    private static string[] Rows(string change = "", int accepts = 6)
    {
        List<string> rows = [];
        uint ordinal = 0;
        void Call(uint number, uint fd, long result, ulong arg1 = 0)
        {
            ulong value = unchecked((ulong)result);
            ordinal++;
            rows.Add($"M16_008_CALL identity=4294967297 nr={number} a0={fd} a1={arg1} a2=0 a3=0 a4=0 a5=0 rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 clock=10 valueBits={value} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}");
        }
        for (int index = 0; index < (change == "missing_health" ? 5 : accepts); index++)
        {
            Call(43, 5, 6);
            if (index == 4)
            {
                uint total = change == "small_body" ? 640u : 49408u;
                while (total > 0)
                {
                    uint count = Math.Min(total, 8192u), fd = change == "wrong_descriptor" ? 7u : 6u;
                    if (change == "scalar_only")
                        Call(44, 6, count);
                    else
                    {
                        uint first = change == "empty_first" ? 0u : change == "first_only" ? total - 1 : Math.Min(128, total), second = total - first;
                        uint generation = change == "changed_generation" && total < 49408 ? 6u : 5u;
                        rows.Add($"M16_013_MESSAGE identity=4294967297 ordinal={ordinal + 1} nr=46 fd={fd} generation={generation} header=8192 flags=0 table=12288 vectors=2 total={total}");
                        rows.Add($"M16_013_VECTOR identity=4294967297 ordinal={ordinal + 1} index=0 address=16384 bytes={first}");
                        rows.Add($"M16_013_VECTOR identity=4294967297 ordinal={ordinal + 1} index=1 address=32768 bytes={second}");
                        Call(46, fd, change == "oversized_return" ? 8193 : count, 8192);
                    }
                    total -= count;
                }
            }
            Call(3, 6, 0);
        }
        return rows.ToArray();
    }

    [HostTest("Kestrel vector call proof requires positive scatter and native short sends on the large response lifetime")]
    private static void Positive()
    {
        var report = QemuTranscripts.InspectNativeAotWebVectorCallsForTests(Rows());
        HostCheck.Equal(7, report.Sends);
        HostCheck.Equal(7, report.ScatterSends);
        HostCheck.Equal(6, report.ShortSends);
        HostCheck.Equal(49408UL, report.Bytes);
    }

    [HostTest("Kestrel vector controls retain fifth response scatter short sends and sixth health across two later accepts")]
    private static void Controls()
    {
        var report = QemuTranscripts.InspectNativeAotWebVectorCallsForTests(Rows(accepts: 8), transportControls: true);
        HostCheck.Equal(7, report.Sends);
        HostCheck.Equal(7, report.ScatterSends);
        HostCheck.Equal(6, report.ShortSends);
        HostCheck.Equal(49408UL, report.Bytes);
        foreach (string change in new[] { "missing_health", "small_body", "wrong_descriptor", "scalar_only", "empty_first", "first_only", "changed_generation", "oversized_return" })
            Reject(Rows(change, 8), true);
    }

    [HostTest("Kestrel vector accept inventories require exact declared controls and reject missing extra or legacy scope")]
    private static void ControlScope()
    {
        foreach (int accepts in new[] { 5, 6, 7, 9 })
            Reject(Rows(accepts: accepts), true);
        foreach (int accepts in new[] { 7, 8, 9 })
            Reject(Rows(accepts: accepts), false);
    }

    private static void Reject(string[] rows, bool controls)
    {
        bool rejected = false;
        try
        {
            QemuTranscripts.InspectNativeAotWebVectorCallsForTests(rows, controls);
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("Kestrel vector call proof rejects scalar small foreign stale oversized and absent later health observations")]
    private static void Negative()
    {
        foreach (string change in new[] { "missing_health", "small_body", "wrong_descriptor", "scalar_only", "empty_first", "first_only", "changed_generation", "oversized_return" })
        {
            bool rejected = false;
            try
            {
                QemuTranscripts.InspectNativeAotWebVectorCallsForTests(Rows(change));
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, change);
        }
    }
}
