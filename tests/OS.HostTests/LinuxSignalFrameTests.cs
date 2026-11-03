using OS.Kernel.Compatibility.LinuxX64;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using Transition = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;
using Owner = LinuxSignalServicesTests.Owner;

internal static unsafe class LinuxSignalFrameTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Input = Base + 0x1200, Output = Base + 0x1300, Handler = Base + 0x180;
    private const ulong Restorer = 0x04000000, Info = 4, OnStack = 0x08000000, NoDefer = 0x40000000;

    private static void Register(Owner o, uint signal = 10, ulong flags = Restorer | Info, ulong mask = 0)
    {
        o.Action(Handler, flags, mask);
        HostCheck.Equal(0L, o.Call(13, signal, Input, 0, 8));
    }
    private static Signals.Delivery Deliver(Owner o, uint signal = 10) => o.Thread.Service->DeliverSignal(Key, 2, signal, -6);
    private static ulong Frame(Owner o) => o.Thread.CurrentImage()->Frame.Rsp;
    private static LinuxSyscallResult Return(Owner o, ulong frame)
    {
        o.Thread.CurrentImage()->Frame.Rsp = frame + 8;
        return o.Thread.Call(15);
    }
    private static void SetMask(Owner o, ulong mask)
    {
        o.Thread.Put64(Input, mask);
        HostCheck.Equal(0L, o.Call(14, 2, Input, 0, 8));
    }

    [HostTest("linux.signal-frame exact x64 ABI saves all GPRs FX mask siginfo and red zone without kernel pointers")]
    private static void Layout()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        Register(o, mask: 1UL << 11);
        var image = m.CurrentImage();
        image->Frame.Rflags = 0x403;
        *(ushort*)image->Fx = 0x77f;
        image->Fx[4] = 3;
        *(uint*)(image->Fx + 24) = 0x5f80;
        Transition.UserImage prior = *image;
        ulong blocked = m.View(m.Kernel.Table->Current).BlockedSignals;
        byte[] user = new ReadOnlySpan<byte>(m.At(Base), (int)LinuxThreadServicesTests.UserBytes).ToArray();
        HostCheck.Equal(Signals.Delivery.Delivered, Deliver(o));
        ulong frame = Frame(o), fp = m.Read64(frame + 232);
        HostCheck.Equal(440U, Signals.FrameBytes);
        HostCheck.True((frame & 15) == 8 && (fp & 63) == 0 && fp == frame + 456);
        HostCheck.Equal(Base + 0x200, m.Read64(frame));
        HostCheck.Equal(6UL, m.Read64(frame + 8));
        HostCheck.Equal(0UL, m.Read64(frame + 16));
        var stack = *(Signals.AlternateStack*)m.At(frame + 24);
        HostCheck.True(stack.Address == 0 && stack.Bytes == 0 && stack.Flags == 2 && stack.Padding == 0);
        ulong[] expected = [prior.Gpr[5],prior.Gpr[6],prior.Gpr[7],prior.Gpr[13],prior.Gpr[8],prior.Gpr[9],prior.Gpr[10],prior.Gpr[11],
            prior.Gpr[3],prior.Gpr[2],prior.Gpr[1],prior.Gpr[0],prior.Gpr[4],unchecked((ulong)prior.Value),prior.Gpr[12],prior.Frame.Rsp,prior.Frame.Rip,prior.Frame.Rflags];
        for (uint i = 0; i < expected.Length; i++)
            HostCheck.Equal(expected[i], m.Read64(frame + 48 + i * 8));
        HostCheck.Equal(0x003300000000003bUL, m.Read64(frame + 192));
        HostCheck.Equal(blocked, m.Read64(frame + 216));
        HostCheck.Equal(blocked, m.Read64(frame + 304));
        HostCheck.Equal(10U, m.Read32(frame + 312));
        HostCheck.Equal(0U, m.Read32(frame + 316));
        HostCheck.Equal(unchecked((uint)-6), m.Read32(frame + 320));
        HostCheck.Equal(101U, m.Read32(frame + 328));
        HostCheck.True(new ReadOnlySpan<byte>(m.At(frame + 332), 108).IndexOfAnyExcept((byte)0) < 0);
        HostCheck.True(new ReadOnlySpan<byte>(m.At(fp), 464).SequenceEqual(new ReadOnlySpan<byte>(prior.Fx, 464)) &&
            new ReadOnlySpan<byte>(m.At(fp + 464), 48).IndexOfAnyExcept((byte)0) < 0);
        for (ulong offset = 0; offset < LinuxThreadServicesTests.UserBytes; offset++)
            if (!(Base + offset >= frame && Base + offset < frame + 440) && !(Base + offset >= fp && Base + offset < fp + 512))
                HostCheck.Equal(user[offset], *m.At(Base + offset));
        HostCheck.True(image->Frame.Rip == Handler && image->Frame.Rflags == 3 && image->Value == 0);
        HostCheck.True(image->Gpr[3] == 10 && image->Gpr[2] == frame + 312 && image->Gpr[4] == frame + 8);
        byte[] freshFx = new byte[512];
        freshFx[0] = 0x7f;
        freshFx[1] = 3;
        BitConverter.GetBytes(0x1f80U).CopyTo(freshFx, 24);
        BitConverter.GetBytes(0xffbfU).CopyTo(freshFx, 28);
        HostCheck.True(new ReadOnlySpan<byte>(image->Fx, 512).SequenceEqual(freshFx));
        HostCheck.Equal(blocked | (1UL << 9) | (1UL << 11), m.View(m.Kernel.Table->Current).BlockedSignals);
    }

    [HostTest("linux.signal-frame checked sigreturn accepts edited user registers FP mask stack and preserves current FS GS bases")]
    private static void EditedReturn()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        Register(o);
        HostCheck.Equal(Signals.Delivery.Delivered, Deliver(o));
        ulong frame = Frame(o), sc = frame + 48, fp = m.Read64(frame + 232);
        for (uint i = 0; i < 15; i++)
            m.Put64(sc + i * 8, 0xa100000000000000UL + i);
        m.Put64(sc + 120, Base + 0x4800);
        m.Put64(sc + 128, Base + 0x280);
        m.Put64(sc + 136, 0x803);
        m.Put64(sc + 168, 0);
        m.Put64(frame + 304, ulong.MaxValue); // uc_sigmask wins over historical oldmask.
        *(Signals.AlternateStack*)m.At(frame + 24) = new()
        {
            Address = Base + 0x3800,
            Bytes = 2048,
            Padding = uint.MaxValue
        };
        m.Put32(fp + 24, 0x3f80);
        *m.At(fp + 32) = 0x71;
        *m.At(fp + 160) = 0x93;
        HostCheck.Equal(0L, o.Call(158, 0x1002, Base + 0x2500));
        var result = Return(o, frame);
        HostCheck.Equal(LinuxSyscallAction.RestoreUser, result.Action);
        HostCheck.Equal(unchecked((long)0xa10000000000000dUL), result.Value);
        var image = m.CurrentImage();
        uint[] map = [11, 10, 9, 8, 12, 0, 1, 2, 4, 5, 6, 7, 14, 3];
        for (uint i = 0; i < 14; i++)
            HostCheck.Equal(0xa100000000000000UL + map[i], image->Gpr[i]);
        HostCheck.True(image->Value == result.Value && image->Frame.Rip == Base + 0x280 && image->Frame.Rsp == Base + 0x4800 && image->Frame.Rflags == 0x803);
        HostCheck.True(image->Frame.FsBase == Base + 0x2500 && image->Frame.GsBase == Base + 0x1140);
        HostCheck.True(image->Frame.Argument0 == image->Gpr[3] && image->Frame.Argument1 == image->Gpr[2] && image->Frame.Argument2 == image->Gpr[4] &&
            image->Frame.Argument3 == image->Gpr[7] && image->Frame.Argument4 == image->Gpr[5] && image->Frame.Argument5 == image->Gpr[6]);
        HostCheck.True(*(uint*)(image->Fx + 24) == 0x3f80 && image->Fx[32] == 0x71 && image->Fx[160] == 0x93);
        HostCheck.Equal(ulong.MaxValue & ~LinuxSignalMask.Unblockable, m.View(m.Kernel.Table->Current).BlockedSignals);
        var stack = o.ReadStack();
        HostCheck.True(stack.Address == Base + 0x3800 && stack.Bytes == 2048 && stack.Flags == 0 && stack.Padding == 0);
    }

    [HostTest("linux.signal-frame nested frames return LIFO and restore each mask and red-zone-separated context")]
    private static void Nested()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        Register(o, flags: Restorer | Info | NoDefer);
        ulong original = Frame(o);
        ulong[] frames = new ulong[4];
        for (int i = 0; i < 4; i++)
        {
            HostCheck.Equal(Signals.Delivery.Delivered, Deliver(o));
            frames[i] = Frame(o);
            HostCheck.True(frames[i] < (i == 0 ? original : frames[i - 1]) - 128);
        }
        for (int i = 3; i >= 0; i--)
        {
            HostCheck.Equal(LinuxSyscallAction.RestoreUser, Return(o, frames[i]).Action);
            HostCheck.Equal(i == 0 ? original : frames[i - 1], Frame(o));
        }
        HostCheck.Equal(Signals.Delivery.Delivered, Deliver(o)); // Popped capacity is reusable.
    }

    [HostTest("linux.signal-frame masks ignored defaults and unsupported job control never fabricate a handler")]
    private static void Dispositions()
    {
        using (Owner o = new())
        {
            o.Running();
            Register(o);
            SetMask(o, 1UL << 9);
            uint before = o.Thread.Writes;
            HostCheck.Equal(Signals.Delivery.Masked, Deliver(o));
            HostCheck.Equal(before, o.Thread.Writes);
            SetMask(o, 0);
            o.Action(1);
            HostCheck.Equal(0L, o.Call(13, 10, Input, 0, 8));
            HostCheck.Equal(Signals.Delivery.Ignored, Deliver(o));
            HostCheck.Equal(Signals.Delivery.Ignored, Deliver(o, 17));
            HostCheck.Equal(Signals.Delivery.Unsupported, Deliver(o, 19));
            HostCheck.True(!o.Thread.View(o.Thread.Kernel.Table->Current).Exiting);
        }
        using (Owner o = new())
        {
            o.Running();
            var child = o.Thread.Clone();
            HostCheck.Equal(Signals.Delivery.Terminate, Deliver(o, 12));
            HostCheck.True(o.Thread.View(child).Exiting && o.Thread.View(child).ExitCode == 140);
        }
    }

    [HostTest("linux.signal-frame alternate stack autodisarm restores registration and synchronous fault siginfo")]
    private static void Alternate()
    {
        foreach (uint flags in new[] { 0U, Signals.AutoDisarm })
        {
            using Owner o = new();
            o.Running();
            var m = o.Thread;
            Register(o, signal: 11, flags: Restorer | Info | OnStack);
            o.Stack(Base + 0x3800, 2048, flags);
            HostCheck.Equal(0L, o.Call(131, Input));
            ulong original = Frame(o);
            HostCheck.Equal(Signals.Delivery.Delivered, m.Service->DeliverSignal(Key, 2, 11, 2, 0x70123456, 7, 14));
            ulong frame = Frame(o);
            HostCheck.True(frame >= Base + 0x3800 && frame < Base + 0x4000);
            HostCheck.Equal(0x70123456UL, m.Read64(frame + 328));
            HostCheck.Equal(7UL, m.Read64(frame + 200));
            HostCheck.Equal(14UL, m.Read64(frame + 208));
            HostCheck.Equal(0x70123456UL, m.Read64(frame + 224));
            HostCheck.Equal(flags == 0 ? 1U : 2U, o.ReadStack().Flags);
            HostCheck.Equal(LinuxSyscallAction.RestoreUser, Return(o, frame).Action);
            HostCheck.Equal(original, Frame(o));
            var stack = o.ReadStack();
            HostCheck.True(stack.Address == Base + 0x3800 && stack.Bytes == 2048 && stack.Flags == flags);
        }
    }

    [HostTest("linux.signal-frame alternate stack exhaustion and nesting limits terminate without unchecked stack writes")]
    private static void Exhaustion()
    {
        foreach (bool alternate in new[] { true, false })
        {
            using Owner o = new();
            o.Running();
            Register(o, flags: Restorer | Info | NoDefer | (alternate ? OnStack : 0));
            if (alternate)
            {
                o.Stack(Base + 0x3800, 2048);
                HostCheck.Equal(0L, o.Call(131, Input));
            }
            int successes = alternate ? 1 : 4;
            for (int i = 0; i < successes; i++)
                HostCheck.Equal(Signals.Delivery.Delivered, Deliver(o));
            uint writes = o.Thread.Writes;
            HostCheck.Equal(Signals.Delivery.BadFrame, Deliver(o));
            HostCheck.Equal(writes, o.Thread.Writes);
            var view = o.Thread.View(o.Thread.Kernel.Table->Current);
            HostCheck.True(view.Exiting && view.ExitCode == 139);
        }
    }

    [HostTest("linux.signal-frame malformed malicious and stale return frames stage real shutdown without partial kernel restoration")]
    private static void BadReturns()
    {
        for (int variant = 0; variant < 23; variant++)
        {
            using Owner o = new();
            o.Running();
            var m = o.Thread;
            Register(o);
            HostCheck.Equal(Signals.Delivery.Delivered, Deliver(o));
            ulong frame = Frame(o), sc = frame + 48, fp = m.Read64(frame + 232), returnFrame = frame;
            switch (variant)
            {
                case 0:
                    returnFrame += 16;
                    break;
                case 1:
                    m.FailRead(frame);
                    break;
                case 2:
                    *(ushort*)m.At(sc + 144) = 8;
                    break;
                case 3:
                    *(ushort*)m.At(sc + 150) = 0x10;
                    break;
                case 4:
                    *(ushort*)m.At(sc + 146) = 0x33;
                    break;
                case 5:
                    *(ushort*)m.At(sc + 148) = 0x33;
                    break;
                case 6:
                    m.Put64(frame + 8, 7);
                    break;
                case 7:
                    m.Put64(sc + 136, 0x202);
                    break;
                case 8:
                    m.Put64(sc + 136, 0x3002);
                    break;
                case 9:
                    m.Put64(sc + 136, 0);
                    break;
                case 10:
                    m.Put64(sc + 128, Base + 0x1000);
                    break;
                case 11:
                    m.Put64(sc + 120, 0xffff800000001000);
                    break;
                case 12:
                    m.Put64(sc + 184, fp + 1);
                    break;
                case 13:
                    m.Put64(sc + 184, Base - 4096);
                    break;
                case 14:
                    m.Put32(fp + 24, 0x10000);
                    break;
                case 15:
                    m.Put32(fp + 28, 0xffff);
                    break;
                case 16:
                    m.Put32(fp + 464, 0x46505853);
                    break;
                case 17:
                    m.Put64(sc + 192, 1);
                    break;
                case 18:
                    ((Signals.AlternateStack*)m.At(frame + 24))->Flags = 3;
                    break;
                case 19:
                    *(Signals.AlternateStack*)m.At(frame + 24) = new()
                    {
                        Address = Base + 0x3800,
                        Bytes = 2047
                    };
                    break;
                case 20:
                    m.FailRead(fp);
                    break;
                case 21:
                    *(Signals.AlternateStack*)m.At(frame + 24) = new()
                    {
                        Address = ulong.MaxValue - 1024,
                        Bytes = 2048
                    };
                    break;
                default:
                    m.Put64(sc + 136, 0x102);
                    break;
            }
            byte[] metadata = new ReadOnlySpan<byte>(o.State, sizeof(Signals.State)).ToArray();
            Transition.UserImage prior = *m.CurrentImage();
            ulong mask = m.View(m.Kernel.Table->Current).BlockedSignals;
            var result = Return(o, returnFrame);
            HostCheck.Equal(LinuxSyscallAction.Terminate, result.Action);
            HostCheck.Equal(139U, result.ExitCode);
            HostCheck.True(new ReadOnlySpan<byte>(o.State, sizeof(Signals.State)).SequenceEqual(metadata));
            var view = m.View(m.Kernel.Table->Current);
            HostCheck.True(view.Exiting && view.ExitCode == 139 && view.BlockedSignals == mask);
            HostCheck.True(new ReadOnlySpan<byte>(m.CurrentImage()->Gpr, 112).SequenceEqual(new ReadOnlySpan<byte>(prior.Gpr, 112)) &&
                new ReadOnlySpan<byte>(m.CurrentImage()->Fx, 512).SequenceEqual(new ReadOnlySpan<byte>(prior.Fx, 512)));
            m.FailRead(ulong.MaxValue);
        }
    }

    [HostTest("linux.signal-frame invalid delivery mappings missing restorer and copy failures never publish a frame")]
    private static void BadDelivery()
    {
        for (int variant = 0; variant < 5; variant++)
        {
            using Owner o = new();
            o.Running();
            var m = o.Thread;
            Register(o);
            if (variant < 3)
            {
                o.Action(variant == 0 ? Base + 0x1000 : Handler, variant == 1 ? 0 : Restorer | Info);
                if (variant == 2)
                    ((Signals.Disposition*)m.At(Input))->Restorer = Base + 0x1000;
                HostCheck.Equal(0L, o.Call(13, 10, Input, 0, 8));
            }
            // Initial RSP=403000 gives fp=402d80 and frame=402bb8.
            else
                m.FailWrite(variant == 3 ? Base + 0x2d80 : Base + 0x2bb8);
            byte[] metadata = new ReadOnlySpan<byte>(o.State, sizeof(Signals.State)).ToArray();
            Transition.UserImage prior = *m.CurrentImage();
            HostCheck.Equal(Signals.Delivery.BadFrame, Deliver(o));
            HostCheck.True(new ReadOnlySpan<byte>(o.State, sizeof(Signals.State)).SequenceEqual(metadata));
            HostCheck.True(new ReadOnlySpan<byte>(m.CurrentImage(), 728).SequenceEqual(new ReadOnlySpan<byte>(&prior, 728)));
        }
    }

    [HostTest("linux.signal-frame actual generation keys copied owners and staged waits reject delivery")]
    private static void OwnerChecks()
    {
        using Owner o = new();
        o.Running();
        var m = o.Thread;
        Register(o);
        HostCheck.Equal(Signals.Delivery.Rejected, m.Service->DeliverSignal(Key + 1, 2, 10, -6));
        HostCheck.Equal(Signals.Delivery.Rejected, m.Service->DeliverSignal(Key, 0x202, 10, -6));
        HostCheck.Equal(Signals.Delivery.Rejected, Deliver(o, 0));
        HostCheck.Equal(Signals.Delivery.Rejected, Deliver(o, 65));
        LinuxThreadServices copied = *m.Service;
        HostCheck.Equal(Signals.Delivery.Rejected, copied.DeliverSignal(Key, 2, 10, -6));
        m.Put32(Input, 7);
        HostCheck.Equal(LinuxSyscallAction.Blocked, m.Call(202, Input, 128, 7).Action);
        HostCheck.Equal(Signals.Delivery.Rejected, Deliver(o));
        var current = m.Kernel.Table->Current;
        HostCheck.True(m.Service->CommitFutexWait(Key, 2, m.Kernel.Capture(current), out bool handoff) && handoff);
        m.Kernel.Confirm();
    }

    [HostTest("linux.signal-frame sigreturn requires this actual generation's delivered frame and resets one-shot actions")]
    private static void GenerationAndReset()
    {
        using (Owner o = new())
        {
            o.Running();
            var result = o.Thread.Call(15);
            HostCheck.Equal(LinuxSyscallAction.Terminate, result.Action);
            HostCheck.Equal(139U, result.ExitCode);
        }
        using (Owner o = new())
        {
            o.Running();
            Register(o, flags: Restorer | Info | 0x80000000);
            HostCheck.Equal(Signals.Delivery.Delivered, Deliver(o));
            ulong frame = Frame(o);
            HostCheck.Equal(0L, o.Call(13, 10, 0, Output, 8));
            HostCheck.Equal(0UL, o.Thread.Read64(Output));
            HostCheck.Equal(Restorer | Info | 0x80000000, o.Thread.Read64(Output + 8));
            HostCheck.Equal(Base + 0x200, o.Thread.Read64(Output + 16));
            HostCheck.Equal(LinuxSyscallAction.RestoreUser, Return(o, frame).Action);
            HostCheck.Equal(Signals.Delivery.Terminate, Deliver(o));
        }
        using (Owner o = new())
        {
            o.Running();
            Register(o);
            HostCheck.Equal(Signals.Delivery.Delivered, Deliver(o));
            var m = o.Thread;
            ulong frame = Frame(o);
            var parent = m.Kernel.Table->Current;
            var child = m.Clone();
            m.Yield(parent);
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(LinuxSyscallAction.Terminate, Return(o, frame).Action);
        }
    }

    [HostTest("linux.signal-frame real gateway explicitly admits only checked sigreturn restoration")]
    private static void Gateway()
    {
        using Owner o = new();
        o.Running();
        Register(o);
        HostCheck.Equal(Signals.Delivery.Delivered, Deliver(o));
        var m = o.Thread;
        ulong frameAddress = Frame(o), sc = frameAddress + 48;
        m.Put64(sc + 104, 0x12345678);
        m.Put64(sc + 128, Base + 0x280);
        m.Put64(sc + 112, 0xa001);
        m.Put64(sc + 24, 0xa002);
        byte* scratch = stackalloc byte[64];
        LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new()
        {
            UserRoot = m.Kernel.Owner.Root,
            MaximumCalls = 64,
            MaximumWrite = 64,
            Scratch = scratch,
            ScratchBytes = 64,
            FirmwareExited = true,
            OwnedBoundaryValidated = true,
            EnableStartup = true,
            EnableDescriptors = true,
            EnableThreads = true
        }, m.GatewayAccess));
        var image = m.CurrentImage();
        image->Frame.Rsp = frameAddress + 8;
        image->Frame.Number = 15;
        var frame = image->Frame;
        var result = LinuxSyscallGateway.Dispatch(ref gate, ref frame);
        HostCheck.True(result.Action == LinuxSyscallAction.RestoreUser && result.Value == 0x12345678 && gate.Status == LinuxSyscallGateStatus.Active && gate.Calls == 1);
        HostCheck.True(frame.Rip == Base + 0x280 && image->Gpr[12] == 0xa001 && image->Gpr[13] == 0xa002 && image->Value == 0x12345678);
    }
}
