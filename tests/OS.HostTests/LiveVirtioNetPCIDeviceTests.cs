using System.Buffers.Binary;
using OS.Drivers;

internal static unsafe class LiveVirtioNetPCIDeviceTests
{
    [HostTest("virtio.net.binding preserves modern identity BDF command and independent capability bytes")]
    private static void Binding()
    {
        byte[] bytes = Snapshot();
        fixed (byte* p = bytes)
        {
            foreach (byte header in new byte[] { 0, 0x80 })
            {
                bytes[14] = header;
                HostCheck.Equal(VirtioNetBindingStatus.Success, LiveVirtioNetPCIDevice.Parse(p, 256, 255, 31, 7, out var b));
                HostCheck.True(b.Identity == 0x10411af4 && b.OriginalCommand == 0x407 && b.Bus == 255 && b.Device == 31 && b.Function == 7);
                HostCheck.True(b.Capabilities.CommonConfiguration.Length == 56 && b.Capabilities.NotifyConfiguration.NotifyOffsetMultiplier == 4 &&
                    b.Capabilities.DeviceConfiguration.Offset == 0x2000 && b.Capabilities.DeviceConfiguration.Length == 6);
            }
        }
    }

    [HostTest("virtio.net.binding rejects wrong absent truncated bridge cyclic and missing metadata atomically")]
    private static void Rejection()
    {
        for (int fault = 0; fault < 9; fault++)
        {
            byte[] bytes = Snapshot();
            VirtioNetBindingStatus expected;
            switch (fault)
            {
                case 0: Write(bytes, 0, 0x10421af4); expected = VirtioNetBindingStatus.WrongIdentity; break;
                case 1: Write(bytes, 0, 0x1041ffff); expected = VirtioNetBindingStatus.AbsentDevice; break;
                case 2: bytes[14] = 1; expected = VirtioNetBindingStatus.UnsupportedHeader; break;
                case 3: bytes[0xa1] = 0x40; expected = VirtioNetBindingStatus.InvalidCapabilities; break;
                case 4: bytes[0x81] = 0; expected = VirtioNetBindingStatus.MissingCapabilities; break;
                case 5: bytes[0x63] = 6; expected = VirtioNetBindingStatus.MissingCapabilities; break;
                case 6: expected = VirtioNetBindingStatus.InvalidSnapshot; break;
                case 7: expected = VirtioNetBindingStatus.InvalidBdf; break;
                default: expected = VirtioNetBindingStatus.InvalidSnapshot; break;
            }
            byte[] original = (byte[])bytes.Clone();
            fixed (byte* p = bytes)
            {
                HostCheck.Equal(expected, LiveVirtioNetPCIDevice.Parse(fault == 8 ? null : p, fault == 6 ? 255U : 256U,
                    0, fault == 7 ? (byte)32 : (byte)6, 0, out var b));
                HostCheck.True(b.Identity == 0 && b.OriginalCommand == 0 && !b.Capabilities.HasCommonConfiguration);
            }
            HostCheck.True(bytes.AsSpan().SequenceEqual(original));
        }
        BoundedPciAccess access = default;
        HostCheck.Equal(VirtioNetBindingStatus.ReadFailed, LiveVirtioNetPCIDevice.Read(ref access, 0, 6, 0, out _));
    }

    internal static byte[] Snapshot()
    {
        byte[] b = new byte[256]; Write(b, 0, 0x10411af4); Write(b, 4, 0x00100407); b[0x34] = 0x40;
        Capability(0x40, 0x60, 1, 0, 56); Capability(0x60, 0x80, 2, 0x3000, 2);
        Write(b, 0x70, 4); Capability(0x80, 0xa0, 3, 0x1000, 1); Capability(0xa0, 0, 4, 0x2000, 6);
        return b;
        void Capability(int at, byte next, byte type, uint offset, uint length)
        {
            b[at] = 9; b[at + 1] = next; b[at + 2] = type == 2 ? (byte)20 : (byte)16;
            b[at + 3] = type; b[at + 4] = 4; Write(b, at + 8, offset); Write(b, at + 12, length);
        }
    }
    private static void Write(byte[] b, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset), value);
}
