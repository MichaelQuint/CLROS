using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

internal static unsafe class NativeAotServiceStartupTests
{
    private const string Service = "CLROS_APP_ENTRY minimal.api.service\nCLROS_APP_CCTOR minimal.api.service\n" +
        "CLROS_APP_CTOR minimal.api.service\nCLROS_APP_BUILT minimal.api.service\n";
    private const string Smoke = "CLROS_APP_ENTRY minimal.api.smoke\nCLROS_APP_CCTOR minimal.api.smoke\n" +
        "CLROS_APP_CTOR minimal.api.smoke\nCLROS_APP_BUILT minimal.api.smoke\n";
    private const ulong Writer = 0x100000001, Binder = 0x100000005;

    [HostTest("nativeaot.service.startup fragmented identity succeeds and default smoke remains isolated", HostLane.Fast, 0)]
    private static void Fragmented()
    {
        var service = LinuxAppStartupObservation.ForService();
        HostCheck.True(service.IsService && !service.StartupComplete && !service.SuccessfulBind && !service.Failed);
        HostCheck.Equal(0U, service.ObservedStages); HostCheck.Equal(0UL, service.WriterIdentity);
        HostCheck.Equal(0UL, service.BindIdentity);
        byte[] bytes = Encoding.ASCII.GetBytes(Service);
        fixed (byte* source = bytes)
            for (int i = 0; i < bytes.Length;)
            {
                int count = Math.Min(1 + i % 7, bytes.Length - i);
                HostCheck.True(service.Observe(Writer, source + i, (uint)count)); i += count;
                HostCheck.True(service.IsService);
            }
        HostCheck.True(service.StartupComplete && service.RecordBind(Binder, true) && service.SuccessfulBind);
        HostCheck.True(service.IsService); HostCheck.Equal(4U, service.ObservedStages);
        HostCheck.Equal(Writer, service.WriterIdentity); HostCheck.Equal(Binder, service.BindIdentity);
        var smoke = default(LinuxAppStartupObservation);
        HostCheck.True(!smoke.IsService && !Feed(ref smoke, Service) && smoke.Failed);
        HostCheck.True(!smoke.IsService && !Feed(ref smoke, Smoke));
        var wrong = LinuxAppStartupObservation.ForService();
        HostCheck.True(!Feed(ref wrong, Smoke) && wrong.Failed && wrong.IsService);
        HostCheck.True(!Feed(ref wrong, Service) && wrong.IsService);
    }

