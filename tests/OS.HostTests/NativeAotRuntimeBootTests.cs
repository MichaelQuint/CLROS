using OS.Boot;
using OS.Kernel.Diagnostics;

internal static unsafe class NativeAotRuntimeBootTests
{
    [HostTest("nativeaot.runtime-boot new destructive leaves default off and append exact catalog definitions")]
    private static void Definitions()
    {
        HostCheck.True(!Probes.EnableM09011Probe && !Probes.EnableM09012Probe && !Probes.EnableM16010Probe);
        var expected = new[]
        {
            (Probes.GetM09011Probe(), "thread.nativeaot-suspension", nameof(Probes.EnableM09011Probe)),
            (Probes.GetM09012Probe(), "thread.nativeaot-threadpool", nameof(Probes.EnableM09012Probe)),
            (Probes.GetM16010Probe(), "app.nativeaot-runtime", nameof(Probes.EnableM16010Probe))
        };
        HostCheck.True(Probes.TryGetProbe(59, out var parent) && parent.Name == nameof(Probes.EnableM16009Probe));
        foreach (var entry in expected)
        {
            HostCheck.Equal(entry.Item2, entry.Item1.Group);
            HostCheck.Equal(entry.Item3, entry.Item1.Name);
            HostCheck.True(!entry.Item1.EnabledByDefault && entry.Item1.Risk == ProbeRisk.Destructive && entry.Item1.Description.Length != 0);
            int matches = 0;
            for (int index = 0; index < Probes.ProbeCatalogCount; index++)
            {
                HostCheck.True(Probes.TryGetProbe(index, out var definition));
                if (definition.Name == entry.Item3)
                {
                    matches++;
                    HostCheck.Equal(entry.Item2, definition.Group);
                    HostCheck.True(!definition.EnabledByDefault);
                }
            }
            HostCheck.Equal(1, matches);
        }
        HostCheck.True(!Probes.TryGetProbe(Probes.ProbeCatalogCount, out _));
    }

    [HostTest("nativeaot.runtime-boot default-off gates reject owned and failed admission without invoking callbacks")]
    private static void NoInvocation()
    {
        int calls = 0;
        for (int leaf = 0; leaf < 3; leaf++)
        {
            for (int state = 0; state < 16; state++)
                HostCheck.True(!Run(leaf, (state & 1) != 0, (state & 2) != 0, (state & 4) != 0,
                    (state & 8) != 0, &calls, &Record));
            HostCheck.True(!Run(leaf, true, true, true, true, null, &Record));
            HostCheck.True(!Run(leaf, true, true, true, true, &calls, null));
            HostCheck.Equal(0, calls);
        }
    }

    private static bool Run(int leaf, bool startup, bool stopped, bool revoked, bool apic,
        void* context, delegate*<void*, bool> callback) => leaf switch
        {
            0 => BootSequence.RunOwnedNativeAotSuspension(startup, stopped, revoked, apic, context, callback),
            1 => BootSequence.RunOwnedNativeAotThreadPool(startup, stopped, revoked, apic, context, callback),
            2 => BootSequence.RunOwnedNativeAotRuntime(startup, stopped, revoked, apic, context, callback),
            _ => false
        };
    private static bool Record(void* context)
    {
        if (context != null)
            (*(int*)context)++;
        return true;
    }
}
