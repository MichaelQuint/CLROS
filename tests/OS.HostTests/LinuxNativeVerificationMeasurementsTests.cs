using System.Runtime.CompilerServices;
using OS.Tooling;
using Measurements = OS.Kernel.Compatibility.LinuxX64.LinuxThreadServices.DiskVerificationMeasurements;

internal static class LinuxNativeVerificationMeasurementsTests
{
    [HostTest("verification timing sums only complete disjoint calls inside its frozen phase")]
    private static void Totals()
    {
        Measurements m = default;
        HostCheck.True(!m.Inspect(out ulong calls, out ulong ns) && calls == 0 && ns == 0);
        HostCheck.True(m.Arm(100) && m.Begin(105) && m.End(115) && m.Begin(120) && m.End(140) && m.Freeze(150));
        HostCheck.True(m.Inspect(out calls, out ns) && calls == 2 && ns == 30);
        HostCheck.True(m.Inspect(out calls, out ns) && calls == 2 && ns == 30);
        m = default;
        HostCheck.True(m.Arm(0) && m.Begin(0) && m.End(0) && m.Freeze(0));
        HostCheck.True(m.Inspect(out calls, out ns) && calls == 1 && ns == 0);
        m = default;
        HostCheck.True(m.Arm(0) && m.Begin(0) && m.End(ulong.MaxValue) && m.Freeze(ulong.MaxValue));
        HostCheck.True(m.Inspect(out calls, out ns) && calls == 1 && ns == ulong.MaxValue);
    }

    [HostTest("verification timing rejects nesting unpaired events and calls crossing phase boundaries")]
    private static void Lifetime()
    {
        Measurements m = default;
        HostCheck.True(!m.Begin(0) && !m.Arm(0));
        Rejected(m);
        m = default;
        HostCheck.True(!m.End(0));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(0) && !m.Arm(0));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(0) && !m.End(0));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(0) && m.Begin(1) && !m.Begin(2));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(0) && m.Begin(1) && !m.Freeze(2));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(0) && m.Freeze(0) && !m.Begin(1));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(0));
        m.Invalidate();
        HostCheck.True(!m.Freeze(1));
        Rejected(m);
    }

    [HostTest("verification timing rejects regressing clocks and checked count time and phase overflow")]
    private static void Bounds()
    {
        Measurements m = default;
        HostCheck.True(m.Arm(100) && !m.Begin(99));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(100) && m.Begin(110) && !m.End(109));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(100) && !m.Freeze(99));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(0) && m.Begin(0));
        Calls(ref m) = ulong.MaxValue;
        HostCheck.True(!m.End(0));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(0) && m.Begin(0));
        Elapsed(ref m) = ulong.MaxValue;
        HostCheck.True(!m.End(1));
        Rejected(m);
        m = default;
        HostCheck.True(m.Arm(100) && m.Begin(100));
        Elapsed(ref m) = 9;
        HostCheck.True(!m.End(101));
        Rejected(m);
    }

    [HostTest("verification aggregate parser binds exact phase and rejects missing duplicate malformed or excessive totals")]
    private static void Protocol()
    {
        const string row = "M10_018_VERIFY calls=3 elapsedNs=30 phaseNs=100 phaseComplete=1\r\n";
        var phase = new Dictionary<string, ulong> { ["phaseNs"] = 100 };
        var result = NativeAotDiskTimingPeer.InspectVerificationMetrics(row, phase);
        HostCheck.True(result["calls"] == 3 && result["elapsedNs"] == 30);
        foreach (string invalid in new[] { "", row + row, row.Replace("calls=3", "calls=0"),
            row.Replace("calls=3", "calls=03"), row.Replace("elapsedNs=30", "elapsedNs=101"),
            row.Replace("phaseNs=100", "phaseNs=99"), row.Replace("phaseComplete=1", "phaseComplete=0"),
            row.Replace(" elapsedNs=30", ""), row.Replace("elapsedNs=30", "elapsedNs=-1"),
            row.Replace("calls=3", "calls=18446744073709551616"), row.Replace("calls=3", "calls=3 calls=3") })
        {
            bool rejected = false;
            try
            {
                _ = NativeAotDiskTimingPeer.InspectVerificationMetrics(invalid, phase);
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected, invalid);
        }
    }

    private static void Rejected(Measurements value) =>
        HostCheck.True(!value.Inspect(out ulong calls, out ulong ns) && calls == 0 && ns == 0);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_calls")]
    private static extern ref ulong Calls(ref Measurements value);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_elapsed")]
    private static extern ref ulong Elapsed(ref Measurements value);
}
