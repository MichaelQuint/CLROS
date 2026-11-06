using System.Buffers.Binary;
using System.Runtime.InteropServices;

internal static unsafe class KestrelSignalProbeTests
{
    // ABI and copied-value controls only: no signal delivery or native waiter is fabricated.
    [HostTest("m10-012 signal probe uses exact Linux x64 action stack and packed epoll layouts")]
    private static void Layout()
    {
        HostCheck.Equal(12, KestrelSignalProbeAbi.Signal);
        HostCheck.Equal(32, sizeof(KestrelSignalProbeAbi.KernelAction));
        HostCheck.Equal(24, sizeof(KestrelSignalProbeAbi.AltStack));
        HostCheck.Equal(12, sizeof(KestrelSignalProbeAbi.EpollEvent));
        Offset<KestrelSignalProbeAbi.KernelAction>("Handler", 0);
        Offset<KestrelSignalProbeAbi.KernelAction>("Flags", 8);
        Offset<KestrelSignalProbeAbi.KernelAction>("Restorer", 16);
        Offset<KestrelSignalProbeAbi.KernelAction>("Mask", 24);
        Offset<KestrelSignalProbeAbi.AltStack>("Address", 0);
        Offset<KestrelSignalProbeAbi.AltStack>("Flags", 8);
        Offset<KestrelSignalProbeAbi.AltStack>("Padding", 12);
        Offset<KestrelSignalProbeAbi.AltStack>("Bytes", 16);
        Offset<KestrelSignalProbeAbi.EpollEvent>("Events", 0);
        Offset<KestrelSignalProbeAbi.EpollEvent>("Data", 4);
    }

    [HostTest("m10-012 signal probe uses distinct original and recovery pipe byte witnesses")]
    private static void DistinctPipeBytes()
    {
        HostCheck.Equal((byte)0x5a, KestrelSignalProbeAbi.WakeByte);
        HostCheck.Equal((byte)0x6b, KestrelSignalProbeAbi.RecoveryByte);
        HostCheck.True(KestrelSignalProbeAbi.WakeByte != KestrelSignalProbeAbi.RecoveryByte);
    }

    [HostTest("m10-012 signal probe packed event and field comparisons retain every copied bit")]
    private static void BytesAndEquality()
    {
        var value = new KestrelSignalProbeAbi.EpollEvent { Events = 0x10203040, Data = 0x1122334455667788 };
        ReadOnlySpan<byte> bytes = new(&value, sizeof(KestrelSignalProbeAbi.EpollEvent));
        HostCheck.Equal(0x10203040U, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        HostCheck.Equal(0x1122334455667788UL, BinaryPrimitives.ReadUInt64LittleEndian(bytes[4..]));
        var action = new KestrelSignalProbeAbi.KernelAction { Handler = 17, Flags = 18, Restorer = 19, Mask = 20 };
        HostCheck.True(KestrelSignalProbeAbi.SameAction(action, action));
        for (int i = 0; i < 4; i++)
        {
            var changed = action;
            ((ulong*)&changed)[i] ^= 1;
            HostCheck.True(!KestrelSignalProbeAbi.SameAction(action, changed));
        }
        var stack = new KestrelSignalProbeAbi.AltStack { Address = 21, Flags = 22, Padding = 23, Bytes = 24 };
        HostCheck.True(KestrelSignalProbeAbi.SameStack(stack, stack));
        for (int i = 0; i < sizeof(KestrelSignalProbeAbi.AltStack); i++)
        {
            var changed = stack;
            ((byte*)&changed)[i] ^= 1;
            HostCheck.True(!KestrelSignalProbeAbi.SameStack(stack, changed));
        }
    }

    [HostTest("m10-012 signal probe action admission requires native handler restorer and all three flags")]
    private static void ActionAdmission()
    {
        var saved = new KestrelSignalProbeAbi.KernelAction { Flags = ulong.MaxValue, Restorer = 99, Mask = 101 };
        HostCheck.True(KestrelSignalProbeAbi.IsDefaultAction(saved));
        saved.Handler = 1;
        HostCheck.True(!KestrelSignalProbeAbi.IsDefaultAction(saved));
        var installed = new KestrelSignalProbeAbi.KernelAction { Handler = 2, Flags = 0x14000004, Restorer = 3, Mask = 8 };
        HostCheck.True(KestrelSignalProbeAbi.IsInstalledAction(installed));
        foreach (ulong flag in new ulong[] { 4, 0x04000000, 0x10000000 })
        {
            var bad = installed;
            bad.Flags &= ~flag;
            HostCheck.True(!KestrelSignalProbeAbi.IsInstalledAction(bad));
        }
        foreach (ulong handler in new ulong[] { 0, 1 })
        {
            var bad = installed;
            bad.Handler = handler;
            HostCheck.True(!KestrelSignalProbeAbi.IsInstalledAction(bad));
        }
        installed.Restorer = 0;
        HostCheck.True(!KestrelSignalProbeAbi.IsInstalledAction(installed));
    }

    [HostTest("m10-012 signal probe refuses every native entry before loading libc on unsupported hosts")]
    private static void PlatformRefusal()
    {
        HostCheck.Equal(OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64,
            KestrelSignalProbeAbi.Supported);
        if (KestrelSignalProbeAbi.Supported)
            return;
        Refused(() =>
        {
            _ = KestrelSignalProbeAbi.ReadAction();
        });
        Refused(() => KestrelSignalProbeAbi.SetAction(default));
        Refused(() =>
        {
            _ = KestrelSignalProbeAbi.ReadMask();
        });
        Refused(() => KestrelSignalProbeAbi.SetMask(0));
        Refused(() =>
        {
            _ = KestrelSignalProbeAbi.ReadAltStack();
        });
        Refused(() =>
        {
            _ = KestrelSignalProbeAbi.GetPid();
        });
        Refused(() =>
        {
            _ = KestrelSignalProbeAbi.GetTid();
        });
        Refused(() => KestrelSignalProbeAbi.CreatePipe(out _, out _));
        Refused(() =>
        {
            _ = KestrelSignalProbeAbi.CreateEpoll();
        });
        Refused(() => KestrelSignalProbeAbi.AddRead(3, 4, 5));
        Refused(() =>
        {
            _ = KestrelSignalProbeAbi.Wait(3, 0);
        });
        Refused(() => KestrelSignalProbeAbi.WriteWake(4));
        Refused(() => KestrelSignalProbeAbi.WriteByte(4, 0x6b));
        Refused(() =>
        {
            _ = KestrelSignalProbeAbi.ReadByte(3);
        });
        Refused(() => KestrelSignalProbeAbi.Close(4));
        Refused(() => KestrelSignalProbeAbi.SendSignal(1, 2));
        Refused(KestrelSignalProbeAbi.YieldBeforeRace);
    }

    private static void Offset<T>(string field, int expected) where T : struct =>
        HostCheck.Equal(expected, Marshal.OffsetOf<T>(field).ToInt32());
    private static void Refused(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (PlatformNotSupportedException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
