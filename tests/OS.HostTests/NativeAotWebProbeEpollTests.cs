using OS.Tooling;

internal static class NativeAotWebProbeEpollTests
{
    private const ulong Buffer = 0x500800, Deadline = 1_000_020;
    private static void Setup(NativeAotThreadingTranscriptTests.Vector v)
    {
        v.Call(293, 0, [0x500500, 0x80800]);
        v.Call(291, 9, [0x80000]);
        v.Call(233, 0, [9, 1, 7, 0x500300]);
    }
    private static QemuTranscripts.WebReplayReport Run(Action<NativeAotThreadingTranscriptTests.Vector> action) =>
        NativeAotWebReplayTests.InspectProbe(v =>
{
    Setup(v);
    action(v);
});
    private static void Reject(Action action, string? expected = null)
    {
        string? reason = null;
        try
        {
            action();
        }
        catch (ToolFailure failure)
        {
            reason = failure.Reason;
        }
        HostCheck.True(reason is not null, "fabricated pipe epoll observation admitted");
        if (expected is not null)
            HostCheck.Equal(expected, reason);
    }
    private static void Close(NativeAotThreadingTranscriptTests.Vector v)
    {
        v.Call(3, 0, [9]);
        v.Call(3, 0, [8]);
        v.Call(3, 0, [7]);
    }

    [HostTest("web probe pipe epoll checks registration duplicate modification deletion and readonly targets")]
    private static void Registration()
    {
        HostCheck.Equal(2, Run(v =>
        {
            v.Call(233, -17, [9, 1, 7, 0x500300]);
            v.Call(233, 0, [9, 3, 7, 0x500300]);
            v.Call(233, 0, [9, 2, 7, 0]);
            v.Call(233, -2, [9, 2, 7, 0]);
            v.Call(233, 0, [9, 1, 7, 0x500300]);
            Close(v);
        }).SocketAccepts);
        foreach (ulong target in new ulong[] { 8, 10, ulong.MaxValue })
            Reject(() => Run(v => v.Call(233, 0, [9, 1, target, 0x500300])));
        Reject(() => Run(v => v.Call(233, 0, [9, 3, 7, 1])));
        Reject(() => Run(v => v.Call(233, 0, [9, 1, 7, 0x500300])));
    }

    [HostTest("web probe pipe epoll bounds immediate counts by live occupancy and writer EOF")]
    private static void Immediate()
    {
        HostCheck.Equal(2, Run(v =>
        {
            v.Call(232, 0, [9, Buffer, 1, 0]);
            v.Call(1, 2, [8, Buffer, 2]);
            v.Call(232, 1, [9, Buffer, 1, 0]);
            v.Call(0, 2, [7, Buffer, 2]);
            v.Call(232, 0, [9, Buffer, 1, 0]);
            v.Call(3, 0, [8]);
            v.Call(232, 1, [9, Buffer, 1, 0]);
            v.Call(0, 0, [7, Buffer, 1]);
            v.Call(3, 0, [7]);
            v.Call(232, 0, [9, Buffer, 1, 0]);
            v.Call(3, 0, [9]);
        }).SocketAccepts);
        Reject(() => Run(v => v.Call(232, 1, [9, Buffer, 1, 0])), "socket_epoll_immediate");
        Reject(() => Run(v =>
{
    v.Call(1, 2, [8, Buffer, 2]);
    v.Call(232, 2, [9, Buffer, 2, 0]);
}), "socket_epoll_immediate");
    }

    [HostTest("web probe pipe epoll close and descriptor reuse cannot revive an old watch")]
    private static void Reuse()
    {
        HostCheck.Equal(2, Run(v =>
        {
            v.Call(3, 0, [7]);
            v.Call(2, 7, [0x500200, 0, 0]);
            v.Call(232, 0, [9, Buffer, 1, 0]);
            v.Call(3, 0, [7]);
            v.Call(3, 0, [8]);
            v.Call(293, 0, [0x500500, 0x80800]);
            v.Call(1, 1, [8, Buffer, 1]);
            v.Call(232, 0, [9, Buffer, 1, 0]);
            v.Call(233, 0, [9, 1, 7, 0x500300]);
            v.Call(232, 1, [9, Buffer, 1, 0]);
            v.Call(0, 1, [7, Buffer, 1]);
            Close(v);
        }).SocketAccepts);
        Reject(() => Run(v =>
{
    v.Call(3, 0, [7]);
    v.Call(2, 7, [0x500200, 0, 0]);
    v.Call(232, 1, [9, Buffer, 1, 0]);
}), "socket_epoll_immediate");
        Reject(() => Run(v =>
{
    v.Call(3, 0, [7]);
    v.Call(3, 0, [8]);
    v.Call(293, 0, [0x500500, 0x80800]);
    v.Call(1, 1, [8, Buffer, 1]);
    v.Call(232, 1, [9, Buffer, 1, 0]);
}), "socket_epoll_immediate");
    }

    [HostTest("web mixed epoll preserves opaque socket bounds without granting empty pipe readiness")]
    private static void Mixed()
    {
        HostCheck.Equal(2, Run(v =>
        {
            v.Call(233, 0, [9, 1, 6, 0x500300]);
            v.Call(232, 1, [9, Buffer, 2, 0]);
            Close(v);
        }).SocketAccepts);
        Reject(() => Run(v =>
{
    v.Call(233, 0, [9, 1, 6, 0x500300]);
    v.Call(232, 2, [9, Buffer, 2, 0]);
}), "socket_epoll_immediate");
    }

