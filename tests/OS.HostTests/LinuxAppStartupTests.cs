using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

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
}
