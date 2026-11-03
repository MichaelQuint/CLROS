using System.Net;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using OS.Kernel.Compatibility.LinuxX64;
using System.Runtime.CompilerServices;

internal static partial class NativeAotDiskTimingPeerTests
{
    private const string StatusJson = "{\"profile\":\"v1\",\"ready\":true,\"message\":\"CLROS ready — café\"}";

    [HostTest("short diagnostic JSON requires the exact status DTO unique fields and JSON media type")]
    private static void DiagnosticJson()
    {
        NativeAotDiskTimingPeer.InspectStatusJson(Encoding.UTF8.GetBytes(StatusJson), "application/json");
        NativeAotDiskTimingPeer.InspectStatusJson(Encoding.UTF8.GetBytes(StatusJson.Replace("—", "\\u2014").Replace("é", "\\u00E9")), "application/json");
        foreach (string body in new[] { "", "{}", "[]", "null", "{", StatusJson + "x",
            StatusJson.Replace("true", "false"), StatusJson.Replace("v1", "v2"), StatusJson.Replace("café", "cafe"),
            StatusJson.Replace("\"profile\":\"v1\"", "\"profile\":\"v1\",\"profile\":\"v1\""),
            StatusJson.Replace("\"ready\":true,", ""), StatusJson.Replace("{", "{\"extra\":0,") })
        {
            bool refused = false;
            try
            {
                NativeAotDiskTimingPeer.InspectStatusJson(Encoding.UTF8.GetBytes(body), "application/json");
            }
            catch (ToolFailure failure) { refused = failure.Reason == "disk_timing_json_response"; }
            HostCheck.True(refused, body);
        }
        foreach (string? mediaType in new string?[] { null, "text/plain", "application/octet-stream" })
        {
            bool refused = false;
            try
            {
                NativeAotDiskTimingPeer.InspectStatusJson(Encoding.UTF8.GetBytes(StatusJson), mediaType);
            }
            catch (ToolFailure failure) { refused = failure.Reason == "disk_timing_json_response"; }
            HostCheck.True(refused);
        }
    }

    [HostTest("dispatch window partitions completed envelopes and supervisor polls without subtracting nested polls twice")]
    private static void DispatchPartition()
    {
        LinuxThreadServices.DiskDispatchMeasurements m = default;
        ulong first = 0, second = 0;
        LinuxThreadServices.DiskDispatchTotals t = default;
        HostCheck.True(m.Arm(100, 50) && m.BeginDispatch(110, 53, out first));
        HostCheck.True(m.BeginPoll() && m.EndPoll() && m.EndDispatch(first, 150, 63));
        HostCheck.True(m.BeginDispatch(160, 67, out second) && second != first && m.EndDispatch(second, 180, 71));
        HostCheck.True(m.Freeze(200, 75) && m.Inspect(out t));
        Partition(t, 100, 25, 60, 14, 2, 0, false);
    }

    [HostTest("dispatch window clips either boundary and one envelope crossing both only once with frozen delayed-return totals")]
    private static void DispatchClips()
    {
        ulong first = 0, last = 0, across = 0;
        LinuxThreadServices.DiskDispatchTotals s = default, e = default, b = default;
        LinuxThreadServices.DiskDispatchMeasurements start = default;
        HostCheck.True(start.BeginDispatch(90, 0, out first) && start.Arm(100, 0) && start.EndDispatch(first, 130, 6));
        HostCheck.True(!start.Inspect(out _) && start.Freeze(150, 10) && start.Inspect(out s));
        Partition(s, 50, 10, 30, 6, 0, 1, false);

        LinuxThreadServices.DiskDispatchMeasurements end = default;
        HostCheck.True(end.Arm(100, 0) && end.BeginDispatch(120, 3, out last) && end.Freeze(160, 8) && end.Inspect(out e));
        Partition(e, 60, 8, 40, 5, 0, 1, true);
        HostCheck.True(end.IsPhysicalDispatchActive);
        HostCheck.True(end.EndDispatch(last, 999, 8) && !end.IsPhysicalDispatchActive && end.Inspect(out e));
        Partition(e, 60, 8, 40, 5, 0, 1, true);
        HostCheck.True(!end.EndDispatch(last, 1000, 8) && !end.Inspect(out e));
        Partition(e, 60, 8, 40, 5, 0, 1, true); // Late bookkeeping fault revokes completion, preserves frozen numbers.

        LinuxThreadServices.DiskDispatchMeasurements both = default;
        HostCheck.True(both.BeginDispatch(90, 0, out across) && both.Arm(100, 0) && both.Freeze(150, 10) && both.Inspect(out b));
        Partition(b, 50, 10, 50, 10, 0, 1, true);
        HostCheck.True(both.EndDispatch(across, 155, 10) && both.Inspect(out b));
        Partition(b, 50, 10, 50, 10, 0, 1, true);
    }

    [HostTest("dispatch window half-open zero-length boundaries add no envelope and missing freeze never completes")]
    private static void DispatchHalfOpen()
    {
        LinuxThreadServices.DiskDispatchMeasurements m = default;
        ulong sequence = 0;
        LinuxThreadServices.DiskDispatchTotals t = default;
        HostCheck.True(m.Arm(100, 0) && m.BeginDispatch(150, 2, out sequence) && !m.Inspect(out _));
        HostCheck.True(m.Freeze(150, 2) && m.Inspect(out t));
        Partition(t, 50, 2, 0, 0, 0, 0, true);
        HostCheck.True(m.EndDispatch(sequence, 200, 2) && m.Inspect(out t));
        Partition(t, 50, 2, 0, 0, 0, 0, true);
        LinuxThreadServices.DiskDispatchMeasurements zero = default;
        HostCheck.True(zero.Arm(1000, 2) && zero.Freeze(1000, 2) && zero.Inspect(out t));
        Partition(t, 0, 0, 0, 0, 0, 0, false);
    }