    private static QemuTranscripts.WebReplayReport Stolen(bool write = true, bool consume = true, bool repark = true) => Run(v =>
    {
        var actor = v.Clone(3, 3);
        v.Call(232, 0, [9, Buffer, 1, 1], 3, 3, Deadline);
        v.Drain(30);
        v.Supervisor(3, 1, 3);
        v.Select(actor);
        if (write)
            v.Call(1, 1, [8, Buffer, 1]);
        v.Rows.Add($"M16_012_WAKE identity={v.Main.Id} sequence=1 completion=1 state=1 clock=30");
        if (write && consume)
            v.Call(0, 1, [7, Buffer, 1]);
        v.Irq(40, 2, 3);
        v.Select(v.Main);
        if (repark)
        {
            v.Rows.Add($"M16_012_REPARK identity={v.Main.Id} previous=1 sequence=2 deadline={Deadline} clock=40");
            v.Main.Sequence = 2;
            v.Supervisor(3, 1, 3);
            v.Select(actor);
            v.Call(1, 1, [8, Buffer, 1]);
            v.Rows.Add($"M16_012_WAKE identity={v.Main.Id} sequence=2 completion=1 state=1 clock=40");
            v.Irq(50, 2, 3);
            v.Select(v.Main);
        }
        v.Rows.Add($"M16_012_COMPLETE identity={v.Main.Id} sequence={(repark ? 2 : 1)} completion=1 valueBits=1 clock={v.Clock}");
        v.Resume(1);
        v.Call(0, 1, [7, Buffer, 1]);
        Close(v);
    });

    [HostTest("web pipe stolen readiness requires empty recheck repark and fresh ready completion")]
    private static void StolenReadiness()
    {
        var report = Stolen();
        HostCheck.True(report.Active == 3 && report.Ready == 1 && report.Protocol.SignalReturns == 0);
    }

    [HostTest("web pipe replay rejects empty wake nonempty repark and consumed immediate completion")]
    private static void StolenRefusals()
    {
        Reject(() => Stolen(write: false), "socket_wake_eligibility");
        Reject(() => Stolen(consume: false), "web_probe_repark_ready");
        Reject(() => Stolen(repark: false), "socket_complete_result");
    }

    private static QemuTranscripts.WebReplayReport DirectedRace(bool skipRepark = false) => Run(v =>
    {
        const ulong frame = 0x6ffffb78, stack = 0x6ffffff0;
        v.Call(13, 0, [12, 0x500400, 0, 8]);
        var actor = v.Clone(3, 3);
        v.Call(232, 0, [9, Buffer, 1, 1], 3, 3, Deadline);
        v.Drain(30);
        v.Supervisor(3, 1, 3);
        v.Select(actor);
        v.Call(1, 1, [8, Buffer, 1]);
        v.Rows.Add($"M16_012_WAKE identity={v.Main.Id} sequence=1 completion=1 state=1 clock=30");
        v.Call(234, 0, [1, 1, 12]);
        v.Call(0, 1, [7, Buffer, 1]);
        v.Irq(40, 2, 3);
        v.Select(v.Main);
        if (skipRepark)
        {
            v.Rows.Add($"M16_012_COMPLETE identity={v.Main.Id} sequence=1 completion=5 valueBits={unchecked((ulong)-4L)} clock=40");
            v.Resume(-4);
            return;
        }
        v.Rows.Add($"M16_012_REPARK identity={v.Main.Id} previous=1 sequence=2 deadline={Deadline} clock=40");
        v.Rows.Add($"M16_012_WAKE identity={v.Main.Id} sequence=2 completion=5 state=3 clock=40");
        v.Main.Sequence = 2;
        v.Supervisor(3, 2, 3);
        v.Select(actor);
        v.Irq(50, 2, 3);
        v.Select(v.Main);
        v.Rows.Add($"M16_012_COMPLETE identity={v.Main.Id} sequence=2 completion=5 valueBits={unchecked((ulong)-4L)} clock=50");
        v.Resume(-4);
        v.Main.Frame = frame;
        v.Main.Depth = 1;
        v.Main.Mask = 1UL << 11;
        v.Main.Rsp = frame - 64;
        v.Call(186, 1);
        v.Main.Rsp = frame + 8;
        v.Main.Frame = v.Main.Depth = v.Main.Mask = 0;
        v.Call(15, -4, action: 5, returnedRsp: stack);
        v.Main.Rsp = stack;
        v.Call(232, 0, [9, Buffer, 1, 1], 3, 3, 1_000_050);
        v.Drain(60);
        v.Supervisor(3, 1, 3);
        v.Select(actor);
        v.Call(1, 1, [8, Buffer, 1]);
        v.Rows.Add($"M16_012_WAKE identity={v.Main.Id} sequence=3 completion=1 state=1 clock=60");
        v.Irq(70, 2, 3);
        v.Select(v.Main);
        v.Rows.Add($"M16_012_COMPLETE identity={v.Main.Id} sequence=3 completion=1 valueBits=1 clock=70");
        v.Resume(1);
        v.Call(0, 1, [7, Buffer, 1]);
        Close(v);
    });

    [HostTest("web directed signal preserves won readiness until empty repark then EINTR return and fresh recovery")]
    private static void DirectedSignalRace()
    {
        var report = DirectedRace();
        HostCheck.True(report.Active == 3 && report.Ready == 1 && report.Protocol.SignalReturns == 1);
        Reject(() => DirectedRace(skipRepark: true));
    }
}
