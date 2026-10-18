using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;

internal static unsafe class LinuxAppStartupTests
{
    private const string Prefix = "CLROS_APP_ENTRY minimal.api.smoke\nCLROS_APP_CCTOR minimal.api.smoke\n" +
        "CLROS_APP_CTOR minimal.api.smoke\nCLROS_APP_BUILT minimal.api.smoke\n";
    private const ulong Writer = 0x100000001, Binder = 0x100000005;

    [HostTest("m10.006 startup observes fragmented constructors before a different verified bind thread")]
    private static void OrderedFragments()
    {
        var value = new LinuxAppStartupObservation();
        HostCheck.Equal(false, value.StartupComplete); HostCheck.Equal(false, value.SuccessfulBind);
        HostCheck.Equal(0U, value.ObservedStages); HostCheck.Equal(0UL, value.WriterIdentity);
        byte[] bytes = Encoding.ASCII.GetBytes(Prefix);
        fixed (byte* source = bytes)
        {
            for (int i = 0; i < bytes.Length;)
            {
                int count = Math.Min(1 + i % 7, bytes.Length - i);
                HostCheck.True(value.Observe(Writer, source + i, (uint)count)); i += count;
            }
            HostCheck.True(value.StartupComplete); HostCheck.Equal(false, value.SuccessfulBind);
            HostCheck.Equal(4U, value.ObservedStages); HostCheck.Equal(Writer, value.WriterIdentity);
            HostCheck.True(value.RecordBind(Binder, true)); HostCheck.True(value.SuccessfulBind);
            HostCheck.Equal(Binder, value.BindIdentity);
            // Logging after startup may come from other owned writers.
            HostCheck.True(value.Observe(Binder + 1, source, 1));
        }
    }

