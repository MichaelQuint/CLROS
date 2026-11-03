using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Storage;

internal static unsafe partial class LiveVirtioBlkPCIDeviceTests
{
    [HostTest("virtio.blk.binding preserves exact identity command BDF and capability metadata")]
    private static void Binding()
    {
        HostCheck.True(default(VirtioBlkBindingStatus) != VirtioBlkBindingStatus.Success);
        byte[] snapshot = Fixture();
        foreach (byte header in new byte[]
        {
            0,
            0x80
        }

        )
        {
            snapshot[0x0e] = header;
            var binding = Success(snapshot, 255, 31, 7);
            HostCheck.True(binding.Identity == 0x10421af4 && binding.OriginalCommand == 0x407 && binding.Bus == 255 && binding.Device == 31 && binding.Function == 7);
            var caps = binding.Capabilities;
            HostCheck.True(caps.HasCommonConfiguration && caps.HasNotifyConfiguration && caps.HasIsrStatus && caps.HasDeviceConfiguration);
            Check(caps.CommonConfiguration, 1, 0, 0x100, 56, 0);
            Check(caps.NotifyConfiguration, 2, 2, 0x200, 2, 4);
            Check(caps.IsrStatus, 3, 2, 0x300, 1, 0);
            Check(caps.DeviceConfiguration, 4, 4, 0x400, 8, 0);
        }

        Write(snapshot, 0xac, 64);
        Write(snapshot, 0xa8, 0xfffffff8);
        var wider = Success(snapshot);
        HostCheck.True(wider.Capabilities.DeviceConfiguration.Length == 64 && wider.Capabilities.DeviceConfiguration.Offset == 0xfffffff8);
        // Numeric binding preserves declarations; actual 64-bit BAR bounds belong to the adapter.
    }

    [HostTest("virtio.blk.binding rejects identity header and incomplete or malformed capabilities atomically")]
    private static void InvalidMetadata()
    {
        foreach (uint identity in new uint[]
        {
            0,
            0x10411af4,
            0x10401af4,
            0x10011af4,
            0x10421234
        }

        )
        {
            byte[] b = Fixture();
            Write(b, 0, identity);
            Reject(b, VirtioBlkBindingStatus.WrongIdentity);
        }

        byte[] absent = Fixture();
        Write(absent, 0, 0x1042ffff);
        Reject(absent, VirtioBlkBindingStatus.AbsentDevice);
        foreach ((int offset, byte value, VirtioBlkBindingStatus status) in new[]
        {
            (0x0e, (byte)1, VirtioBlkBindingStatus.UnsupportedHeader),
            (0x0e, (byte)2, VirtioBlkBindingStatus.UnsupportedHeader),
            (6, (byte)0, VirtioBlkBindingStatus.MissingCapabilities),
            (0x81, (byte)0, VirtioBlkBindingStatus.MissingCapabilities),
            (0xa4, (byte)6, VirtioBlkBindingStatus.MissingCapabilities),
            (0xa3, (byte)0xff, VirtioBlkBindingStatus.MissingCapabilities),
            (0xa1, (byte)0x40, VirtioBlkBindingStatus.InvalidCapabilities),
            (0xa1, (byte)0xa0, VirtioBlkBindingStatus.InvalidCapabilities),
            (0xa1, (byte)0xfd, VirtioBlkBindingStatus.InvalidCapabilities),
            (0x42, (byte)15, VirtioBlkBindingStatus.InvalidCapabilities),
            (0x42, (byte)33, VirtioBlkBindingStatus.InvalidCapabilities),
            (0x62, (byte)19, VirtioBlkBindingStatus.InvalidCapabilities),
            (0xa2, (byte)255, VirtioBlkBindingStatus.InvalidCapabilities),
            (0x70, (byte)3, VirtioBlkBindingStatus.InvalidCapabilities),
            (0xa8, (byte)2, VirtioBlkBindingStatus.InvalidCapabilities)
        }

        )
        {
            byte[] b = Fixture();
            b[offset] = value;
            Reject(b, status);
        }

        for (uint length = 0; length < 8; length++)
        {
            byte[] b = Fixture();
            Write(b, 0xac, length);
            Reject(b, length == 0 ? VirtioBlkBindingStatus.InvalidCapabilities : VirtioBlkBindingStatus.DeviceConfigurationTooShort);
        }
    }

