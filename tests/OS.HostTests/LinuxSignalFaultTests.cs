using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using Owner = LinuxSignalServicesTests.Owner;
using Transition = OS.Kernel.Compatibility.LinuxX64.X64LinuxUserTransition;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;

internal static unsafe class LinuxSignalFaultTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase, Input = Base + 0x1200;
    private static void Action(Owner o, bool alternate = false)
    {
        o.Action(Base + 0x180, 0x04000004 | (alternate ? 0x08000000UL : 0));
        HostCheck.Equal(0L, o.Call(13, 11, Input, 0, 8));
        if (alternate)
        {
            o.Stack(Base + 0x3000, 0x1000);
            HostCheck.Equal(0L, o.Call(131, Input));
        }
    }
    private static Transition.UserImage* Fault(Owner o, ulong rip = 0, ulong rsp = 0)
    {
        var image = o.Thread.CurrentImage();
        image->Frame.Number = 0;
        image->Frame.Rip = rip;
        image->Frame.Rsp = rsp;
        image->Frame.Rflags = 0x10403;
        image->Value = unchecked((long)0xfedcba9876543210);
        for (uint i = 0; i < 14; i++)
            image->Gpr[i] = 0x1111000000000000UL + i;
        return image;
    }

    [HostTest("linux.signal-fault inaccessible RIP and null RSP reach alternate handler with exact raw trap context and checked return")]
    private static void AlternateRecovery()
    {
        using Owner o = new();
        o.Running();
        Action(o, true);
        var m = o.Thread;
        var image = Fault(o, LinuxSyscallGateway.UserEnd + 0x123, 0);
        Transition.UserImage before = *image;
        HostCheck.Equal(Signals.Delivery.Delivered, m.Service->DeliverUserFault(Key, 2, 14, 0x14, before.Frame.Rip, 2));
        ulong frame = image->Frame.Rsp, sc = frame + 48;
        HostCheck.True(frame >= Base + 0x3000 && frame < Base + 0x4000);
        HostCheck.Equal(11UL, image->Gpr[3]);
        HostCheck.Equal(Base + 0x180, image->Frame.Rip);
        HostCheck.True((image->Frame.Rflags & 0x10400) == 0);
        HostCheck.Equal(before.Frame.Rip, m.Read64(sc + 128));
        HostCheck.Equal(0UL, m.Read64(sc + 120));
        HostCheck.Equal(before.Frame.Rflags, m.Read64(sc + 136));
        HostCheck.Equal(unchecked((ulong)before.Value), m.Read64(sc + 104));
        HostCheck.Equal(0x15UL, m.Read64(sc + 152));
        HostCheck.Equal(14UL, m.Read64(sc + 160)); // Sanitized outside-user error.
        HostCheck.Equal(before.Frame.Rip, m.Read64(sc + 176));
        HostCheck.Equal(11U, *(uint*)m.At(frame + 312));
        HostCheck.Equal(2, *(int*)m.At(frame + 320));
        HostCheck.Equal(before.Frame.Rip, m.Read64(frame + 328));
        // An unchanged invalid continuation cannot pass checked sigreturn.
        m.Put64(sc + 128, Base + 0x280);
        m.Put64(sc + 120, Base + 0x4ff0);
        image->Frame.Rsp = frame + 8;
        var result = m.Call(15);
        HostCheck.Equal(LinuxSyscallAction.RestoreUser, result.Action);
        HostCheck.Equal(Base + 0x280, image->Frame.Rip);
        HostCheck.Equal(Base + 0x4ff0, image->Frame.Rsp);
        HostCheck.Equal(before.Value, image->Value);
        HostCheck.True((image->Frame.Rflags & 0x10000) == 0);
        for (uint i = 0; i < 14; i++)
            HostCheck.Equal(before.Gpr[i], image->Gpr[i]);
    }

    [HostTest("linux.signal-fault unchanged inaccessible sigreturn and unusable ordinary stack stage actual SIGSEGV shutdown")]
    private static void BadContinuations()
    {
        foreach (bool alternate in new[] { false, true })
        {
            using Owner o = new();
            o.Running();
            Action(o, alternate);
            var image = Fault(o);
            var delivery = o.Thread.Service->DeliverUserFault(Key, 2, 14, 4, 0, 1);
            if (alternate)
            {
                HostCheck.Equal(Signals.Delivery.Delivered, delivery);
                image->Frame.Rsp += 8;
                HostCheck.Equal(LinuxSyscallAction.Terminate, o.Thread.Call(15).Action);
            }
            else
                HostCheck.Equal(Signals.Delivery.BadFrame, delivery);
            var view = o.Thread.View(o.Thread.Kernel.Table->Current);
            HostCheck.True(view.Exiting && view.ExitCode == 139);
        }
    }

    [HostTest("linux.signal-fault blocked ignored and default faults terminate every actual process generation")]
    private static void FatalActions()
    {
        for (int mode = 0; mode < 3; mode++)
        {
            using Owner o = new();
            o.Running();
            var m = o.Thread;
            if (mode == 0)
            {
                m.Put64(Input, 1UL << 10);
                HostCheck.Equal(0L, o.Call(14, 2, Input, 0, 8));
            }
            if (mode == 1)
            {
                o.Action(1);
                HostCheck.Equal(0L, o.Call(13, 11, Input, 0, 8));
            }
            var child = m.Clone();
            HostCheck.True(child.IsValid);
            Fault(o);
            HostCheck.Equal(Signals.Delivery.Terminate, m.Service->DeliverUserFault(Key, 2, 13, 0, 0, 128));
            HostCheck.True(m.View(m.Kernel.Table->Current).Exiting && m.View(child).Exiting);
            HostCheck.Equal(139U, m.View(child).ExitCode);
        }
    }

    [HostTest("linux.signal-fault rejects forged trap privilege error code copied owner and flags without publishing a frame")]
    private static void Provenance()
    {
        using Owner o = new();
        o.Running();
        Action(o, true);
        Fault(o);
        var m = o.Thread;
        foreach ((uint trap, ulong error, ulong address, int code) in new (uint, ulong, ulong, int)[]
        { (0,0,0,128),(13,0x10000,0,128),(13,0,1,128),(13,0,0,1),(14,0,0,1),(14,12,0,1),(14,36,0,1),(14,4,0,128) })
        {
            uint writes = m.Writes;
            HostCheck.Equal(Signals.Delivery.Rejected, m.Service->DeliverUserFault(Key, 2, trap, error, address, code));
            HostCheck.Equal(writes, m.Writes);
            HostCheck.True(!m.View(m.Kernel.Table->Current).Exiting);
        }
        LinuxThreadServices copy = *m.Service;
        HostCheck.Equal(Signals.Delivery.Rejected, copy.DeliverUserFault(Key, 2, 14, 4, 0, 1));
        HostCheck.Equal(Signals.Delivery.Rejected, m.Service->DeliverUserFault(Key + 1, 2, 14, 4, 0, 1));
        HostCheck.Equal(Signals.Delivery.Rejected, m.Service->DeliverUserFault(Key, 0x202, 14, 4, 0, 1));
        HostCheck.Equal(Signals.Delivery.Rejected, m.Service->DeliverSignal(Key, 2, 11, 1)); // Ordinary delivery cannot borrow fault admission.
    }

    [HostTest("linux.signal-fault full native capture keeps all raw user fields and rejects malformed hardware envelopes atomically")]
    private static void Capture()
    {
        byte* data = (byte*)NativeMemory.AlignedAlloc(Transition.DataBytes, 4096);
        var image = (Transition.UserImage*)NativeMemory.AlignedAlloc(4096, 4096);
        try
        {
            new Span<byte>(data, (int)Transition.DataBytes).Clear();
            *(ulong*)(data + 0x1f0) = 1;
            *(ulong*)(data + 0xe0) = 3;
            *(ulong*)(data + 0xf0) = 14;
            *(ulong*)(data + 0xf8) = 0x14;
            *(ushort*)(data + 0x1f8) = 0x3b;
            *(ushort*)(data + 0x1fa) = 0x33;
            var frame = (LinuxSyscallFrame*)(data + 0x100);
            *frame = new()
            {
                Rip = ulong.MaxValue,
                Rsp = 0,
                Rflags = 0x10403,
                FsBase = Base,
                GsBase = Base + 1
            };
            *(long*)(data + 0xe8) = -123;
            for (uint i = 0; i < 14; i++)
                *(ulong*)(data + 0x160 + i * 8) = 0x1000000000000001UL + i;
            *(ushort*)(data + 0x400) = 0x027f;
            *(uint*)(data + 0x418) = 0x5f80;
            *(uint*)(data + 0x41c) = 0xffff;
            byte[] baseline = new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).ToArray();
            HostCheck.True(Transition.CaptureFaultUserImage(data, Transition.DataBytes, image, 728, 0xffff));
            HostCheck.Equal(ulong.MaxValue, image->Frame.Rip);
            HostCheck.Equal(0UL, image->Frame.Rsp);
            HostCheck.Equal(-123L, image->Value);
            for (uint i = 0; i < 14; i++)
                HostCheck.Equal(0x1000000000000001UL + i, image->Gpr[i]);
            HostCheck.True(new ReadOnlySpan<byte>(data, (int)Transition.DataBytes).SequenceEqual(baseline));
            for (int variant = 0; variant < 12; variant++)
            {
                baseline.CopyTo(new Span<byte>(data, (int)Transition.DataBytes));
                new Span<byte>(image, 4096).Fill(0xa5);
                switch (variant)
                {
                    case 0:
                        *(ulong*)(data + 0x1f0) = 2;
                        break;
                    case 1:
                        *(ulong*)(data + 0xe0) = 0;
                        break;
                    case 2:
                        *(ushort*)(data + 0x1f8) = 8;
                        break;
                    case 3:
                        *(ushort*)(data + 0x1fa) = 16;
                        break;
                    case 4:
                        *(ulong*)(data + 0xf0) = 6;
                        break;
                    case 5:
                        *(ulong*)(data + 0xf8) = 0;
                        break;
                    case 6:
                        *(ulong*)(data + 0xf8) = 12;
                        break;
                    case 7:
                        frame->Number = 1;
                        break;
                    case 8:
                        frame->Rflags |= 0x200;
                        break;
                    case 9:
                        frame->FsBase = ulong.MaxValue;
                        break;
                    case 10:
                        *(uint*)(data + 0x418) = 0x10000;
                        break;
                    case 11:
                        *(uint*)(data + 0x41c) = 0xffbf;
                        break;
                }
                HostCheck.True(!Transition.CaptureFaultUserImage(data, Transition.DataBytes, image, 728, 0xffff), "capture variant " + variant);
                HostCheck.True(new ReadOnlySpan<byte>(image, 4096).IndexOfAnyExcept((byte)0xa5) < 0);
            }
        }
        finally { NativeMemory.AlignedFree(data); NativeMemory.AlignedFree(image); }
    }
}
