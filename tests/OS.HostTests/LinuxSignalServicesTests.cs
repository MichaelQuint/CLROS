using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using Model = LinuxThreadServicesTests.Model;

internal static unsafe partial class LinuxSignalServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, SignalKey = 0x7369676e616c6d64;
    private const ulong Base = LinuxThreadServicesTests.UserBase, Input = Base + 0x1200, Output = Base + 0x1300;
    [HostTest("linux.signal-startup admission authenticates retained metadata and rejects copies late attachment and active release")]
    private static void Admission()
    {
        using Owner o = new(attach: false);
        var m = o.Thread;
        Signals.State fresh = default;
        var invalid = o.Plan;
        HostCheck.True(!Signals.Initialize(&fresh, (uint)sizeof(Signals.State) - 1, SignalKey, 2, o.Plan) && !Signals.Initialize(&fresh, (uint)sizeof(Signals.State), 0, 2, o.Plan) && !Signals.Initialize(&fresh, (uint)sizeof(Signals.State), SignalKey, 0x202, o.Plan));
        invalid.Capacity = 0;
        HostCheck.True(!Signals.Initialize(&fresh, (uint)sizeof(Signals.State), SignalKey, 2, invalid));
        invalid = o.Plan;
        invalid.Capacity = 17;
        HostCheck.True(!Signals.Initialize(&fresh, (uint)sizeof(Signals.State), SignalKey, 2, invalid));
        invalid = o.Plan;
        invalid.CopyToUser = null;
        HostCheck.True(!Signals.Initialize(&fresh, (uint)sizeof(Signals.State), SignalKey, 2, invalid));
        invalid = o.Plan;
        invalid.FirmwareExited = false;
        HostCheck.True(!Signals.Initialize(&fresh, (uint)sizeof(Signals.State), SignalKey, 2, invalid));
        HostCheck.True(new ReadOnlySpan<byte>(&fresh, sizeof(Signals.State)).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.True(!m.Service->EnableSignals(Key, 2, (Signals.State*)m.Service) && !m.Service->EnableSignals(Key, 2, (Signals.State*)m.Kernel.Context(0)));
        Signals.State copy = *o.State;
        HostCheck.True(!m.Service->EnableSignals(Key, 2, &copy) && !m.Service->EnableSignals(Key + 1, 2, o.State) && !m.Service->EnableSignals(Key, 0x202, o.State));
        HostCheck.True(!Signals.Initialize(null, 4096, SignalKey, 2, o.Plan) && !Signals.Initialize(o.State, 4096, SignalKey, 2, o.Plan));
        HostCheck.True(m.Service->EnableSignals(Key, 2, o.State) && !m.Service->EnableSignals(Key, 2, o.State));
        HostCheck.True(!Signals.End(o.State, SignalKey, 2) && !Signals.End(&copy, SignalKey, 2));
        o.Running();
        HostCheck.True(!m.Service->EnableSignals(Key, 2, o.State) && !m.Service->Release(Key, 2));
        HostCheck.True(!Signals.End(o.State, SignalKey + 1, 2) && !Signals.End(o.State, SignalKey, 0x202));
    }

    [HostTest("linux.signal-startup process actions use the exact kernel layout shared across actual cloned threads")]
    private static void SharedActions()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        var parent = m.Kernel.Table->Current;
        HostCheck.Equal(32, sizeof(Signals.Disposition));
        HostCheck.Equal(24, sizeof(Signals.AlternateStack));
        o.Action(Base + 0x180, ulong.MaxValue, ulong.MaxValue);
        HostCheck.Equal(0L, o.Call(13, 10, Input, Output, 8));
        HostCheck.True(new ReadOnlySpan<byte>(m.At(Output), 32).IndexOfAnyExcept((byte)0) < 0);
        var child = m.Clone();
        m.Yield(parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, o.Call(13, 10, 0, Output, 8));
        var action = *(Signals.Disposition*)m.At(Output);
        HostCheck.True(action.Handler == Base + 0x180 && action.Restorer == Base + 0x200 && action.Flags == 0xdc000807 && action.Mask == (ulong.MaxValue & ~((1UL << 8) | (1UL << 18))));
        o.Action(1);
        HostCheck.Equal(0L, o.Call(13, 10, Input, 0, 8));
        m.Yield(child);
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, o.Call(13, 10, 0, Output, 8));
        HostCheck.Equal(1UL, m.Read64(Output));
    }

    [HostTest("linux.signal-startup sigaction validates size signal numbers copy ordering and committed old-action EFAULT")]
    private static void ActionFailures()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        o.Action(Base + 0x180);
        uint before = m.Reads;
        HostCheck.Equal(-22L, o.Call(13, 10, Input, 0, 16));
        HostCheck.Equal(before, m.Reads);
        foreach (ulong signal in new[]
        {
            0UL,
            65UL,
            9UL,
            19UL
        }

        )
            HostCheck.Equal(-22L, o.Call(13, signal, Input, 0, 8));
        HostCheck.Equal(0L, o.Call(13, 9, 0, Output, 8));
        HostCheck.Equal(0UL, m.Read64(Output));
        m.FailRead(Input);
        HostCheck.Equal(-14L, o.Call(13, 0, Input, Output, 8));
        m.FailRead(ulong.MaxValue);
        m.FailWrite(Output);
        HostCheck.Equal(-14L, o.Call(13, 10, Input, Output, 8));
        m.FailWrite(ulong.MaxValue);
        HostCheck.Equal(0L, o.Call(13, 0x10000000a, 0, Output, 8));
        HostCheck.Equal(Base + 0x180, m.Read64(Output));
        o.Action(0);
        m.FailRead(Input);
        HostCheck.Equal(-14L, o.Call(13, 10, Input, 0, 8));
        m.FailRead(ulong.MaxValue);
        HostCheck.Equal(0L, o.Call(13, 10, 0, Output, 8));
        HostCheck.Equal(Base + 0x180, m.Read64(Output));
    }

    [HostTest("linux.signal-startup alternate stacks are per generation clear on shared-VM clone and scrub at reap reuse")]
    private static void StackOwnership()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        var parent = m.Kernel.Table->Current;
        HostCheck.Equal(2U, o.ReadStack().Flags);
        o.Stack(Base + 0x3800, 2048);
        HostCheck.Equal(0L, o.Call(131, Input));
        var child = m.Clone();
        m.Yield(parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        var stack = o.ReadStack();
        HostCheck.True(stack.Flags == 2 && stack.Address == 0 && stack.Bytes == 0);
        o.Stack(Base + 0x1800, 2048);
        HostCheck.Equal(0L, o.Call(131, Input));
        o.ExitCurrent();
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        stack = o.ReadStack();
        HostCheck.True(stack.Address == Base + 0x3800 && stack.Bytes == 2048 && stack.Flags == 0 && stack.Padding == 0);
        var reused = m.Clone();
        HostCheck.True((uint)reused.Value == (uint)child.Value && reused.Value != child.Value);
        m.Yield(parent);
        HostCheck.Equal(reused.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(2U, o.ReadStack().Flags);
    }

    [HostTest("linux.signal-startup altstack enforces active-stack EPERM min size flags autodisarm and normalized disable")]
    private static void StackValidation()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        o.Stack(Base + 0x3800, 2047);
        HostCheck.Equal(-12L, o.Call(131, Input));
        o.Stack(Base + 0x3800, 2048, 3);
        HostCheck.Equal(-22L, o.Call(131, Input));
        o.Stack(Base + 0x3800, 2048);
        HostCheck.Equal(0L, o.Call(131, Input));
        m.CurrentImage()->Frame.Rsp = Base + 0x3900;
        HostCheck.Equal(1U, o.ReadStack().Flags);
        o.Stack(0, 0, 2);
        HostCheck.Equal(-1L, o.Call(131, Input));
        m.CurrentImage()->Frame.Rsp = Base + 0x3000;
        o.Stack(Base + 0x3800, 2048, 0x80000000);
        HostCheck.Equal(0L, o.Call(131, Input));
        m.CurrentImage()->Frame.Rsp = Base + 0x3900;
        HostCheck.Equal(0x80000000U, o.ReadStack().Flags);
        o.Stack(ulong.MaxValue, ulong.MaxValue, 0x80000002);
        HostCheck.Equal(0L, o.Call(131, Input));
        var stack = o.ReadStack();
        HostCheck.True(stack.Address == 0 && stack.Bytes == 0 && stack.Flags == 0x80000002 && stack.Padding == 0);
    }

    [HostTest("linux.signal-startup altstack input faults preserve state while output faults retain a valid committed update")]
    private static void StackCopyFaults()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        o.Stack(Base + 0x3800, 2048);
        m.FailRead(Input);
        HostCheck.Equal(-14L, o.Call(131, Input));
        m.FailRead(ulong.MaxValue);
        HostCheck.Equal(2U, o.ReadStack().Flags);
        m.FailWrite(Output);
        HostCheck.Equal(-14L, o.Call(131, Input, Output));
        m.FailWrite(ulong.MaxValue);
        var stack = o.ReadStack();
        HostCheck.True(stack.Address == Base + 0x3800 && stack.Bytes == 2048 && stack.Padding == 0);
        HostCheck.Equal(-14L, o.Call(131, Base - 1, Output));
        HostCheck.Equal(Base + 0x3800, o.ReadStack().Address);
    }

    [HostTest("linux.signal-startup real gateway routes action and altstack calls but never reports successful unimplemented delivery")]
    private static void Gateway()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        byte* scratch = stackalloc byte[64];
        LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new() { UserRoot = m.Kernel.Owner.Root, MaximumCalls = 64, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64, FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true }, m.GatewayAccess));
        foreach (ulong number in new[]
        {
            13UL,
            131UL
        }

        )
        {
            var frame = m.CurrentImage()->Frame;
            frame.Number = number;
            frame.Argument0 = number == 13 ? 10UL : 0;
            frame.Argument1 = frame.Argument2 = 0;
            frame.Argument3 = number == 13 ? 8UL : 0;
            var result = LinuxSyscallGateway.Dispatch(ref gate, ref frame);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            HostCheck.Equal(0L, result.Value);
        }

        HostCheck.Equal(-38L, o.Call(62));
        HostCheck.Equal(-22L, o.Call(234));
    }

    [HostTest("linux.signal-startup optional attachment preserves original unimplemented signal behavior")]
    private static void Disabled()
    {
        using Owner o = new(attach: false);
        o.Running();
        HostCheck.Equal(-38L, o.Call(13, 10, 0, 0, 8));
        HostCheck.Equal(-38L, o.Call(131));
        HostCheck.True(!o.Thread.Service->EnableSignals(Key, 2, o.State));
    }
}
