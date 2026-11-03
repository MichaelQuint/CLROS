using System.Text;
using OS.Tooling;

internal static class NativeAotWebSignalReplayTests
{
    // Independent scalar rows over the existing synthetic web owner. No
    // prepared ELF, siginfo bytes, process or guest execution is supplied.
    private const ulong Usr2 = 1UL << 11, Activation = 1UL << 34, Frame = 0x6ffffb78, Stack = 0x6ffffff0;
    private static void Register(NativeAotThreadingTranscriptTests.Vector v) => v.Call(13, 0, [12, 0x500400, 0, 8]);
    private static void Mask(NativeAotThreadingTranscriptTests.Vector v, ulong mask)
    {
        v.Main.Mask = mask;
        v.Call(14, 0, [2, 0x500400, 0, 8]);
    }
    private static void Enter(NativeAotThreadingTranscriptTests.Vector v, ulong mask, ulong frame = Frame)
    {
        v.Main.Frame = frame;
        v.Main.Depth = 1;
        v.Main.Mask = mask;
        v.Main.Rsp = frame - 64;
        v.Call(186, 1);
    }
    private static void Return(NativeAotThreadingTranscriptTests.Vector v, ulong mask = 0)
    {
        v.Main.Rsp = v.Main.Frame + 8;
        v.Main.Depth = v.Main.Frame = 0;
        v.Main.Mask = mask;
        v.Call(15, 0, action: 5, returnedRsp: Stack);
        v.Main.Rsp = Stack;
    }
    private static void Reject(Action action)
    {
        bool failed = false;
        try
        {
            action();
        }
        catch (ToolFailure) { failed = true; }
        HostCheck.True(failed);
    }

    [HostTest("web signal12 scalar replay joins directed registered delivery and real frame return")]
    private static void Directed()
    {
        var report = NativeAotWebReplayTests.InspectProbe(v =>
        {
            Register(v);
            v.Call(234, 0, [1, 1, 12]);
            Enter(v, Usr2);
            Return(v);
            v.Call(234, 0, [1, 1, 0]);
        });
        HostCheck.Equal(1, report.Protocol.SignalReturns);
        HostCheck.Equal(2, report.Active);
    }

    [HostTest("web blocked standard signal12 coalesces without consuming realtime35 queue")]
    private static void CoalescingAndPriority()
    {
        var report = NativeAotWebReplayTests.InspectProbe(v =>
        {
            Register(v);
            Mask(v, Usr2 | Activation);
            v.Call(234, 0, [1, 1, 35]);
            v.Call(234, 0, [1, 1, 12]);
            v.Call(234, 0, [1, 1, 12]);
            v.Call(234, 0, [1, 1, 35]);
            Mask(v, 0);
            Enter(v, Usr2 | Activation);
            Return(v);
            Enter(v, Activation);
            Return(v);
            Enter(v, Activation);
            Return(v);
        });
        HostCheck.Equal(3, report.Protocol.SignalReturns);
        Reject(() => NativeAotWebReplayTests.InspectProbe(v =>
        {
            Register(v);
            Mask(v, Usr2);
            v.Call(234, 0, [1, 1, 12]);
            v.Call(234, 0, [1, 1, 12]);
            Mask(v, 0);
            Enter(v, Usr2);
            Return(v);
            Enter(v, Usr2);
        }));
    }

    [HostTest("web selected activation signal stays latched across IRQ before its first observed handler call")]
    private static void SelectedBeforeIrq()
    {
        var report = NativeAotWebReplayTests.InspectProbe(v =>
        {
            Register(v);
            var actor = v.Clone(3, 3);
            v.Call(234, 0, [1, 1, 35]);
            v.Main.Rsp = Frame - 64;
            v.Irq(30, 2, 3);
            v.Select(actor);
            v.Call(234, 0, [1, 1, 12]);
            v.Wait(0x500210);
            v.Drain(40);
            v.Supervisor(3, 1, 3);
            v.Select(v.Main);
            Enter(v, Usr2 | Activation);
            Return(v);
            Enter(v, Usr2);
            Return(v);
        });
        HostCheck.Equal(2, report.Protocol.SignalReturns);
        HostCheck.Equal(3, report.Active);
    }

