using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;
using Transition = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;
using Owner = LinuxSignalServicesTests.Owner;

internal static unsafe class LinuxUserInterruptTests
{
    private const ulong Base = LinuxThreadServicesTests.UserBase, Key = LinuxThreadServicesTests.Key, Input = Base + 0x1200;

    [HostTest("linux.user-irq signal attachment rejects a different retained IF policy before publication")]
    private static void SignalPolicyBinding()
    {
        using Owner o = new(attach: false, userInterrupts: true);
        var rejected = (LinuxSignalServices.State*)NativeMemory.AlignedAlloc(4096, 4096);
        try
        {
            HostCheck.True(rejected != null); new Span<byte>(rejected, 4096).Clear();
            var plan = o.Plan; plan.User.UserInterrupts = false;
            HostCheck.True(LinuxSignalServices.Initialize(rejected, 4096, 1234, 2, plan));
            HostCheck.True(!o.Thread.Service->EnableSignals(Key, 2, rejected));
            HostCheck.True(LinuxSignalServices.End(rejected, 1234, 2));
        }
        finally { NativeMemory.AlignedFree(rejected); }
        HostCheck.True(o.Thread.Service->EnableSignals(Key, 2, o.State));
        o.Running();
    }

    [HostTest("linux.user-irq return flags require the trusted policy and cannot enable kernel or legacy interrupts")]
    private static void ReturnPolicy()
    {
        using LinuxThreadServicesTests.Model legacy = new(), enabled = new(userInterrupts: true);
        var frame = enabled.Initial->Frame;
        HostCheck.True(Transition.ValidateUserImage(enabled.Initial, Transition.UserImageBytes, 0xffbf, enabled.Access));
        HostCheck.True(!LinuxSyscallGateway.ValidateReturn(frame, legacy.Access));
        foreach (ulong flags in new[] { 0UL, 2UL, 0x3202UL, 0x40202UL, 0x10202UL, 0x302UL, ulong.MaxValue })
        { frame.Rflags = flags; HostCheck.True(!LinuxSyscallGateway.ValidateReturn(frame, enabled.Access), "flags=" + flags); }
        foreach (ulong flags in new[] { 0x202UL, 0x603UL, 0x200ad7UL })
        { frame.Rflags = flags; HostCheck.True(LinuxSyscallGateway.ValidateReturn(frame, enabled.Access), "flags=" + flags); }
        frame = legacy.Initial->Frame;
        HostCheck.True(LinuxSyscallGateway.ValidateReturn(frame, legacy.Access) && !LinuxSyscallGateway.ValidateReturn(frame, enabled.Access));
        byte* scratch = stackalloc byte[64]; LinuxSyscallGateState gate = default;
        LinuxSyscallAdmission admission = new() { UserRoot = 0x200000, MaximumCalls = 16, MaximumWrite = 64,
            Scratch = scratch, ScratchBytes = 64, FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true };
        var access = enabled.GatewayAccess; access.Threads = null;
        HostCheck.Equal(LinuxSyscallGateStatus.InvalidAdmission, LinuxSyscallGateway.Begin(ref gate, admission, access));
        admission.EnableThreads = true;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, admission, enabled.GatewayAccess));
        var identity = enabled.Start(); HostCheck.Equal(identity.Value, enabled.Kernel.Dispatch().Value);
        frame = enabled.CurrentImage()->Frame; frame.Number = 158; frame.Argument0 = 0x1001; frame.Argument1 = Base + 0x2100;
        enabled.CurrentImage()->Frame = frame;
        HostCheck.Equal(LinuxSyscallAction.Return, LinuxSyscallGateway.Dispatch(ref gate, ref frame).Action);
        HostCheck.True(frame.Rflags == 0x202 && frame.GsBase == Base + 0x2100 && gate.Calls == 1);
    }

    private static void Capture(byte* data, Transition.UserImage* source)
    {
        new Span<byte>(data, (int)Transition.DataBytes).Clear();
        *(ulong*)(data + Transition.PhaseOffset) = 1; *(ulong*)(data + Transition.OutcomeOffset) = 3;
        *(ulong*)(data + Transition.VectorOffset) = 0xe1;
        *(ushort*)(data + Transition.FaultSelectorsOffset) = Transition.UserCs;
        *(ushort*)(data + Transition.FaultSelectorsOffset + 2) = Transition.UserSs;
        *(LinuxSyscallFrame*)(data + Transition.FrameOffset) = source->Frame;
        *(ulong*)(data + Transition.FrameOffset) = 0;
        *(long*)(data + Transition.ValueOffset) = source->Value;
        for (uint i = 0; i < 14; i++) *(ulong*)(data + 0x160 + i * 8) = source->Gpr[i];
        for (uint i = 0; i < 512; i++) data[Transition.UserFxOffset + i] = source->Fx[i];
    }

    [HostTest("linux.user-irq full capture retains actual RAX all GPRs bases flags and FX and rejects corrupt continuations atomically")]
    private static void FullCapture()
    {
        using LinuxThreadServicesTests.Model m = new(userInterrupts: true);
        byte* data = (byte*)NativeMemory.AlignedAlloc(Transition.DataBytes, 4096);
        try
        {
            HostCheck.True(data != null); Transition.UserImage source = *m.Initial; source.Frame.Number = 0;
            source.Frame.Rflags = 0x603; source.Value = unchecked((long)0xf123456789abcdef);
            Capture(data, &source); Transition.UserImage result = default;
            HostCheck.True(Transition.CaptureInterruptUserImage(data, Transition.DataBytes, &result, Transition.UserImageBytes, 0xffbf, m.Access));
            HostCheck.True(new ReadOnlySpan<byte>(&source, sizeof(Transition.UserImage)).SequenceEqual(new ReadOnlySpan<byte>(&result, sizeof(Transition.UserImage))));
            source.Frame.Rflags |= 0x10000; Capture(data, &source);
            byte[] raw = new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).ToArray();
            HostCheck.True(!LinuxSyscallGateway.ValidateReturn(source.Frame, m.Access), "ordinary return still rejects RF");
            HostCheck.True(Transition.CaptureInterruptUserImage(data, Transition.DataBytes, &result, Transition.UserImageBytes, 0xffbf, m.Access));
            var normalized = source; normalized.Frame.Rflags &= ~0x10000UL;
            HostCheck.True(new ReadOnlySpan<byte>(&normalized, sizeof(Transition.UserImage)).SequenceEqual(new ReadOnlySpan<byte>(&result, sizeof(Transition.UserImage))));
            HostCheck.True(new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).SequenceEqual(raw), "raw hardware capture is unchanged");
            foreach (ulong badFlags in new[] { source.Frame.Rflags | 0x3000, source.Frame.Rflags | 0x4000,
                source.Frame.Rflags | 0x20000, source.Frame.Rflags & ~0x200UL })
            {
                Capture(data, &source); *(ulong*)(data + 0x148) = badFlags; var before = result;
                HostCheck.True(!Transition.CaptureInterruptUserImage(data, Transition.DataBytes, &result, Transition.UserImageBytes, 0xffbf, m.Access));
                HostCheck.True(new ReadOnlySpan<byte>(&before, sizeof(Transition.UserImage)).SequenceEqual(new ReadOnlySpan<byte>(&result, sizeof(Transition.UserImage))));
            }
            for (int mutation = 0; mutation < 12; mutation++)
            {
                Capture(data, &source); Transition.UserImage before = result;
                switch (mutation)
                {
                    case 0: *(ulong*)(data + Transition.PhaseOffset) = 2; break;
                    case 1: *(ulong*)(data + Transition.OutcomeOffset) = 0; break;
                    case 2: *(ulong*)(data + Transition.VectorOffset) = 14; break;
                    case 3: *(ulong*)(data + Transition.ErrorOffset) = 1; break;
                    case 4: *(ushort*)(data + Transition.FaultSelectorsOffset) = 8; break;
                    case 5: *(ushort*)(data + Transition.FaultSelectorsOffset + 2) = 16; break;
                    case 6: *(ulong*)(data + 0x138) = Base + 0x1100; break; // RW/NX.
                    case 7: *(ulong*)(data + 0x140) = 0; break;
                    case 8: *(ulong*)(data + 0x148) = 2; break;
                    case 9: *(ulong*)(data + 0x150) = ulong.MaxValue; break;
                    case 10: *(uint*)(data + Transition.UserFxOffset + 24) = uint.MaxValue; break;
                    case 11: *(uint*)(data + Transition.UserFxOffset + 28) = 0; break;
                }
                HostCheck.True(!Transition.CaptureInterruptUserImage(data, Transition.DataBytes, &result, Transition.UserImageBytes, 0xffbf, m.Access), "mutation=" + mutation);
                HostCheck.True(new ReadOnlySpan<byte>(&before, sizeof(Transition.UserImage)).SequenceEqual(new ReadOnlySpan<byte>(&result, sizeof(Transition.UserImage))));
            }
            Capture(data, &source); var disabled = m.Access; disabled.UserInterrupts = false;
            HostCheck.True(!Transition.CaptureInterruptUserImage(data, Transition.DataBytes, &result, Transition.UserImageBytes, 0xffbf, disabled));
            HostCheck.True(!Transition.CaptureFaultUserImage(data, Transition.DataBytes, &result, Transition.UserImageBytes, 0xffbf, true));
        }
        finally { NativeMemory.AlignedFree(data); }
    }

    [HostTest("linux.user-irq cloned generation queues delivery across a real table yield without syscall accounting and sigreturn preserves IF")]
    private static void SignalsAfterYield()
    {
        using Owner o = new(userInterrupts: true); o.Running(); var m = o.Thread;
        var parent = m.Kernel.Table->Current;
        o.Action(Base + 0x180, 0x04000004); HostCheck.Equal(0L, o.Call(13, 10, Input, 0, 8));
        var original = *m.CurrentImage(); var child = m.Clone();
        m.Yield(parent); HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0x202UL, m.CurrentImage()->Frame.Rflags);
        HostCheck.Equal(0L, m.Call(234, 101, m.View(parent).Tid, 10).Value);
        m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        // Model only the native owner's fully captured IRQ image. No opcode,
        // hardware IRQ, frame restoration or native CALL executes on this host.
        *m.CurrentImage() = original;
        HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Service->PrepareUserReturn(Key, 2, out var handler).Action);
        ulong frame = handler->Frame.Rsp;
        HostCheck.True(handler->Frame.Rip == Base + 0x180 && handler->Frame.Rflags == 0x202);
        HostCheck.Equal(0x202UL, m.Read64(frame + 184));
        // Userspace may change arithmetic flags, but cannot turn off this
        // thread's trusted asynchronous-interrupt policy via sigcontext.
        m.Put64(frame + 184, 3); handler->Frame.Rsp = frame + 8;
        HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Call(15).Action);
        HostCheck.True(m.CurrentImage()->Frame.Rflags == 0x203 && m.CurrentImage()->Frame.Rip == original.Frame.Rip);
        HostCheck.Equal(original.Value, m.CurrentImage()->Value);
    }

    [HostTest("linux.user-irq emitter has an isolated positive E1 stub complete capture and unchanged disabled gateway")]
    private static void NativeBytes()
    {
        byte* code = (byte*)NativeMemory.AlignedAlloc(4096, 4096), fault = (byte*)NativeMemory.AlignedAlloc(4096, 4096);
        try
        {
            HostCheck.True(code != null && fault != null);
            X64LinuxUserTransitionPlan plan = new() { CodeAddress = 0x100000, ContextAddress = 0x200000, UserRoot = 0x300000,
                UserRip = Base + 0x100, UserRsp = Base + 0x3000, KernelRoot = 0x500000, ResumeUser = true,
                UserFaultEntry = 0x600000, Dispatcher = 0x800100, FaultReporter = 0x800200, CallbackContext = 0x800000,
                TssRsp0Address = 0x900004, OriginalGp = 0x801000, OriginalPf = 0x802000 };
            HostCheck.True(Transition.Emit(code, 4096, plan) && Transition.EmitFaultCapture(fault, 4096, plan));
            byte[] disabledGateway = new ReadOnlySpan<byte>(code, 4096).ToArray();
            HostCheck.True(new ReadOnlySpan<byte>(fault + Transition.UserInterruptOffset, 0x100).IndexOfAnyExcept((byte)0xcc) < 0);
            plan.UserInterrupts = true; plan.UserSpuriousEntry = 0x7001e0;
            HostCheck.True(Transition.Emit(code, 4096, plan) && Transition.EmitFaultCapture(fault, 4096, plan) && Transition.VerifyFaultCapture(fault, 4096, plan));
            HostCheck.True(disabledGateway.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(code, 4096)));
            ReadOnlySpan<byte> stub = new(fault + Transition.UserInterruptOffset, 64), capture = new(fault, (int)Transition.UserInterruptOffset);
            HostCheck.True(stub[..10].SequenceEqual(new byte[] { 0xf3,0x0f,0x1e,0xfa,0x48,0x83,0x7c,0x24,8,0x3b }));
            HostCheck.True(stub.Slice(16,7).SequenceEqual(new byte[] { 0x6a,0,0x68,0xe1,0,0,0 }) && stub[23] == 0xe9);
            HostCheck.Equal(-((int)Transition.UserInterruptOffset + 28), *(int*)(fault + Transition.UserInterruptOffset + 24));
            HostCheck.True(capture.IndexOf(new byte[] { 0x49,0x89,0xa6,0x58,6,0,0 }) > 0); // MOV [R14+658h],RSP.
            HostCheck.True(new ReadOnlySpan<byte>(fault + 0x7e0,4).SequenceEqual(new byte[] { 0xfa,0xf4,0xeb,0xfd }));
            fault[Transition.UserInterruptOffset + 19] = 0xff;
            HostCheck.True(!Transition.VerifyFaultCapture(fault, 4096, plan));
            plan.ResumeUser = false; HostCheck.True(!Transition.Emit(code, 4096, plan));
            plan.ResumeUser = true; plan.UserSpuriousEntry = plan.UserFaultEntry; HostCheck.True(!Transition.Emit(code, 4096, plan));
            plan.UserSpuriousEntry = 0xfffffffc; HostCheck.True(!Transition.Emit(code, 4096, plan));
            plan.UserInterrupts = false; HostCheck.True(!Transition.Emit(code, 4096, plan));
        }
        finally { NativeMemory.AlignedFree(code); NativeMemory.AlignedFree(fault); }
    }
}