    [HostTest("m10.006 startup rejects malformed missing duplicate reordered and truncated observations")]
    private static void InvalidSequence()
    {
        foreach (string text in new[] { Prefix.Replace("CLROS_APP_CCTOR minimal.api.smoke\n", "", StringComparison.Ordinal),
            Prefix.Replace("CLROS_APP_CCTOR", "CLROS_APP_CTOR", StringComparison.Ordinal),
            Prefix.Replace("minimal.api.smoke", "another.api.smoke", StringComparison.Ordinal),
            "CLROS_APP_ENTRY minimal.api.smoke\n" + Prefix, Prefix.Replace("\n", "\r\n", StringComparison.Ordinal), Prefix[..^1] })
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            var value = new LinuxAppStartupObservation();
            fixed (byte* source = bytes) value.Observe(Writer, source, (uint)bytes.Length);
            HostCheck.Equal(false, value.StartupComplete); HostCheck.Equal(false, value.RecordBind(Binder, true));
            HostCheck.True(value.Failed); HostCheck.Equal(false, value.SuccessfulBind);
            HostCheck.Equal(false, Feed(ref value));
        }
    }

    [HostTest("m10.006 startup invalid buffers foreign writers and bind failures remain failed")]
    private static void InvalidOwnershipAndBind()
    {
        byte one = (byte)'C';
        for (int mutation = 0; mutation < 8; mutation++)
        {
            var value = new LinuxAppStartupObservation();
            bool accepted;
            switch (mutation)
            {
                case 0: accepted = value.Observe(0, &one, 1); break;
                case 1: accepted = value.Observe(Writer, null, 1); break;
                case 2: accepted = value.Observe(Writer, &one, 0); break;
                case 3: accepted = value.Observe(Writer, &one, 8193); break;
                case 4:
                    HostCheck.True(value.Observe(Writer, &one, 1));
                    accepted = value.Observe(Binder, &one, 1); break;
                case 5: accepted = value.RecordBind(Binder, true); break;
                case 6: HostCheck.True(Feed(ref value)); accepted = value.RecordBind(Binder, false); break;
                default:
                    HostCheck.True(Feed(ref value)); HostCheck.True(value.RecordBind(Binder, true));
                    accepted = value.RecordBind(Binder, true); break;
            }
            HostCheck.Equal(false, accepted); HostCheck.True(value.Failed);
            HostCheck.Equal(false, Feed(ref value)); HostCheck.Equal(false, value.StartupComplete);
            HostCheck.Equal(false, value.SuccessfulBind);
        }
        var zeroBinder = new LinuxAppStartupObservation(); HostCheck.True(Feed(ref zeroBinder));
        HostCheck.Equal(false, zeroBinder.RecordBind(0, true)); HostCheck.True(zeroBinder.Failed);
    }

    [HostTest("m10.006 startup observation and bind allocate no managed memory")]
    private static void AllocationBoundary()
    {
        var warm = new LinuxAppStartupObservation(); HostCheck.True(Feed(ref warm));
        byte[] bytes = Encoding.ASCII.GetBytes(Prefix);
        bool accepted = true;
        long before = GC.GetAllocatedBytesForCurrentThread();
        fixed (byte* source = bytes)
            for (int i = 0; i < 1000; i++)
            {
                var value = new LinuxAppStartupObservation();
                accepted &= value.Observe(Writer, source, (uint)bytes.Length) && value.RecordBind(Binder, true) && value.SuccessfulBind;
            }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(accepted); HostCheck.Equal(0L, allocated);
    }

    private static bool Feed(ref LinuxAppStartupObservation value)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(Prefix);
        fixed (byte* source = bytes) return value.Observe(Writer, source, (uint)bytes.Length);
    }

    [HostTest("m10.006 raw startup joins framed stderr writev bind and live observations")]
    private static void RawJoin()
    {
        byte[] raw = StartupRaw("none"), before = (byte[])raw.Clone();
        var value = QemuTranscripts.InspectNativeAotWebStartupForTests(raw);
        HostCheck.Equal(Writer, value.WriterIdentity); HostCheck.Equal(Binder, value.BindIdentity);
        HostCheck.Equal(2, value.StartupWriteRecords); HostCheck.True(raw.AsSpan().SequenceEqual(before));
    }

    [HostTest("m10.006 raw startup rejects missing forged foreign early failed and repeated observations")]
    private static void RawNegatives()
    {
        foreach (string mutation in new[] { "missing", "marker_writer", "foreign_writer", "early_bind", "failed_bind",
            "duplicate_bind", "missing_ready", "failure", "short_write", "wrong_bind_fd", "wrong_listen_fd", "stages", "early_marker", "orphan" })
        {
            bool rejected = false;
            try { QemuTranscripts.InspectNativeAotWebStartupForTests(StartupRaw(mutation)); }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, mutation);
        }
    }

    private static byte[] StartupRaw(string mutation)
    {
        string Call(ulong identity, int ordinal, int number, int descriptor, long result, int count = 1) =>
            $"M16_008_CALL identity={identity} nr={number} a0={descriptor} a1=4096 a2={count} a3=0 a4=0 a5=0 rip=4194818 rsp=1879048176 flags=514 fs=0 gs=0 clock=10 valueBits={unchecked((ulong)result)} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048176 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={unchecked((ulong)result)}\r\n";
        string Frame(string text) => $"M16_005_WRITE fd=2 bytes={Encoding.ASCII.GetByteCount(text)} data={text}\r\n";
        string startup = $"M10_006_STARTUP writer={(mutation == "marker_writer" ? Binder : Writer)} stages={(mutation == "stages" ? 3 : 4)}\r\n";
        string bind = $"M10_006_BIND identity={Binder} fd={(mutation == "wrong_bind_fd" ? 7 : 6)}\r\n" +
            Call(Binder, mutation == "foreign_writer" ? 2 : 1, 49, 6, mutation == "failed_bind" ? -98 : 0);
        string first = Prefix[..10], rest = Prefix[10..];
        string text = "TEST_START m10.004\r\nTEST_START m16-009\r\nTEST_START m10.006\r\n";
        if (mutation == "early_marker") text += startup;
        text += Frame(first) + Call(Writer, 1, 1, 2, first.Length, first.Length);
        if (mutation == "early_bind") text += bind;
        if (mutation != "missing") text += Frame(rest);
        if (mutation != "early_marker") text += startup;
        text += Call(mutation == "foreign_writer" ? Binder : Writer, mutation == "foreign_writer" ? 1 : 2,
            20, 2, rest.Length - (mutation == "short_write" ? 1 : 0));
        if (mutation != "early_bind") text += bind;
        if (mutation == "duplicate_bind") text += Call(Binder, 2, 49, 6, 0);
        text += $"M10_004_LISTEN fd={(mutation == "wrong_listen_fd" ? 7 : 6)} address=0 port=8080 owner=live\r\n" +
            "CLROS_M10_004_READY\r\nTEST_PASS m10.004\r\n";
        if (mutation != "missing_ready") text += "CLROS_M10_006_READY\r\n";
        text += "TEST_PASS m10.006\r\n";
        if (mutation == "failure") text += "TEST_FAIL m10.006 startup-observation\r\n";
        if (mutation == "orphan") text += Frame("x");
        return Encoding.ASCII.GetBytes(text);
    }
}