    [HostTest("web signal12 rejects missing actions unknown signals stale targets and nonweb requests")]
    private static void ScopeAndRequests()
    {
        Reject(() => NativeAotWebReplayTests.InspectProbe(v => v.Call(234, 0, [1, 1, 12])));
        Reject(() => NativeAotWebReplayTests.InspectProbe(v => { Register(v); v.Call(234, 0, [1, 1, 10]); }));
        Reject(() => NativeAotWebReplayTests.InspectProbe(v => { Register(v); v.Call(234, 0, [1, 99, 12]); }));
        Reject(() => NativeAotWebReplayTests.InspectProbe(v => { Register(v); v.Call(234, 0, [2, 1, 12]); }));
        var ordinary = NativeAotSocketProtocolTests.Vector(policyPrefix: v => { Register(v); v.Call(234, 0, [1, 1, 12]); });
        Reject(() => QemuTranscripts.InspectNativeAotSocketProtocolForTests(false, NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n", ordinary.Rows) + "\r\n"),
            address => address == 0x400200 || address == 0x400202, address => address == 0x400202, 8192, 12288));
    }

    [HostTest("web signal12 frame entry rejects missing pending wrong geometry masked delivery and orphan returns")]
    private static void FrameRefusals()
    {
        Reject(() => NativeAotWebReplayTests.InspectProbe(v => { Register(v); Enter(v, Usr2); }));
        Reject(() => NativeAotWebReplayTests.InspectProbe(v => { Register(v); v.Call(234, 0, [1, 1, 12]); Enter(v, Usr2, Frame - 16); }));
        Reject(() => NativeAotWebReplayTests.InspectProbe(v => { Register(v); v.Call(234, 0, [1, 1, 12]); Enter(v, 0); }));
        Reject(() => NativeAotWebReplayTests.InspectProbe(v =>
        {
            Register(v);
            Mask(v, Usr2);
            v.Call(234, 0, [1, 1, 12]);
            Enter(v, Usr2);
        }));
        Reject(() => NativeAotWebReplayTests.InspectProbe(v =>
        {
            Register(v);
            Mask(v, Usr2 | Activation);
            v.Call(234, 0, [1, 1, 35]);
            v.Call(234, 0, [1, 1, 12]);
            Mask(v, 0);
            Enter(v, Activation);
        }));
        Reject(() => NativeAotWebReplayTests.InspectProbe(v =>
        {
            Register(v);
            v.Call(234, 0, [1, 1, 12]);
            Enter(v, Usr2);
            Return(v);
            v.Main.Rsp = Frame + 8;
            v.Call(15, 0, action: 5, returnedRsp: Stack);
        }));
    }

    [HostTest("web standard pending signal12 does not spend or extend four-entry realtime35 capacity")]
    private static void QueueBounds()
    {
        var report = NativeAotWebReplayTests.InspectProbe(v =>
        {
            Register(v);
            Mask(v, Usr2 | Activation);
            for (int i = 0; i < 4; i++)
                v.Call(234, 0, [1, 1, 35]);
            v.Call(234, -11, [1, 1, 35]);
            for (int i = 0; i < 6; i++)
                v.Call(234, 0, [1, 1, 12]);
            Mask(v, 0);
            Enter(v, Usr2 | Activation);
            Return(v);
            for (int i = 0; i < 4; i++)
            {
                Enter(v, Activation);
                Return(v);
            }
        });
        HostCheck.Equal(5, report.Protocol.SignalReturns);
        Reject(() => NativeAotWebReplayTests.InspectProbe(v =>
        {
            Mask(v, Activation);
            for (int i = 0; i < 5; i++)
                v.Call(234, 0, [1, 1, 35]);
        }));
    }
}