    [HostTest("virtio.blk.binding bounds exact snapshot storage and BDF without mutation")]
    private static void Storage()
    {
        byte[] b = Fixture();
        fixed (byte* bytes = b)
        {
            foreach (uint length in new uint[]
            {
                0,
                255,
                257,
                uint.MaxValue
            }

            )
            {
                HostCheck.Equal(VirtioBlkBindingStatus.InvalidSnapshot, LiveVirtioBlkPCIDevice.Parse(bytes, length, 0, 0, 0, out var binding));
                Empty(binding);
            }

            foreach ((byte device, byte function) in new (byte, byte)[]
            {
                (32, 0),
                (0, 8),
                (255, 255)
            }

            )
            {
                HostCheck.Equal(VirtioBlkBindingStatus.InvalidBdf, LiveVirtioBlkPCIDevice.Parse(bytes, 256, 0, device, function, out var binding));
                Empty(binding);
            }
        }

        HostCheck.Equal(VirtioBlkBindingStatus.InvalidSnapshot, LiveVirtioBlkPCIDevice.Parse(null, 256, 0, 0, 0, out var empty));
        Empty(empty);
        HostCheck.Equal(VirtioBlkBindingStatus.InvalidSnapshot, LiveVirtioBlkPCIDevice.Parse((byte*)(ulong.MaxValue - 127), 256, 0, 0, 0, out empty));
        Empty(empty);
    }

