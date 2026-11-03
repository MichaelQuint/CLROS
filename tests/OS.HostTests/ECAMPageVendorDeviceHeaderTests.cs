using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Drivers;

internal static unsafe partial class ECAMPageVendorDeviceHeaderTests
{
    [HostTest("m05.005 retained mcfg validates checksum reserved fields and overlapping allocations")]
    private static void Mcfg()
    {
        byte* table = stackalloc byte[76];
        var bytes = new Span<byte>(table, 76);
        bytes.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x4746434d);
        table[8] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], 60);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[44..], 0xe0000000);
        table[54] = 8;
        table[55] = 31;
        Checksum(bytes[..60]);
        HostCheck.True(ECAMPageVendorDeviceHeader.TryWindow(table, 60, 0, 8, 9, out var window) && window.Start == 0xe0800000);
        HostCheck.True(!ECAMPageVendorDeviceHeader.TryWindow(table, 59, 0, 8, 8, out _));
        HostCheck.True(!ECAMPageVendorDeviceHeader.TryWindow(table, 60, 1, 8, 8, out _));
        HostCheck.True(!ECAMPageVendorDeviceHeader.TryWindow(table, 60, 0, 7, 8, out _));
        foreach (int index in new[]
        {
            0,
            4,
            8,
            36,
            56
        }

        )
        {
            byte old = table[index];
            table[index] ^= 2;
            Checksum(bytes[..60]);
            HostCheck.True(!ECAMPageVendorDeviceHeader.TryWindow(table, 60, 0, 8, 8, out _));
            table[index] = old;
            Checksum(bytes[..60]);
        }

        table[9] ^= 1;
        HostCheck.True(!ECAMPageVendorDeviceHeader.TryWindow(table, 60, 0, 8, 8, out _));
        table[9] ^= 1;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], 76);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[60..], 0xd0000000);
        table[70] = 20;
        table[71] = 40;
        Checksum(bytes);
        HostCheck.True(!ECAMPageVendorDeviceHeader.TryWindow(table, 76, 0, 8, 8, out _)); // Same segment overlapping buses.
        table[68] = 1;
        Checksum(bytes);
        HostCheck.True(ECAMPageVendorDeviceHeader.TryWindow(table, 76, 1, 20, 20, out window) && window.Segment == 1);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[60..], 0xe0000000);
        Checksum(bytes);
        HostCheck.True(!ECAMPageVendorDeviceHeader.TryWindow(table, 76, 0, 8, 8, out _)); // Distinct segments may not alias ECAM.
    }

    [HostTest("m05.005 aperture admission rejects ram partial maps and malformed extents")]
    private static void Map()
    {
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 0, 0, 255, 0, 0, out var window));
        ulong* rows = stackalloc ulong[10];
        new Span<ulong>(rows, 10).Clear();
        rows[0] = 7;
        rows[1] = 0;
        rows[3] = 256;
        rows[5] = 11;
        rows[6] = 0xe0000000;
        rows[8] = 256;
        OS.Boot.FirmwareMemoryMapRecord map = new()
        {
            BufferAddress = (ulong)rows,
            BufferSize = 80,
            MapSize = 80,
            DescriptorSize = 40,
            DescriptorCount = 2,
            DescriptorVersion = 1,
            Flags = 1
        };
        HostCheck.True(ECAMPageVendorDeviceHeader.AdmitWindow(window, map));
        foreach (ulong type in new ulong[]
        {
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            12,
            13,
            uint.MaxValue
        }

        )
        {
            rows[5] = type;
            HostCheck.True(!ECAMPageVendorDeviceHeader.AdmitWindow(window, map));
        }

        rows[5] = 0;
        HostCheck.True(ECAMPageVendorDeviceHeader.AdmitWindow(window, map));
        rows[8] = 1;
        HostCheck.True(!ECAMPageVendorDeviceHeader.AdmitWindow(window, map));
        rows[8] = ulong.MaxValue;
        HostCheck.True(!ECAMPageVendorDeviceHeader.AdmitWindow(window, map));
        rows[8] = 256;
        rows[6]++;
        HostCheck.True(!ECAMPageVendorDeviceHeader.AdmitWindow(window, map));
        rows[6] = 0xf0000000;
        HostCheck.True(ECAMPageVendorDeviceHeader.AdmitWindow(window, map)); // Map may omit positively described MMIO.
        map.Flags = 0;
        HostCheck.True(!ECAMPageVendorDeviceHeader.AdmitWindow(window, map));
    }

    [HostTest("m05.005 header reads are bounded and absence does not consume payload")]
    private static void Header()
    {
        uint* words = stackalloc uint[66];
        new Span<uint>(words, 66).Clear();
        words[0] = 0x29c08086;
        words[1] = 0x00100000;
        words[2] = 0x06000001;
        words[3] = 0x00800000;
        words[13] = 0x40;
        var context = new Context
        {
            Words = words
        };
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryWindow(0xe0000000, 0, 0, 255, 0, 0, out var window));
        HostCheck.True(BoundedPCIConfigurationSpaceInterface.TryOpen(window, &context, &Read, out var access));
        HostCheck.True(ECAMPageVendorDeviceHeader.ReadHeader(ref access, 0, 0, 0, out var header) == BoundedPciStatus.Success);
        HostCheck.True(header.Identity == words[0] && header.HeaderType == 0x80 && header.CapabilityPointer == 0x40 && context.Calls == 16);
        words[0] = uint.MaxValue;
        context.Calls = 0;
        HostCheck.True(ECAMPageVendorDeviceHeader.ReadHeader(ref access, 0, 0, 0, out _) == BoundedPciStatus.AbsentDevice && context.Calls == 1);
        words[0] = 0x12348086;
        words[3] = 0x00020000;
        HostCheck.True(ECAMPageVendorDeviceHeader.ReadHeader(ref access, 0, 0, 0, out _) == BoundedPciStatus.UnsupportedHeader);
        context.Calls = 0;
        HostCheck.True(ECAMPageVendorDeviceHeader.ReadSnapshot(ref access, 0, 0, 0, words, 15) == BoundedPciStatus.InvalidAccess && context.Calls == 0);
        uint* destination = stackalloc uint[66];
        new Span<uint>(destination, 66).Fill(0xdeadbeef);
        words[3] = 0;
        HostCheck.True(ECAMPageVendorDeviceHeader.ReadSnapshot(ref access, 0, 0, 0, destination + 1, 64) == BoundedPciStatus.Success);
        HostCheck.True(context.Calls == 64 && destination[0] == 0xdeadbeef && destination[65] == 0xdeadbeef);
    }

    [UnmanagedCallersOnly]
    private static uint Read(void* pointer, ulong address)
    {
        var c = (Context*)pointer;
        c->Calls++;
        return c->Words[(address & 4095) / 4];
    }

    private static void Checksum(Span<byte> table)
    {
        table[9] = 0;
        byte sum = 0;
        foreach (byte b in table)
            sum = unchecked((byte)(sum + b));
        table[9] = unchecked((byte)-sum);
    }
}