    [HostTest("dispatch window rejects overlapping wrong or missing ends poll-crossing boundaries rollback overflow and impossible partitions")]
    private static void DispatchRejections()
    {
        ulong sequence = 0;
        LinuxThreadServices.DiskDispatchMeasurements overlap = default;
        HostCheck.True(overlap.BeginDispatch(10, 0, out _) && !overlap.BeginDispatch(11, 0, out _) && !overlap.IsComplete);
        LinuxThreadServices.DiskDispatchMeasurements wrong = default;
        HostCheck.True(wrong.Arm(10, 0) && wrong.BeginDispatch(11, 0, out sequence) && !wrong.EndDispatch(sequence + 1, 12, 0));
        LinuxThreadServices.DiskDispatchMeasurements poll = default;
        HostCheck.True(poll.BeginPoll() && !poll.Arm(10, 0) && !poll.IsComplete);
        poll = default;
        HostCheck.True(poll.Arm(10, 0) && poll.BeginPoll() && !poll.Freeze(20, 0));
        poll = default;
        HostCheck.True(!poll.EndPoll());
        LinuxThreadServices.DiskDispatchMeasurements time = default;
        HostCheck.True(time.Arm(10, 10) && !time.Freeze(9, 10));
        time = default;
        HostCheck.True(time.Arm(10, 10) && !time.Freeze(20, 9));
        time = default;
        HostCheck.True(time.Arm(10, 0) && !time.Freeze(20, 11));
        time = default;
        HostCheck.True(time.Arm(10, 0) && time.BeginDispatch(11, 0, out sequence) && !time.EndDispatch(sequence, 12, 2));
        LinuxThreadServices.DiskDispatchMeasurements counter = default;
        HostCheck.True(counter.Arm(0, 0) && counter.BeginDispatch(1, 0, out sequence));
        CompletedDispatchCount(ref counter) = ulong.MaxValue;
        HostCheck.True(!counter.EndDispatch(sequence, 2, 0));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_completed")]
    private static extern ref ulong CompletedDispatchCount(ref LinuxThreadServices.DiskDispatchMeasurements value);

    [HostTest("dispatch child partitions completed nested driver and shell nonpoll time without double subtraction")]
    private static void DispatchChildPartition()
    {
        LinuxThreadServices.DiskDispatchMeasurements parent = default, child = default;
        ulong outer = 0, inner = 0;
        HostCheck.True(parent.BeginDispatch(80, 48, out outer) && child.BeginDispatch(90, 49, out inner));
        HostCheck.True(parent.Arm(100, 50) && child.Arm(100, 50));
        HostCheck.True(child.EndDispatch(inner, 130, 56) && parent.EndDispatch(outer, 150, 60));
        HostCheck.True(parent.BeginDispatch(160, 65, out outer) && child.BeginDispatch(170, 66, out inner));
        HostCheck.True(child.EndDispatch(inner, 180, 68) && parent.EndDispatch(outer, 190, 69));
        HostCheck.True(child.Freeze(200, 75) && parent.Freeze(200, 75));
        ChildPartition(parent, child, 32, 34); // D80/PD14, V40/PV8; parent nonpoll66.
    }

    [HostTest("dispatch child clips both window boundaries and preserves partition across valid late physical returns")]
    private static void DispatchChildClips()
    {
        LinuxThreadServices.DiskDispatchMeasurements parent = default, child = default;
        ulong outer = 0, inner = 0;
        HostCheck.True(parent.BeginDispatch(90, 48, out outer) && child.BeginDispatch(95, 49, out inner));
        HostCheck.True(parent.Arm(100, 50) && child.Arm(100, 50));
        HostCheck.True(child.Freeze(160, 60) && parent.Freeze(160, 60));
        ChildPartition(parent, child, 50, 0);
        HostCheck.True(child.EndDispatch(inner, 170, 60) && parent.EndDispatch(outer, 180, 60));
        ChildPartition(parent, child, 50, 0);
        HostCheck.True(!child.EndDispatch(inner, 190, 60));
        ChildRejected(parent, child); // A late bookkeeping fault still revokes completeness.
        HostCheck.True(parent.Inspect(out var totals));
        Partition(totals, 60, 10, 60, 10, 0, 1, true);
    }

    [HostTest("dispatch child requires exact original window and successful freeze with zero failure outputs")]
    private static void DispatchChildWindowRejections()
    {
        var parent = FrozenDispatch(100, 50, 60, 10);
        var child = FrozenDispatch(100, 50, 40, 6);
        LinuxThreadServices.DiskDispatchMeasurements empty = default, armed = default;
        HostCheck.True(armed.Arm(100, 50));
        ChildRejected(parent, empty);
        ChildRejected(empty, child);
        ChildRejected(parent, armed);
        ChildRejected(armed, child);
        ChildRejected(parent, FrozenDispatch(101, 50, 40, 6)); // Equal duration, different start.
        ChildRejected(parent, FrozenDispatch(100, 51, 40, 6)); // Equal poll delta, different base.
        ChildRejected(parent, FrozenDispatch(100, 50, 40, 6, 99, 20));
        ChildRejected(parent, FrozenDispatch(100, 50, 40, 6, 100, 19));
        var failed = child;
        HostCheck.True(!failed.BeginPoll());
        ChildRejected(parent, failed);
        failed = parent;
        HostCheck.True(!failed.EndDispatch(1, 201, 70));
        ChildRejected(failed, child);
        ChildPartition(parent, child, 34, 16); // Rejections do not mutate the valid pair.
    }

    [HostTest("dispatch child rejects impossible subsets without clamping and conserves zero and maximum unsigned totals")]
    private static void DispatchChildSubsetBoundaries()
    {
        var parent = FrozenDispatch(100, 50, 60, 10);
        ChildRejected(parent, FrozenDispatch(100, 50, 61, 10)); // V>D.
        ChildRejected(parent, FrozenDispatch(100, 50, 40, 11)); // PV>PD.
        ChildRejected(FrozenDispatch(100, 50, 60, 15), FrozenDispatch(100, 50, 55, 5)); // Shell poll10 exceeds shell5.
        LinuxThreadServices.DiskDispatchMeasurements invalid = default;
        ulong sequence = 0;
        HostCheck.True(invalid.Arm(100, 50) && invalid.BeginDispatch(100, 50, out sequence));
        HostCheck.True(!invalid.EndDispatch(sequence, 110, 65)); // A child cannot freeze V<PV.
        ChildRejected(parent, invalid);
        var zero = FrozenDispatch(0, 0, 0, 0, 0, 0);
        ChildPartition(zero, zero, 0, 0); // A frozen empty window is distinct from default.
        parent = FrozenDispatch(0, 0, ulong.MaxValue, 0, ulong.MaxValue, 0);
        var child = FrozenDispatch(0, 0, ulong.MaxValue - 1, 0, ulong.MaxValue, 0);
        ChildPartition(parent, child, ulong.MaxValue - 1, 1);
        parent = FrozenDispatch(0, 0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        child = FrozenDispatch(0, 0, 0, 0, ulong.MaxValue, ulong.MaxValue);
        ChildPartition(parent, child, 0, 0);
    }

    private static LinuxThreadServices.DiskDispatchMeasurements FrozenDispatch(ulong start, ulong pollStart,
        ulong dispatch, ulong nestedPoll, ulong phase = 100, ulong poll = 20)
    {
        LinuxThreadServices.DiskDispatchMeasurements value = default;
        ulong sequence = 0;
        HostCheck.True(value.Arm(start, pollStart) && value.BeginDispatch(start, pollStart, out sequence) &&
            value.EndDispatch(sequence, start + dispatch, pollStart + nestedPoll) && value.Freeze(start + phase, pollStart + poll));
        return value;
    }

    private static void ChildPartition(in LinuxThreadServices.DiskDispatchMeasurements parent,
        in LinuxThreadServices.DiskDispatchMeasurements child, ulong expectedDriver, ulong expectedShell)
    {
        HostCheck.True(parent.InspectChild(in child, out ulong driver, out ulong shell));
        HostCheck.Equal(expectedDriver, driver);
        HostCheck.Equal(expectedShell, shell);
        HostCheck.True(parent.Inspect(out var totals));
        HostCheck.Equal(totals.DispatchNonPollNanoseconds, driver + shell);
    }

    private static void ChildRejected(in LinuxThreadServices.DiskDispatchMeasurements parent,
        in LinuxThreadServices.DiskDispatchMeasurements child)
    {
        ulong driver = ulong.MaxValue, shell = ulong.MaxValue;
        HostCheck.True(!parent.InspectChild(in child, out driver, out shell));
        HostCheck.Equal(0UL, driver);
        HostCheck.Equal(0UL, shell);
    }

    private static void Partition(LinuxThreadServices.DiskDispatchTotals t, ulong phase, ulong poll, ulong dispatch, ulong nested, ulong completed, ulong clipped, bool active)
    {
        HostCheck.True(t.PhaseNanoseconds == phase && t.PollNanoseconds == poll && t.DispatchNanoseconds == dispatch &&
            t.NestedPollNanoseconds == nested && t.CompletedEnvelopes == completed && t.ClippedEnvelopes == clipped && t.PhysicalDispatchActiveAtFreeze == active);
        HostCheck.Equal(t.PhaseNanoseconds, t.PollNanoseconds + t.DispatchNonPollNanoseconds + t.SupervisorNonPollNanoseconds);
    }

    private const string MetricsV4 = "M10_018_TIMING bytes=65536 calls=8 steps=32 pumpNs=3200 scanNs=3200 flushNs=3200 preNetworkNs=800 postNetworkNs=800 postReadinessNs=300 readinessRefreshNs=100 readinessWaitScanNs=200 postWaitRecordNs=500 networkCalls=8 diskReady=1 otherReady=0 noAdvance=0 elapsed=0 stepLimit=7 failures=0 flushedPastBudget=0";
    private const string Metrics = MetricsV4 + " pollNs=11200 diskRoundNs=9600 clockCalls=8 clockNs=11200 clockPreNs=800 clockDiskNs=9600 clockPostNs=800" +
        " syscallCalls=0 syscallNs=0 syscallPreNs=0 syscallDiskNs=0 syscallPostNs=0" +
        " supervisorCalls=0 supervisorNs=0 supervisorPreNs=0 supervisorDiskNs=0 supervisorPostNs=0" +
        " idleCalls=0 idleNs=0 idlePreNs=0 idleDiskNs=0 idlePostNs=0\r\n";
    private const string Dispatch = "M10_018_DISPATCH bytes=65536 phaseNs=20000 pollNs=11200 dispatchNs=16000 nestedPollNs=10000 dispatchNonPollNs=6000 supervisorNonPollNs=2800 completed=2 clipped=2 physicalActive=1 phaseComplete=1\r\n";
    private const string Driver = "M10_018_DRIVER bytes=65536 phaseNs=20000 pollNs=11200 driverNs=12000 nestedPollNs=8000 driverNonPollNs=4000 dispatchShellNonPollNs=2000 completed=2 clipped=2 physicalActive=1 phaseComplete=1\r\n";
    private const string Verification = "M10_018_VERIFY calls=3 elapsedNs=3000 phaseNs=20000 phaseComplete=1\r\n";

    [HostTest("driver aggregate conserves complete dispatch and rejects missing malformed widened or negative child partitions")]
    private static void DriverAggregate()
    {
        var polls = NativeAotDiskTimingPeer.InspectMetrics(Metrics);
        var dispatch = NativeAotDiskTimingPeer.InspectDispatchMetrics(Dispatch, polls);
        var driver = NativeAotDiskTimingPeer.InspectDriverMetrics(Driver, dispatch);
        HostCheck.Equal(6000UL, driver["driverNonPollNs"] + driver["dispatchShellNonPollNs"]);
        foreach (string bad in new[] { "", Driver + Driver, Driver.Replace("bytes=65536", "bytes=65535"),
            Driver.Replace("phaseNs=20000", "phaseNs=20001"), Driver.Replace("pollNs=11200", "pollNs=11201"),
            Driver.Replace("driverNs=12000", "driverNs=16001"), Driver.Replace("nestedPollNs=8000", "nestedPollNs=10001"),
            Driver.Replace("driverNs=12000", "driverNs=6000"),
            Driver.Replace("driverNs=12000", "driverNs=15000"), // Remaining poll2000 exceeds shell1000.
            Driver.Replace("driverNonPollNs=4000", "driverNonPollNs=3999"),
            Driver.Replace("dispatchShellNonPollNs=2000", "dispatchShellNonPollNs=2001"),
            Driver.Replace("completed=2", "completed=02"), Driver.Replace("clipped=2", "clipped=3"),
            Driver.Replace("physicalActive=1", "physicalActive=0"), Driver.Replace("phaseComplete=1", "phaseComplete=0"),
            Driver.Replace("driverNs=12000", "driverNs=18446744073709551616"),
            Driver.Replace(" nestedPollNs=8000", ""), Driver.Replace("clipped=2", "clipped=2 clipped=2") })
        {
            bool rejected = false;
            try
            {
                _ = NativeAotDiskTimingPeer.InspectDriverMetrics(bad, dispatch);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, bad);
        }
        bool missingParent = false;
        try
        {
            _ = NativeAotDiskTimingPeer.InspectDriverMetrics(Driver, new Dictionary<string, ulong>());
        }
        catch (ToolFailure) { missingParent = true; }
        HostCheck.True(missingParent);
    }

    [HostTest("driver aggregate preserves zero child and maximum unsigned difference conservation without overflow")]
    private static void DriverAggregateEdges()
    {
        var dispatch = NativeAotDiskTimingPeer.InspectDispatchMetrics(Dispatch, NativeAotDiskTimingPeer.InspectMetrics(Metrics));
        string zero = Driver.Replace("driverNs=12000", "driverNs=0").Replace("nestedPollNs=8000", "nestedPollNs=0")
            .Replace("driverNonPollNs=4000", "driverNonPollNs=0").Replace("dispatchShellNonPollNs=2000", "dispatchShellNonPollNs=6000");
        HostCheck.Equal(6000UL, NativeAotDiskTimingPeer.InspectDriverMetrics(zero, dispatch)["dispatchShellNonPollNs"]);
        var maximum = dispatch.ToDictionary(pair => pair.Key, pair => pair.Value);
        maximum["phaseNs"] = maximum["dispatchNs"] = maximum["dispatchNonPollNs"] = ulong.MaxValue;
        maximum["pollNs"] = maximum["nestedPollNs"] = maximum["supervisorNonPollNs"] = 0;
        string row = Driver.Replace("phaseNs=20000", "phaseNs=18446744073709551615").Replace("pollNs=11200", "pollNs=0")
            .Replace("driverNs=12000", "driverNs=18446744073709551614").Replace("nestedPollNs=8000", "nestedPollNs=0")
            .Replace("driverNonPollNs=4000", "driverNonPollNs=18446744073709551614").Replace("dispatchShellNonPollNs=2000", "dispatchShellNonPollNs=1");
        HostCheck.Equal(1UL, NativeAotDiskTimingPeer.InspectDriverMetrics(row, maximum)["dispatchShellNonPollNs"]);
    }

    [HostTest("dispatch aggregate requires separate complete bounded conservation matching unchanged v5 polls and explicit active physical dispatch")]
    private static void DispatchAggregate()
    {
        var polls = NativeAotDiskTimingPeer.InspectMetrics(Metrics);
        HostCheck.Equal(20000UL, NativeAotDiskTimingPeer.InspectDispatchMetrics(Metrics + Dispatch, polls)["phaseNs"]);
        foreach (string bad in new[] { Metrics, Dispatch + Dispatch, Dispatch.Replace("phaseNs=20000", "phaseNs=0"),
            Dispatch.Replace("pollNs=11200", "pollNs=11201"), Dispatch.Replace("nestedPollNs=10000", "nestedPollNs=11201"),
            Dispatch.Replace("dispatchNs=16000", "dispatchNs=20001"), Dispatch.Replace("dispatchNonPollNs=6000", "dispatchNonPollNs=5999"),
            Dispatch.Replace("supervisorNonPollNs=2800", "supervisorNonPollNs=2801"), Dispatch.Replace("completed=2", "completed=02"),
            Dispatch.Replace("clipped=2", "clipped=3"), Dispatch.Replace("physicalActive=1", "physicalActive=0"),
            Dispatch.Replace("phaseComplete=1", "phaseComplete=0"), Dispatch.Replace("phaseNs=20000", "phaseNs=18446744073709551616"),
            Dispatch.Replace(" nestedPollNs=10000", ""), Dispatch.Replace("clipped=2", "clipped=2 clipped=2") })
        {
            bool rejected = false;
            try
            {
                _ = NativeAotDiskTimingPeer.InspectDispatchMetrics(bad, polls);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, bad);
        }
    }

    [HostTest("timing aggregate rejects duplicates overflow missing and nonexclusive yield reasons")]
    private static void Aggregate()
    {
        HostCheck.Equal(210, NativeAotDiskTimingPeer.PeerTimeoutSeconds(240));
        HostCheck.Equal(450, NativeAotDiskTimingPeer.PeerTimeoutSeconds(480));
        HostCheck.Equal(480, NativeAotDiskTimingPeer.PeerTimeoutSeconds(600));
        foreach (int timeout in new[] { 239, 601 })
        {
            bool denied = false;
            try
            {
                _ = NativeAotDiskTimingPeer.PeerTimeoutSeconds(timeout);
            }
            catch (ToolFailure) { denied = true; }
            HostCheck.True(denied);
        }
        HostCheck.Equal(32UL, NativeAotDiskTimingPeer.InspectMetrics("unrelated\n" + Metrics)["steps"]);
        foreach (string bad in new[]{"",Metrics+Metrics,Metrics.Replace("calls=8","calls=18446744073709551616"),
            Metrics.Replace("calls=8","calls=08"),Metrics.Replace("stepLimit=7","stepLimit=8"),
            Metrics.Replace("failures=0","failures=1"),Metrics.Replace("steps=32","steps=33"),
            Metrics.Replace("networkCalls=8","networkCalls=7"),Metrics.Replace("bytes=65536","bytes=65535"),
            Metrics.Replace(" flushedPastBudget=0",""),Metrics.Replace("postReadinessNs=300","postReadinessNs=299"),
            Metrics.Replace("postWaitRecordNs=500","postWaitRecordNs=501"),Metrics.Replace(" postReadinessNs=300",""),
            Metrics.Replace("postWaitRecordNs=500","postWaitRecordNs=-1"),Metrics.Replace("postReadinessNs=300","postReadinessNs=300 postReadinessNs=300"),
            Metrics.Replace("postReadinessNs=300","postReadinessNs=18446744073709551615").Replace("postWaitRecordNs=500","postWaitRecordNs=1")})
        {
            bool rejected = false;
            try
            {
                _ = NativeAotDiskTimingPeer.InspectMetrics(bad);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, bad);
        }
    }

    [HostTest("timing post-network partition conserves parent duration at zero and maximum unsigned boundaries")]
    private static void PostNetworkPartition()
    {
        foreach (var parts in new (string Parent, string Ready, string Wait)[]{("0","0","0"),("800","0","800"),("800","800","0"),
            ("18446744073709551615","18446744073709551614","1"),("18446744073709551615","0","18446744073709551615")})
        {
            string row = Metrics.Replace("postNetworkNs=800", "postNetworkNs=" + parts.Parent)
                .Replace("postReadinessNs=300", "postReadinessNs=" + parts.Ready).Replace("postWaitRecordNs=500", "postWaitRecordNs=" + parts.Wait)
                .Replace("readinessRefreshNs=100", "readinessRefreshNs=" + parts.Ready).Replace("readinessWaitScanNs=200", "readinessWaitScanNs=0")
                .Replace("preNetworkNs=800", "preNetworkNs=0").Replace("diskRoundNs=9600", "diskRoundNs=0")
                .Replace("pollNs=11200", "pollNs=" + parts.Parent).Replace("clockNs=11200", "clockNs=" + parts.Parent)
                .Replace("clockPreNs=800", "clockPreNs=0").Replace("clockDiskNs=9600", "clockDiskNs=0")
                .Replace("clockPostNs=800", "clockPostNs=" + parts.Parent);
            var values = NativeAotDiskTimingPeer.InspectMetrics(row);
            HostCheck.Equal(values["postNetworkNs"], values["postReadinessNs"] + values["postWaitRecordNs"]);
        }
        string oldV3 = MetricsV4.Replace("readinessRefreshNs=100 readinessWaitScanNs=200 ", "");
        string oldV2 = oldV3.Replace("postReadinessNs=300 postWaitRecordNs=500 ", "")
            .Replace("flushedPastBudget=0", "flushedPastBudget=0 replenished=0 replenishBudgetSkips=0");
        foreach (string original in new[] { MetricsV4, oldV3, oldV2 })
        {
            bool rejected = false;
            try
            {
                _ = NativeAotDiskTimingPeer.InspectMetrics(original);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, "original v2/v3/v4 producers remain distinct from the v5 cause partition");
        }
    }

    [HostTest("timing poll causes conserve counts whole elapsed and three stages without borrowing empty or overflowing buckets")]
    private static void PollCauses()
    {
        // Redistribute the same measured intervals across all four causes.
        // No independent pump/scan/flush sum is required to equal the WHOLE
        // disk round: those are subintervals of an overlapping parent.
        foreach (string cause in new[] { "clock", "syscall", "supervisor", "idle" })
        {
            string row = Metrics;
            if (cause != "clock")
                row = row.Replace("clockCalls=8", "clockCalls=0").Replace("clockNs=11200", "clockNs=0")
                    .Replace("clockPreNs=800", "clockPreNs=0").Replace("clockDiskNs=9600", "clockDiskNs=0").Replace("clockPostNs=800", "clockPostNs=0")
                    .Replace(cause + "Calls=0", cause + "Calls=8").Replace(cause + "Ns=0", cause + "Ns=11200")
                    .Replace(cause + "PreNs=0", cause + "PreNs=800").Replace(cause + "DiskNs=0", cause + "DiskNs=9600").Replace(cause + "PostNs=0", cause + "PostNs=800");
            var values = NativeAotDiskTimingPeer.InspectMetrics(row);
            HostCheck.Equal(values["networkCalls"], values[cause + "Calls"]);
            HostCheck.Equal(values["pollNs"], values[cause + "Ns"]);
        }
        string mixed = Metrics.Replace("clockCalls=8", "clockCalls=2").Replace("clockNs=11200", "clockNs=2800")
            .Replace("clockPreNs=800", "clockPreNs=200").Replace("clockDiskNs=9600", "clockDiskNs=2400").Replace("clockPostNs=800", "clockPostNs=200");
        foreach (string cause in new[] { "syscall", "supervisor", "idle" })
            mixed = mixed.Replace(cause + "Calls=0", cause + "Calls=2").Replace(cause + "Ns=0", cause + "Ns=2800")
                .Replace(cause + "PreNs=0", cause + "PreNs=200").Replace(cause + "DiskNs=0", cause + "DiskNs=2400").Replace(cause + "PostNs=0", cause + "PostNs=200");
        HostCheck.Equal(11200UL, NativeAotDiskTimingPeer.InspectMetrics(mixed)["pollNs"]);
        foreach (string bad in new[]{Metrics.Replace("clockCalls=8","clockCalls=7"),Metrics.Replace("clockCalls=8","clockCalls=0"),
            Metrics.Replace("clockNs=11200","clockNs=11199"),Metrics.Replace("diskRoundNs=9600","diskRoundNs=9599"),
            Metrics.Replace("pollNs=11200","pollNs=11201"),Metrics.Replace(" clockDiskNs=9600",""),
            Metrics.Replace("idleCalls=0","idleCalls=0 idleCalls=0"),Metrics.Replace("idleCalls=0","idleCalls=1"),
            Metrics.Replace("clockDiskNs=9600","clockDiskNs=18446744073709551615"),
            Metrics.Replace("idleNs=0","idleNs=18446744073709551615").Replace("idlePreNs=0","idlePreNs=18446744073709551615")
                .Replace("idleCalls=0","idleCalls=1").Replace("clockCalls=8","clockCalls=7")})
        {
            bool rejected = false;
            try
            {
                _ = NativeAotDiskTimingPeer.InspectMetrics(bad);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected, bad);
        }
    }

    [HostTest("timing readiness children conserve parent and reject missing duplicated regressed and overflowing durations")]
    private static void ReadinessPartition()
    {
        foreach (var parts in new (string Refresh, string Scan)[] { ("0", "300"), ("300", "0"), ("100", "200") })
        {
            string row = Metrics.Replace("readinessRefreshNs=100", "readinessRefreshNs=" + parts.Refresh)
                .Replace("readinessWaitScanNs=200", "readinessWaitScanNs=" + parts.Scan);
            var values = NativeAotDiskTimingPeer.InspectMetrics(row);
            HostCheck.Equal(values["postReadinessNs"], values["readinessRefreshNs"] + values["readinessWaitScanNs"]);
        }
        foreach (string bad in new[]{Metrics.Replace("readinessRefreshNs=100","readinessRefreshNs=99"),
            Metrics.Replace("readinessWaitScanNs=200","readinessWaitScanNs=201"),Metrics.Replace(" readinessRefreshNs=100",""),
            Metrics.Replace("readinessWaitScanNs=200","readinessWaitScanNs=-1"),
            Metrics.Replace("readinessRefreshNs=100","readinessRefreshNs=100 readinessRefreshNs=100"),
            Metrics.Replace("readinessRefreshNs=100","readinessRefreshNs=18446744073709551615").Replace("readinessWaitScanNs=200","readinessWaitScanNs=1")})
        {
            bool rejected = false;
            try
            {
                _ = NativeAotDiskTimingPeer.InspectMetrics(bad);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    // Real loopback HTTP with synthetic runner/serial and independent image
    // extraction tests ownership and byte validation; it is not guest evidence.
    [HostTest("short timing peer checks actual bytes metadata metrics and joins deliberate cancellation")]
    private static void Lifecycle() => WebRootImageFixtureTests.WithImage((root, dir, image, source) =>
    {
        byte[] core = Enumerable.Range(0, 70001).Select(i => (byte)(i * 37 ^ (i >> 9))).ToArray();
        File.WriteAllBytes(Path.Combine(source, "_framework", "System.Private.CoreLib.wasm"), core);
        string applied = Path.Combine(dir, "timing.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), applied);
        var inventory = WebRootDiskInventory.Inspect(applied, File.ReadAllBytes(applied), receipt);
        foreach (string mode in new[]{"good","body-timeout","json","json-type","body","etag","metrics","dispatch","driver","driver-duplicate","driver-late-change","verification","verification-duplicate","verification-late-change","selection","selection-late-change","late-fault","unconfirmed",
            "evidence-trace", "evidence-summary", "evidence-image-alias", "evidence-crc", "evidence-missing-end", "evidence-late-row", "evidence-unselected"})
            Check(mode).GetAwaiter().GetResult();

        async Task Check(string mode)
        {
            string serial = Path.Combine(root, "artifacts", "tests", "disk-timing-" + Guid.NewGuid().ToString("N") + ".serial.log");
            Directory.CreateDirectory(Path.GetDirectoryName(serial)!);
            bool exited = false;
            bool bodyStarted = false, bodyTimedOut = false;
            bool evidenceSelected = mode.StartsWith("evidence-", StringComparison.Ordinal) && mode != "evidence-unselected";
            bool succeeds = mode is "good" or "evidence-trace" or "evidence-summary" or "evidence-image-alias";
            bool payload = mode != "evidence-summary";
            byte[] evidenceImage = evidenceSelected || mode == "evidence-unselected" ? File.ReadAllBytes(applied) : [];
            var evidence = evidenceImage.Length != 0 ? HostDiskDiagnosticFixture.Create(evidenceImage,
                inventory.Find(WebRootDiskInventory.Root + NativeAotDiskTimingPeer.Target)!, payload) :
                (Prefix: "", BeforeMetrics: "", AfterMetrics: "", Rows: 0, Sectors: 0);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            ToolFailure? failure = null;
            try
            {
                var result = mode == "body-timeout"
                    ? await NativeAotDiskTimingPeer.RunAsync(root, serial, Stub, inventory,
                        new NativeAotDiskTimingPeer.BenchmarkOptions { RequestTimeout = TimeSpan.FromSeconds(1) }, 600, deadline.Token)
                    : evidenceSelected
                        ? await NativeAotDiskTimingPeer.RunAsync(root, serial, Stub, inventory,
                            new NativeAotDiskTimingPeer.BenchmarkOptions
                            {
                                DiskEvidence = payload ? NativeAotDiskTimingPeer.DiskEvidenceMode.Trace : NativeAotDiskTimingPeer.DiskEvidenceMode.Summary,
                                DiskImageBytes = evidenceImage
                            }, 600, deadline.Token)
                        : await NativeAotDiskTimingPeer.RunAsync(root, serial, Stub, inventory, 600, deadline.Token);
                HostCheck.True(succeeds && result.Runner.Cancelled && result.Runner.TerminationConfirmed && exited);
                int starts = 0;
                bool denied = false;
                try
                {
                    _ = await NativeAotDiskTimingPeer.RunAsync(root, serial, (_, _) => { starts++; throw new InvalidOperationException(); }, inventory, 600, deadline.Token);
                }
                catch (ToolFailure error) { denied = error.Reason == "disk_timing_fresh_artifacts"; }
                HostCheck.True(denied && starts == 0);
            }
            catch (ToolFailure error) { failure = error; }
            catch (OperationCanceledException) when (mode == "body-timeout" && !deadline.IsCancellationRequested)
            {
                bodyTimedOut = true;
            }
            if (mode == "body-timeout")
                HostCheck.True(exited && bodyStarted && bodyTimedOut && failure is null,
                    "streamed body must expire after headers and partial data, before the outer watchdog");
            else
                HostCheck.True(exited && (succeeds ? failure is null : failure is not null), mode + " " + failure?.Message);
            using var actual = JsonDocument.Parse(File.ReadAllBytes(serial + ".disk-timing/diagnostic.json"));
            HostCheck.Equal(succeeds, actual.RootElement.GetProperty("Complete").GetBoolean());
            HostCheck.True(!actual.RootElement.GetProperty("FullAndCleanupQualified").GetBoolean() &&
                actual.RootElement.GetProperty("RunnerJoined").GetBoolean());
            HostCheck.Equal("clros-disk-read-timing-v11", actual.RootElement.GetProperty("Schema").GetString()!);
            HostCheck.Equal(evidenceSelected ? payload ? "Trace" : "Summary" : "Original",
                actual.RootElement.GetProperty("DiskEvidenceMode").GetString()!);
            if (mode is "evidence-trace" or "evidence-summary" or "evidence-image-alias")
            {
                var disk = actual.RootElement.GetProperty("DiskEvidence");
                HostCheck.Equal(evidence.Rows, disk.GetProperty("Rows").GetInt32());
                HostCheck.Equal(evidence.Sectors, disk.GetProperty("CopiedSectors").GetInt32());
                HostCheck.Equal(payload, disk.GetProperty("PayloadIncluded").GetBoolean());
                HostCheck.Equal(0UL, disk.GetProperty("Cancelled").GetUInt64());
            }
            HostCheck.Equal((mode == "body-timeout" ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(180)).Ticks,
                actual.RootElement.GetProperty("RequestTimeoutTicks").GetInt64());
            HostCheck.Equal("headers-and-streamed-body", actual.RootElement.GetProperty("RequestTimeoutScope").GetString()!);
            if (mode == "body-timeout")
            {
                HostCheck.Equal(0, actual.RootElement.GetProperty("Responses").GetArrayLength());
                var incomplete = actual.RootElement.GetProperty("IncompleteRequest");
                HostCheck.Equal("body", incomplete.GetProperty("Phase").GetString()!);
                HostCheck.Equal(1L, incomplete.GetProperty("Bytes").GetInt64());
                var runner = actual.RootElement.GetProperty("Runner");
                HostCheck.True(runner.GetProperty("Cancelled").GetBoolean() && runner.GetProperty("TerminationConfirmed").GetBoolean());
            }
            HostCheck.Equal("json-health-head-range-health", actual.RootElement.GetProperty("RequestSequence").GetString()!);
            HostCheck.Equal("clros-thread-dispatch-selection-v1", actual.RootElement.GetProperty("SelectionSchema").GetString()!);
            HostCheck.True(actual.RootElement.GetProperty("ClockFrequency").GetInt64() > 0);
            foreach (var response in actual.RootElement.GetProperty("Responses").EnumerateArray())
            {
                long started = response.GetProperty("Started").GetInt64();
                long headers = response.GetProperty("HeadersReceived").GetInt64();
                long received = response.GetProperty("BodyReceived").GetInt64();
                long written = response.GetProperty("BodyWritten").GetInt64();
                long flushed = response.GetProperty("BodyFlushed").GetInt64();
                long finished = response.GetProperty("Finished").GetInt64();
                HostCheck.True(started <= headers && headers <= received && received <= written && written <= flushed && flushed <= finished,
                    "network completion must precede saved body and durable-flush completion");
            }
            if (succeeds)
            {
                HostCheck.Equal(5, actual.RootElement.GetProperty("Responses").GetArrayLength());
                HostCheck.Equal(4, actual.RootElement.GetProperty("Selections").GetArrayLength());
                HostCheck.Equal(NativeAotDiskTimingPeer.VerificationSchema, actual.RootElement.GetProperty("VerificationSchema").GetString()!);
                HostCheck.Equal(3000UL, actual.RootElement.GetProperty("VerificationMetrics").GetProperty("elapsedNs").GetUInt64());
            }
            if (mode is "good" or "late-fault" or "driver-late-change")
            {
                HostCheck.Equal(1UL, actual.RootElement.GetProperty("DispatchMetrics").GetProperty("phaseComplete").GetUInt64());
                HostCheck.Equal(4000UL, actual.RootElement.GetProperty("DriverMetrics").GetProperty("driverNonPollNs").GetUInt64());
            }

            async Task<ProcessResult> Stub(int port, CancellationToken cancellation)
            {
                using HttpListener listener = new();
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
                File.WriteAllText(serial, "M10_018_LISTENING main=1\r\n");
                if (evidenceSelected || mode == "evidence-unselected")
                    File.AppendAllText(serial, evidence.Prefix);
                var entry = inventory.Find(WebRootDiskInventory.Root + NativeAotDiskTimingPeer.Target)!;
                if (mode == "evidence-image-alias")
                    evidenceImage[checked((int)(entry.Sectors[0].Lba * 512))] ^= 1;
                long modified = DateTimeOffset.FromUnixTimeSeconds(entry.ModifiedSeconds).ToFileTime();
                string etag = "\"" + (modified ^ entry.Bytes).ToString("x") + "\"";
                try
                {
                    for (int i = 0; ; i++)
                    {
                        var context = await listener.GetContextAsync().WaitAsync(cancellation);
                        bool head = context.Request.HttpMethod == "HEAD", range = context.Request.Headers["Range"] is not null;
                        bool asset = context.Request.RawUrl == NativeAotDiskTimingPeer.Target;
                        bool json = context.Request.RawUrl == "/api/status";
                        HostCheck.True(i switch
                        {
                            0 => json,
                            1 or 4 => context.Request.RawUrl == "/healthz",
                            2 => asset && head,
                            3 => asset && range,
                            _ => false
                        });
                        byte[] entity = json ? Encoding.UTF8.GetBytes(mode == "json" ? StatusJson.Replace("true", "false") : StatusJson) : asset ? range ? core[..65536] : [] : "OS_MINIMAL_API_OK"u8.ToArray();
                        if (mode == "body" && range)
                            entity[1] ^= 1;
                        context.Response.StatusCode = range ? 206 : 200;
                        context.Response.ContentLength64 = head ? core.Length : entity.Length;
                        context.Response.ContentType = json && mode != "json-type" ? "application/json" : asset ? "application/wasm" : "text/plain";
                        if (asset)
                            context.Response.Headers["ETag"] = mode == "etag" ? "\"wrong\"" : etag;
                        if (range)
                            context.Response.Headers["Content-Range"] = "bytes 0-65535/" + core.Length;
                        if (mode == "body-timeout")
                        {
                            await context.Response.OutputStream.WriteAsync(entity.AsMemory(0, 1), cancellation);
                            await context.Response.OutputStream.FlushAsync(cancellation);
                            bodyStarted = true;
                            await Task.Delay(Timeout.Infinite, cancellation);
                        }
                        if (!head)
                            await context.Response.OutputStream.WriteAsync(entity, cancellation);
                        context.Response.Close();
                        if (i == 4 && (evidenceSelected || mode == "evidence-unselected"))
                        {
                            string observations = evidence.BeforeMetrics;
                            if (mode == "evidence-crc")
                            {
                                int start = observations.IndexOf(" crc32=", StringComparison.Ordinal) + 7;
                                int end = observations.IndexOf(' ', start);
                                uint crc = uint.Parse(observations[start..end], System.Globalization.CultureInfo.InvariantCulture);
                                observations = observations[..start] + (crc ^ 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + observations[end..];
                            }
                            if (mode == "evidence-missing-end")
                                observations = observations[..observations.IndexOf("CLROS_DISK_IO_END_V1 ", StringComparison.Ordinal)];
                            File.AppendAllText(serial, observations);
                        }
                        if (i == 4)
                            File.AppendAllText(serial, (mode == "metrics" ? Metrics + Metrics : Metrics) +
                            (mode == "dispatch" ? Dispatch.Replace("nestedPollNs=10000", "nestedPollNs=20000") : Dispatch) +
                            (mode == "driver" ? Driver.Replace("dispatchShellNonPollNs=2000", "dispatchShellNonPollNs=2001") :
                                mode == "driver-duplicate" ? Driver + Driver : Driver) +
                            (mode == "verification" ? Verification.Replace("elapsedNs=3000", "elapsedNs=20001") :
                                mode == "verification-duplicate" ? Verification + Verification : Verification) +
                            (evidenceSelected || mode == "evidence-unselected" ? evidence.AfterMetrics : "") +
                             (mode == "selection" ? SelectionStream.Replace("sequence=2", "sequence=02") : SelectionStream));
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    if (mode == "late-fault")
                        File.AppendAllText(serial, "TEST_FAIL disk.dispatch.bookkeeping late_end\r\n");
                    if (mode == "driver-late-change")
                        File.AppendAllText(serial, Driver);
                    if (mode == "verification-late-change")
                        File.AppendAllText(serial, Verification);
                    if (mode == "selection-late-change")
                        File.AppendAllText(serial, SelectionStream);
                    if (mode == "evidence-late-row")
                        File.AppendAllText(serial, "CLROS_DISK_IO_V1 kind=0\r\n");
                    return new(-1, "synthetic-runner-cancelled", "", false, false, true, TimeSpan.Zero)
                    {
                        TerminationConfirmed = mode != "unconfirmed"
                    };
                }
                finally { exited = true; listener.Stop(); }
            }
        }
    });

    [HostTest("short timing peer rejects invalid request budgets before artifacts or runner creation")]
    private static void InvalidRequestBudgets()
    {
        foreach (var timeout in new[] { TimeSpan.Zero, Timeout.InfiniteTimeSpan, TimeSpan.FromSeconds(181), TimeSpan.MaxValue })
        {
            int starts = 0;
            bool rejected = false;
            try
            {
                _ = NativeAotDiskTimingPeer.RunAsync(".", "unused.serial", (_, _) =>
                {
                    starts++;
                    throw new InvalidOperationException("invalid budget started a runner");
                }, null!, new NativeAotDiskTimingPeer.BenchmarkOptions { RequestTimeout = timeout }).GetAwaiter().GetResult();
            }
            catch (ToolFailure error) when (error.Reason == "disk_timing_request_timeout")
            {
                rejected = true;
            }
            HostCheck.True(rejected && starts == 0);
        }
    }
}