    [HostTest("virtio.blk.binding reads one bounded snapshot and clears partial read failures")]
    private static void Snapshot()
    {
        BoundedPciAccess access = default;
        HostCheck.Equal(VirtioBlkBindingStatus.ReadFailed, LiveVirtioBlkPCIDevice.Read(ref access, 2, 3, 1, out var binding));
        Empty(binding);
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 0, 0, 255, 2, 2, out var window));
        byte[] b = Fixture();
        byte[] before = (byte[])b.Clone();
        fixed (byte* bytes = b)
        {
            ReadContext context = new()
            {
                Bytes = bytes,
                Page = 0xe0219000,
                Access = &access
            };
            HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryOpen(window, &context, &ReadWord, out access));
            HostCheck.Equal(VirtioBlkBindingStatus.Success, LiveVirtioBlkPCIDevice.Read(ref access, 2, 3, 1, out binding));
            HostCheck.True(binding.Bus == 2 && binding.Device == 3 && binding.Function == 1 && binding.OriginalCommand == 0x407 && binding.Identity == 0x10421af4 && binding.Capabilities.DeviceConfiguration.Length == 8 && context.Calls == 64 && !context.BadAddress);
            HostCheck.Equal(VirtioBlkBindingStatus.ReadFailed, LiveVirtioBlkPCIDevice.Read(ref access, 1, 3, 1, out binding));
            Empty(binding);
            HostCheck.Equal(VirtioBlkBindingStatus.InvalidBdf, LiveVirtioBlkPCIDevice.Read(ref access, 2, 32, 0, out binding));
            Empty(binding);
            HostCheck.Equal(VirtioBlkBindingStatus.InvalidBdf, LiveVirtioBlkPCIDevice.Read(ref access, 2, 0, 8, out binding));
            Empty(binding);
            HostCheck.Equal(64U, context.Calls);
            HostCheck.True(b.AsSpan().SequenceEqual(before));
            context.Calls = 0;
            context.RevokeAt = 4;
            HostCheck.Equal(VirtioBlkBindingStatus.ReadFailed, LiveVirtioBlkPCIDevice.Read(ref access, 2, 3, 1, out binding));
            Empty(binding);
            HostCheck.True(context.Calls == 4 && !context.BadAddress && b.AsSpan().SequenceEqual(before));
            context.Calls = 0;
            context.RevokeAt = 0;
            HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryOpen(window, &context, &ReadWord, out access));
            Write(b, 0, 0x10411af4);
            HostCheck.Equal(VirtioBlkBindingStatus.WrongIdentity, LiveVirtioBlkPCIDevice.Read(ref access, 2, 3, 1, out binding));
            Empty(binding);
            HostCheck.Equal(64U, context.Calls);
            context.Calls = 0;
            Write(b, 0, 0xffffffff);
            HostCheck.Equal(VirtioBlkBindingStatus.AbsentDevice, LiveVirtioBlkPCIDevice.Read(ref access, 2, 3, 1, out binding));
            Empty(binding);
            HostCheck.Equal(1U, context.Calls);
        }
    }

    private static byte[] Fixture()
    {
        byte[] b = new byte[256];
        Write(b, 0, 0x10421af4);
        Write(b, 4, 0x00100407);
        b[0x34] = 0x40;
        Record(b, 0x40, 0x60, 1, 0, 0x100, 56);
        Record(b, 0x60, 0x80, 2, 2, 0x200, 2);
        Record(b, 0x80, 0xa0, 3, 2, 0x300, 1);
        Record(b, 0xa0, 0, 4, 4, 0x400, 8);
        return b;
    }

    private static void Record(byte[] b, int node, byte next, byte type, byte bar, uint offset, uint bytes)
    {
        b[node] = 9;
        b[node + 1] = next;
        b[node + 2] = type == 2 ? (byte)20 : (byte)16;
        b[node + 3] = type;
        b[node + 4] = bar;
        Write(b, node + 8, offset);
        Write(b, node + 12, bytes);
        if (type == 2)
            Write(b, node + 16, 4);
    }

    private static void Write(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static VirtioBlkPciBinding Success(byte[] b, byte bus = 0, byte device = 0, byte function = 0)
    {
        byte[] before = (byte[])b.Clone();
        fixed (byte* bytes = b)
        {
            HostCheck.Equal(VirtioBlkBindingStatus.Success, LiveVirtioBlkPCIDevice.Parse(bytes, 256, bus, device, function, out var binding));
            HostCheck.True(b.AsSpan().SequenceEqual(before));
            return binding;
        }
    }

    private static void Reject(byte[] b, VirtioBlkBindingStatus status)
    {
        var binding = Success(Fixture());
        byte[] before = (byte[])b.Clone();
        fixed (byte* bytes = b)
            HostCheck.Equal(status, LiveVirtioBlkPCIDevice.Parse(bytes, 256, 0, 0, 0, out binding));
        Empty(binding);
        HostCheck.True(b.AsSpan().SequenceEqual(before));
    }

    private static void Empty(VirtioBlkPciBinding b)
    {
        HostCheck.True(b.Identity == 0 && b.OriginalCommand == 0 && b.Bus == 0 && b.Device == 0 && b.Function == 0 && !b.Capabilities.HasCommonConfiguration && !b.Capabilities.HasNotifyConfiguration && !b.Capabilities.HasIsrStatus && !b.Capabilities.HasDeviceConfiguration);
        Check(b.Capabilities.CommonConfiguration, 0, 0, 0, 0, 0);
        Check(b.Capabilities.NotifyConfiguration, 0, 0, 0, 0, 0);
        Check(b.Capabilities.IsrStatus, 0, 0, 0, 0, 0);
        Check(b.Capabilities.DeviceConfiguration, 0, 0, 0, 0, 0);
    }

    private static void Check(VirtioPciCapabilityMetadata c, byte type, byte bar, uint offset, uint bytes, uint multiplier) => HostCheck.True((byte)c.Type == type && c.Bar == bar && c.Offset == offset && c.Length == bytes && c.NotifyOffsetMultiplier == multiplier);
    [UnmanagedCallersOnly]
    private static uint ReadWord(void* state, ulong address)
    {
        ReadContext* c = (ReadContext*)state;
        c->Calls++;
        if (address < c->Page || address - c->Page > 252 || (address & 3) != 0)
        {
            c->BadAddress = true;
            return 0xffffffff;
        }

        uint value = BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(c->Bytes + (address - c->Page), 4));
        if (c->RevokeAt != 0 && c->Calls == c->RevokeAt)
            BoundedPCIConfigurationSpaceInterface.Revoke(ref *c->Access);
        return value;
    }
}
