using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class ModernVirtioPCICapabilitiesLiveConfigurationSpaceTests
{
    [HostTest("virtio.capabilities parses independent modern snapshot without mutation")]
    private static void Valid()
    {
        HostCheck.True(default(VirtioCapabilityStatus) != VirtioCapabilityStatus.Success);
        byte[] config = Fixture();
        byte[] before = (byte[])config.Clone();
        VirtioPciCapabilitySet result = Success(config);
        HostCheck.True(result.HasCommonConfiguration && result.HasNotifyConfiguration && result.HasIsrStatus && result.HasDeviceConfiguration);
        Check(result.CommonConfiguration, 1, 0, 0, 56, 0);
        Check(result.NotifyConfiguration, 2, 2, 0x100, 2, 4);
        Check(result.IsrStatus, 3, 2, 0x200, 1, 0);
        Check(result.DeviceConfiguration, 4, 4, 0x300, 1, 0);
        HostCheck.True(config.AsSpan().SequenceEqual(before));
        foreach (ushort id in new ushort[]
        {
            0x1040,
            0x107f
        }

        )
        {
            BinaryPrimitives.WriteUInt16LittleEndian(config.AsSpan(2), id);
            config[0x0e] = 0x80; // Multifunction remains a type-zero header.
            Success(config);
        }

        config = Fixture();
        Write(config, 0x48, 0xfffffff0);
        Write(config, 0x6c, uint.MaxValue);
        result = Success(config); // Crossing 4 GiB is metadata; actual BAR bounds belong to the planner.
        Check(result.CommonConfiguration, 1, 0, 0xfffffff0, 56, 0);
        Check(result.NotifyConfiguration, 2, 2, 0x100, uint.MaxValue, 4);
    }

    [HostTest("virtio.capabilities ignores reserved records accepts extensions and keeps first instance")]
    private static void ForwardCompatibility()
    {
        byte[] config = Fixture();
        config[0xa1] = 0xb0;
        config[0xb0] = 9;
        config[0xb1] = 0xb4;
        config[0xb2] = 4;
        config[0xb3] = 0xfe;
        config[0xb4] = 9;
        config[0xb5] = 0xbc;
        config[0xb6] = 5;
        config[0xb7] = 1;
        config[0xb8] = 6;
        config[0xbc] = 5; // Unrelated capability only supplies its two-byte list header.
        Success(config);
        config = Fixture();
        foreach (int node in new[]
        {
            0x40,
            0x60,
            0x80,
            0xa0
        }

        )
            config[node + 2] = 32;
        Success(config);
        config = Fixture();
        config[0xa1] = 0xb0;
        Record(config, 0xb0, 0xc0, 1, 5, 0x1000, 64);
        Record(config, 0xc0, 0, 2, 3, 0x2000, 4, 8);
        VirtioPciCapabilitySet result = Success(config);
        Check(result.CommonConfiguration, 1, 0, 0, 56, 0);
        Check(result.NotifyConfiguration, 2, 2, 0x100, 2, 4);
        foreach (uint multiplier in new uint[]
        {
            0,
            2,
            0x80000000
        }

        )
        {
            config = Fixture();
            Write(config, 0x70, multiplier);
            HostCheck.Equal(multiplier, Success(config).NotifyConfiguration.NotifyOffsetMultiplier);
        }
    }

    [HostTest("virtio.capabilities rejects malformed extents complete-chain cycles and missing records atomically")]
    private static void InvalidRecords()
    {
        foreach ((int offset, byte value, VirtioCapabilityStatus status)in new[]
        {
            (0x42, (byte)15, VirtioCapabilityStatus.InvalidCapability),
            (0x62, (byte)19, VirtioCapabilityStatus.InvalidCapability),
            (0x82, (byte)0, VirtioCapabilityStatus.InvalidCapability),
            (0xa2, (byte)255, VirtioCapabilityStatus.InvalidCapability),
            (0x42, (byte)33, VirtioCapabilityStatus.OverlappingCapabilities),
            (0xa1, (byte)0x40, VirtioCapabilityStatus.InvalidChain),
            (0xa1, (byte)0xa0, VirtioCapabilityStatus.InvalidChain),
            (0xa1, (byte)0xfd, VirtioCapabilityStatus.InvalidChain),
            (0xa1, (byte)0x3c, VirtioCapabilityStatus.InvalidChain),
            (0x81, (byte)0, VirtioCapabilityStatus.MissingRequired),
            (0x06, (byte)0, VirtioCapabilityStatus.MissingRequired),
            (0xa3, (byte)0xff, VirtioCapabilityStatus.MissingRequired),
            (0xa4, (byte)0xff, VirtioCapabilityStatus.MissingRequired),
            (0x00, (byte)0, VirtioCapabilityStatus.UnsupportedDevice),
            (0x02, (byte)0x3f, VirtioCapabilityStatus.UnsupportedDevice),
            (0x02, (byte)0x80, VirtioCapabilityStatus.UnsupportedDevice),
            (0x0e, (byte)1, VirtioCapabilityStatus.UnsupportedHeader)
        }

        )
        {
            byte[] config = Fixture();
            config[offset] = value;
            Reject(config, status);
        }

        byte[] tail = Fixture();
        tail[0xa1] = 0xfc;
        tail[0xfc] = 9;
        tail[0xfe] = 4;
        tail[0xff] = 1;
        Reject(tail, VirtioCapabilityStatus.InvalidCapability);
        tail[0xff] = 0xfe;
        Success(tail); // Minimal unknown record ends exactly at byte 256.
        tail[0xfe] = 5;
        Reject(tail, VirtioCapabilityStatus.InvalidCapability);
        tail = Fixture();
        tail[0xa1] = 0xb0;
        tail[0xb0] = 9;
        tail[0xb1] = 0xc0;
        tail[0xb2] = 32;
        tail[0xb3] = 0xfe;
        tail[0xc0] = 1;
        Reject(tail, VirtioCapabilityStatus.OverlappingCapabilities);
        tail = Fixture();
        tail[0] = 255;
        tail[1] = 255;
        Reject(tail, VirtioCapabilityStatus.AbsentDevice);
    }

    [HostTest("virtio.capabilities enforces bounded region sizes alignment and notify multiplier")]
    private static void InvalidRegions()
    {
        foreach ((int offset, uint value)in new[]
        {
            (0x4c, 55U),
            (0x6c, 1U),
            (0x8c, 0U),
            (0xac, 0U),
            (0x48, 2U),
            (0x68, 1U),
            (0xa8, 2U),
            (0x70, 1U),
            (0x70, 3U),
            (0x70, uint.MaxValue)
        }

        )
        {
            byte[] config = Fixture();
            Write(config, offset, value);
            Reject(config, VirtioCapabilityStatus.InvalidCapability);
        }
    }

    [HostTest("virtio.capabilities rejects null truncated and overflowing snapshot storage")]
    private static void Storage()
    {
        byte[] config = Fixture();
        fixed (byte* bytes = config)
        {
            foreach (uint length in new uint[]
            {
                0,
                63,
                255
            }

            )
            {
                HostCheck.Equal(VirtioCapabilityStatus.InvalidSnapshot, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Parse(bytes, length, out var result));
                Empty(result);
            }
        }

        HostCheck.Equal(VirtioCapabilityStatus.InvalidSnapshot, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Parse(null, 256, out var empty));
        Empty(empty);
        HostCheck.Equal(VirtioCapabilityStatus.InvalidSnapshot, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Parse((byte*)(ulong.MaxValue - 127), 256, out empty));
        Empty(empty);
    }

    [HostTest("virtio.capabilities snapshot wrapper distinguishes read failure and parse failure")]
    private static void SnapshotRead()
    {
        BoundedPciAccess access = default;
        HostCheck.Equal(VirtioCapabilityStatus.ReadFailed, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Read(ref access, 0, 0, 0, out var result));
        Empty(result);
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 0, 0, 255, 0, 0, out var window));
        byte[] config = Fixture();
        fixed (byte* bytes = config)
        {
            ReadContext context = new()
            {
                Bytes = bytes
            };
            HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryOpen(window, &context, &ReadWord, out access));
            HostCheck.Equal(VirtioCapabilityStatus.Success, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Read(ref access, 0, 0, 0, out result));
            HostCheck.Equal(64U, context.Calls);
            Check(result.NotifyConfiguration, 2, 2, 0x100, 2, 4);
            HostCheck.Equal(VirtioCapabilityStatus.ReadFailed, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Read(ref access, 1, 0, 0, out result));
            Empty(result);
            HostCheck.Equal(64U, context.Calls);
            config[0xa1] = 0x40;
            HostCheck.Equal(VirtioCapabilityStatus.InvalidChain, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Read(ref access, 0, 0, 0, out result));
            Empty(result);
            HostCheck.Equal(128U, context.Calls);
            config[0] = 255;
            config[1] = 255;
            HostCheck.Equal(VirtioCapabilityStatus.AbsentDevice, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Read(ref access, 0, 0, 0, out result));
            Empty(result);
            HostCheck.Equal(129U, context.Calls);
        }
    }

    private static byte[] Fixture()
    {
        byte[] config = new byte[256];
        config[0] = 0xf4;
        config[1] = 0x1a;
        config[2] = 0x42;
        config[3] = 0x10;
        config[6] = 0x10;
        config[0x34] = 0x40;
        Record(config, 0x40, 0x60, 1, 0, 0, 56);
        Record(config, 0x60, 0x80, 2, 2, 0x100, 2, 4);
        Record(config, 0x80, 0xa0, 3, 2, 0x200, 1);
        Record(config, 0xa0, 0, 4, 4, 0x300, 1);
        return config;
    }

    private static void Record(byte[] bytes, int node, byte next, byte type, byte bar, uint offset, uint length, uint multiplier = 0)
    {
        bytes[node] = 9;
        bytes[node + 1] = next;
        bytes[node + 2] = type == 2 ? (byte)20 : (byte)16;
        bytes[node + 3] = type;
        bytes[node + 4] = bar;
        Write(bytes, node + 8, offset);
        Write(bytes, node + 12, length);
        if (type == 2)
            Write(bytes, node + 16, multiplier);
    }

    private static void Write(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static VirtioPciCapabilitySet Success(byte[] config)
    {
        fixed (byte* bytes = config)
        {
            HostCheck.Equal(VirtioCapabilityStatus.Success, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Parse(bytes, (uint)config.Length, out var result));
            return result;
        }
    }

    private static void Reject(byte[] config, VirtioCapabilityStatus expected)
    {
        byte[] before = (byte[])config.Clone();
        VirtioPciCapabilitySet result = Success(Fixture());
        fixed (byte* bytes = config)
            HostCheck.Equal(expected, ModernVirtioPCICapabilitiesLiveConfigurationSpace.Parse(bytes, (uint)config.Length, out result));
        Empty(result);
        HostCheck.True(config.AsSpan().SequenceEqual(before));
    }

    private static void Empty(VirtioPciCapabilitySet result)
    {
        HostCheck.True(!result.HasCommonConfiguration && !result.HasNotifyConfiguration && !result.HasIsrStatus && !result.HasDeviceConfiguration);
        Check(result.CommonConfiguration, 0, 0, 0, 0, 0);
        Check(result.NotifyConfiguration, 0, 0, 0, 0, 0);
        Check(result.IsrStatus, 0, 0, 0, 0, 0);
        Check(result.DeviceConfiguration, 0, 0, 0, 0, 0);
    }

    private static void Check(VirtioPciCapabilityMetadata result, byte type, byte bar, uint offset, uint length, uint multiplier)
    {
        HostCheck.True((byte)result.Type == type && result.Bar == bar && result.Offset == offset && result.Length == length && result.NotifyOffsetMultiplier == multiplier);
    }

    [UnmanagedCallersOnly]
    private static uint ReadWord(void* context, ulong address)
    {
        ReadContext* input = (ReadContext*)context;
        input->Calls++;
        return BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(input->Bytes + (address - 0xe0000000), 4));
    }
}