    [HostTest("nativeaot.service.startup malformed mixed writer buffer and bind failures stay failed", HostLane.Fast, 1)]
    private static void Failures()
    {
        foreach (string text in new[]
        {
            Service.Replace("CLROS_APP_CCTOR minimal.api.service\n", "", StringComparison.Ordinal),
            Service.Replace("CLROS_APP_CCTOR", "CLROS_APP_CTOR", StringComparison.Ordinal),
            "CLROS_APP_ENTRY minimal.api.service\n" + Service,
            Service.Replace("\n", "\r\n", StringComparison.Ordinal), Service[..^1],
            Service.Replace("CLROS_APP_CTOR minimal.api.service", "CLROS_APP_CTOR minimal.api.smoke", StringComparison.Ordinal),
            Service.Replace("minimal.api.service", "another.api.service", StringComparison.Ordinal)
        })
        {
            var value = LinuxAppStartupObservation.ForService(); _ = Feed(ref value, text);
            HostCheck.True(!value.StartupComplete && !value.RecordBind(Binder, true));
            HostCheck.True(value.Failed && value.IsService && !value.SuccessfulBind);
            HostCheck.True(!Feed(ref value, Service) && value.IsService);
        }
        byte one = (byte)'C';
        for (int mutation = 0; mutation < 9; mutation++)
        {
            var value = LinuxAppStartupObservation.ForService(); bool accepted;
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
                case 6: HostCheck.True(Feed(ref value, Service)); accepted = value.RecordBind(Binder, false); break;
                case 7: HostCheck.True(Feed(ref value, Service)); accepted = value.RecordBind(0, true); break;
                default:
                    HostCheck.True(Feed(ref value, Service) && value.RecordBind(Binder, true));
                    accepted = value.RecordBind(Binder, true); break;
            }
            HostCheck.True(!accepted && value.Failed && value.IsService && !value.SuccessfulBind);
            HostCheck.True(!Feed(ref value, Service) && !value.StartupComplete && value.IsService);
        }
    }

    [HostTest("nativeaot.service.startup both explicit modes retain original prefix writer bind and buffer semantics", HostLane.Fast, 2)]
    private static void BothModes()
    {
        foreach (bool mode in new[] { false, true })
        {
            var value = mode ? LinuxAppStartupObservation.ForService() : default;
            string prefix = mode ? Service : Smoke;
            byte[] bytes = Encoding.ASCII.GetBytes(prefix);
            fixed (byte* source = bytes)
                for (int i = 0; i < bytes.Length; i++)
                    HostCheck.True(value.Observe(Writer, source + i, 1));
            HostCheck.Equal(mode, value.IsService); HostCheck.Equal(4U, value.ObservedStages);
            HostCheck.Equal(Writer, value.WriterIdentity); HostCheck.True(!value.SuccessfulBind);
            HostCheck.True(Feed(ref value, mode ? Smoke : Service, Binder + 1)); // Later stderr is outside the prefix contract.
            HostCheck.True(value.RecordBind(Binder, true) && value.SuccessfulBind);
            HostCheck.Equal(mode, value.IsService); HostCheck.Equal(Binder, value.BindIdentity);
            HostCheck.True(!value.RecordBind(Binder, true) && value.Failed && !value.SuccessfulBind);
            HostCheck.Equal(mode, value.IsService); HostCheck.Equal(4U, value.ObservedStages);
            HostCheck.Equal(Writer, value.WriterIdentity); HostCheck.Equal(Binder, value.BindIdentity);

            var bounded = mode ? LinuxAppStartupObservation.ForService() : default;
            byte[] maximum = Encoding.ASCII.GetBytes(prefix + new string('x', 8192 - prefix.Length));
            fixed (byte* source = maximum)
            {
                HostCheck.True(bounded.Observe(Writer, source, 8192) && bounded.StartupComplete);
                HostCheck.True(bounded.RecordBind(Binder, true));
                HostCheck.True(!bounded.Observe(Writer, source, 8193) && bounded.Failed);
            }
            HostCheck.Equal(mode, bounded.IsService);
        }
    }

    [HostTest("nativeaot.service.startup service factory and both observer modes allocate no managed memory", HostLane.Fast, 3)]
    private static void Allocation()
    {
        var warm = LinuxAppStartupObservation.ForService(); HostCheck.True(Feed(ref warm, Service));
        warm = default; HostCheck.True(Feed(ref warm, Smoke));
        byte[] serviceBytes = Encoding.ASCII.GetBytes(Service), smokeBytes = Encoding.ASCII.GetBytes(Smoke);
        bool accepted = true; long before = GC.GetAllocatedBytesForCurrentThread();
        fixed (byte* service = serviceBytes) fixed (byte* smoke = smokeBytes)
            for (int i = 0; i < 1000; i++)
            {
                var value = LinuxAppStartupObservation.ForService();
                accepted &= value.IsService && value.Observe(Writer, service, (uint)serviceBytes.Length) &&
                    value.RecordBind(Binder, true) && value.SuccessfulBind;
                value = default;
                accepted &= !value.IsService && value.Observe(Writer, smoke, (uint)smokeBytes.Length) &&
                    value.RecordBind(Binder, true) && value.SuccessfulBind;
            }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(accepted); HostCheck.Equal(0L, allocated);
    }

    private static bool Feed(ref LinuxAppStartupObservation value, string text, ulong writer = Writer)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        fixed (byte* source = bytes) return value.Observe(writer, source, (uint)bytes.Length);
    }
}
