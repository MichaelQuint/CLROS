using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using Owner = LinuxSignalServicesTests.Owner;

internal static unsafe class LinuxSignalPendingTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Input = Base + 0x1200, Output = Base + 0x1300;
    private static void Action(Owner o,uint signal,ulong handler = Base + 0x180)
    { o.Action(handler,0x04000004); HostCheck.Equal(0L,o.Call(13,signal,Input,0,8)); }
    private static void Mask(Owner o,ulong value)
    { o.Thread.Put64(Input,value); HostCheck.Equal(0L,o.Call(14,2,Input,0,8)); }
    private static long Send(Owner o,uint tid,uint signal) => o.Call(234,101,tid,signal);
    private static Signals.Delivery Deliver(Owner o) => o.Thread.Service->DeliverPendingSignals(Key,2);
    private static uint Delivered(Owner o)
    {
        HostCheck.Equal(Signals.Delivery.Delivered,Deliver(o));
        var image = o.Thread.CurrentImage(); ulong frame = image->Frame.Rsp;
        uint signal = o.Thread.Read32(frame + 312);
        HostCheck.Equal(101U,o.Thread.Read32(frame + 328)); HostCheck.Equal(unchecked((uint)-6),o.Thread.Read32(frame + 320));
        image->Frame.Rsp = frame + 8; HostCheck.Equal(LinuxSyscallAction.RestoreUser,o.Thread.Call(15).Action);
        return signal;
    }

    [HostTest("linux.signal-pending tgkill validates exact Linux int arguments process identity and null probes")]
    private static void Identities()
    {
        using Owner o = new(); o.Running();
        HostCheck.Equal(0L,o.Call(234,101,101,0)); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
        foreach (ulong group in new[] { 0UL,0xffffffffUL }) HostCheck.Equal(-22L,o.Call(234,group,101,0));
        foreach (ulong tid in new[] { 0UL,0xffffffffUL }) HostCheck.Equal(-22L,o.Call(234,101,tid,0));
        HostCheck.Equal(-3L,o.Call(234,102,101,0)); HostCheck.Equal(-3L,o.Call(234,101,102,0));
        HostCheck.Equal(-3L,o.Call(234,101,102,65)); // Linux validates the signal after finding its target.
        HostCheck.Equal(-22L,o.Call(234,101,101,65)); HostCheck.Equal(-22L,o.Call(234,101,101,0xffffffff));
        HostCheck.Equal(0L,o.Call(234,0x100000065,0x100000065,0x100000000));
        HostCheck.Equal(Signals.Delivery.None,Deliver(o));
    }

    [HostTest("linux.signal-pending standard coalescing synchronous priority and ascending selection retain genuine siginfo and return frames")]
    private static void Standard()
    {
        using Owner o = new(); o.Running(); foreach (uint signal in new[] { 8U,10U,12U,31U }) Action(o,signal); Mask(o,0);
        HostCheck.Equal(0L,Send(o,101,12)); HostCheck.Equal(0L,Send(o,101,10)); HostCheck.Equal(0L,Send(o,101,10));
        HostCheck.Equal(0L,Send(o,101,31)); HostCheck.Equal(0L,Send(o,101,8));
        HostCheck.Equal(8U,Delivered(o)); HostCheck.Equal(31U,Delivered(o));
        HostCheck.Equal(10U,Delivered(o)); HostCheck.Equal(12U,Delivered(o)); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
    }

    [HostTest("linux.signal-pending realtime quota preserves repeated events priority and freed queue capacity")]
    private static void Realtime()
    {
        using Owner o = new(); o.Running(); foreach (uint signal in new[] { 10U,32U,34U,35U,64U }) Action(o,signal); Mask(o,0);
        foreach (uint signal in new[] { 35U,34U,35U,64U }) HostCheck.Equal(0L,Send(o,101,signal));
        byte[] before = new ReadOnlySpan<byte>(o.State,sizeof(Signals.State)).ToArray();
        HostCheck.Equal(-11L,Send(o,101,32)); HostCheck.True(new ReadOnlySpan<byte>(o.State,sizeof(Signals.State)).SequenceEqual(before));
        HostCheck.Equal(0L,Send(o,101,10)); HostCheck.Equal(10U,Delivered(o)); HostCheck.Equal(34U,Delivered(o));
        HostCheck.Equal(0L,Send(o,101,32));
        foreach (uint signal in new[] { 32U,35U,35U,64U }) HostCheck.Equal(signal,Delivered(o));
        HostCheck.Equal(Signals.Delivery.None,Deliver(o));
    }

    [HostTest("linux.signal-pending blocked standard realtime and ignored signals survive until unmask or action change")]
    private static void MasksAndIgnored()
    {
        using Owner o = new(); o.Running(); Action(o,10); Action(o,34); Mask(o,(1UL << 9) | (1UL << 33));
        HostCheck.Equal(0L,Send(o,101,34)); HostCheck.Equal(0L,Send(o,101,10)); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
        Mask(o,0); HostCheck.Equal(10U,Delivered(o)); HostCheck.Equal(34U,Delivered(o));
        Action(o,10,1); HostCheck.Equal(0L,Send(o,101,10)); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
        Mask(o,1UL << 9); HostCheck.Equal(0L,Send(o,101,10)); Action(o,10); Mask(o,0);
        HostCheck.Equal(10U,Delivered(o)); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
        foreach (uint signal in new[] { 17U,18U,23U,28U }) HostCheck.Equal(0L,Send(o,101,signal));
        HostCheck.Equal(Signals.Delivery.None,Deliver(o));
    }

    [HostTest("linux.signal-pending new ignored action flushes every actual thread before old-action copyout EFAULT")]
    private static void ActionFlush()
    {
        using Owner o = new(); o.Running(); var m = o.Thread; Action(o,10); Action(o,34); Mask(o,0);
        var parent = m.Kernel.Table->Current; var child = m.Clone(); uint childTid = m.View(child).Tid;
        foreach (uint tid in new[] { 101U,childTid }) foreach (uint signal in new[] { 10U,34U }) HostCheck.Equal(0L,Send(o,tid,signal));
        o.Action(1); m.FailWrite(Output); HostCheck.Equal(-14L,o.Call(13,10,Input,Output,8)); m.FailWrite(ulong.MaxValue);
        Action(o,34,1); Action(o,10); Action(o,34); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
        m.Yield(parent); HostCheck.Equal(child.Value,m.Kernel.Dispatch().Value); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
    }

    [HostTest("linux.signal-pending actual generation exit and reuse discard private queues without recycling Linux TIDs")]
    private static void Generation()
    {
        using Owner o = new(); o.Running(); var m = o.Thread; Action(o,10); Action(o,34); Mask(o,0);
        var parent = m.Kernel.Table->Current; var child = m.Clone(); uint tid = m.View(child).Tid;
        HostCheck.Equal(0L,Send(o,tid,10)); HostCheck.Equal(0L,Send(o,tid,34)); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
        m.Yield(parent); HostCheck.Equal(child.Value,m.Kernel.Dispatch().Value); o.ExitCurrent();
        HostCheck.Equal(parent.Value,m.Kernel.Dispatch().Value); var reused = m.Clone(); uint newTid = m.View(reused).Tid;
        HostCheck.True((uint)reused.Value == (uint)child.Value && reused.Value != child.Value && newTid != tid);
        HostCheck.Equal(-3L,Send(o,tid,0)); HostCheck.Equal(-3L,Send(o,tid,10)); HostCheck.Equal(0L,Send(o,newTid,0));
        m.Yield(parent); HostCheck.Equal(reused.Value,m.Kernel.Dispatch().Value); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
    }

    [HostTest("linux.signal-pending real interruption wins once while masked waits retain a directed signal")]
    private static void Blocked()
    {
        foreach (bool masked in new[] { false,true })
        {
            using Owner o = new(); o.Running(); var m = o.Thread; Action(o,10); Mask(o,masked ? 1UL << 9 : 0);
            var parent = m.Kernel.Table->Current; var child = m.Clone();
            m.Put32(Input,7); HostCheck.Equal(LinuxSyscallAction.Blocked,m.Call(202,Input,128,7).Action);
            HostCheck.True(m.Service->CommitFutexWait(Key,2,m.Kernel.Capture(parent),out bool handoff) && handoff); m.Kernel.Confirm();
            HostCheck.Equal(child.Value,m.Kernel.Dispatch().Value); HostCheck.Equal(0L,Send(o,101,10));
            HostCheck.Equal(masked ? KernelThreadTable.State.Waiting : KernelThreadTable.State.Ready,m.Kernel.Inspect(parent).State);
            HostCheck.Equal(masked ? 1L : 0L,o.Call(202,Input,129,1)); m.Yield(child); HostCheck.Equal(parent.Value,m.Kernel.Dispatch().Value);
            var completion = m.Service->CompleteFutexWait(Key,2); HostCheck.True(completion.Action == LinuxSyscallAction.Return && completion.Value == (masked ? 0 : -4));
            if (masked) { HostCheck.Equal(Signals.Delivery.None,Deliver(o)); Mask(o,0); }
            else HostCheck.True(!m.Service->CurrentImage(Key,2,out _));
            HostCheck.Equal(10U,Delivered(o)); HostCheck.Equal(Signals.Delivery.None,Deliver(o));
        }
    }

    [HostTest("linux.signal-pending unsupported job control fails without queue and fatal default stages owned shutdown")]
    private static void Defaults()
    {
        using Owner o = new(); o.Running(); Mask(o,0);
        foreach (uint signal in new[] { 19U,20U,21U,22U }) HostCheck.Equal(-38L,Send(o,101,signal));
        HostCheck.Equal(Signals.Delivery.None,Deliver(o)); Action(o,20); HostCheck.Equal(0L,Send(o,101,20)); HostCheck.Equal(20U,Delivered(o));
        HostCheck.Equal(0L,Send(o,101,12)); HostCheck.Equal(Signals.Delivery.Terminate,Deliver(o));
        var view = o.Thread.View(o.Thread.Kernel.Table->Current); HostCheck.True(view.Exiting && view.ExitCode == 140);
    }

    [HostTest("linux.signal-pending SIGKILL stages actual process shutdown instead of waiting behind masks or queued handlers")]
    private static void Kill()
    {
        using Owner o = new(); o.Running(); Action(o,10); Mask(o,ulong.MaxValue); var m = o.Thread;
        var child = m.Clone(); HostCheck.Equal(0L,Send(o,101,10));
        var result = m.Call(234,101,m.View(child).Tid,9);
        HostCheck.True(result.Action == LinuxSyscallAction.Terminate && result.ExitCode == 137 &&
            m.View(child).Exiting && m.View(child).ExitCode == 137 && m.View(m.Kernel.Table->Current).Exiting);
    }

    [HostTest("linux.signal-pending gateway routes tgkill without control mutation and delivery authenticates actual ownership")]
    private static void GatewayAndOwner()
    {
        using Owner o = new();
        HostCheck.Equal(Signals.Delivery.Rejected,Deliver(o)); o.Running(); var m = o.Thread; Action(o,10); Mask(o,0);
        HostCheck.Equal(Signals.Delivery.Rejected,m.Service->DeliverPendingSignals(Key + 1,2));
        HostCheck.Equal(Signals.Delivery.Rejected,m.Service->DeliverPendingSignals(Key,0x202));
        LinuxThreadServices copy = *m.Service; HostCheck.Equal(Signals.Delivery.Rejected,copy.DeliverPendingSignals(Key,2));
        byte* scratch = stackalloc byte[64]; LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active,LinuxSyscallGateway.Begin(ref gate,new()
        { UserRoot = m.Kernel.Owner.Root,MaximumCalls = 64,MaximumWrite = 64,Scratch = scratch,ScratchBytes = 64,
            FirmwareExited = true,OwnedBoundaryValidated = true,EnableStartup = true,EnableDescriptors = true,EnableThreads = true },m.GatewayAccess));
        var frame = m.CurrentImage()->Frame; frame.Number = 234; frame.Argument0 = frame.Argument1 = 101; frame.Argument2 = 10; var original = frame;
        var result = LinuxSyscallGateway.Dispatch(ref gate,ref frame);
        HostCheck.True(result.Action == LinuxSyscallAction.Return && result.Value == 0 && gate.Calls == 1 && gate.Status == LinuxSyscallGateStatus.Active &&
            new ReadOnlySpan<byte>(&frame,96).SequenceEqual(new ReadOnlySpan<byte>(&original,96)));
        HostCheck.Equal(10U,Delivered(o));
        using Owner disabled = new(attach:false); disabled.Running(); HostCheck.Equal(-38L,disabled.Call(234,101,101,0));
        HostCheck.Equal(Signals.Delivery.Rejected,Deliver(disabled));
    }
}
