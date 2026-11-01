namespace OS.Drivers;
public static unsafe class ECAMPageVendorDeviceHeader
{
    // Revalidate the complete retained MCFG, then select at most four buses of one
    // allocation. All allocations are checked for reserved bits and overlapping authority.
    public static bool TryWindow(byte* mcfg, ulong bytes, uint allocation, byte firstBus, byte lastBus, out BoundedPciWindow window)
    {
        window = default;
        if (mcfg == null || bytes < 60 || bytes > 300 || (bytes - 44) % 16 != 0 || (ulong)mcfg > ~0UL - bytes || Read32(mcfg) != 0x4746434d || Read32(mcfg + 4) != bytes || mcfg[8] != 1 || Read64(mcfg + 36) != 0 || allocation >= (bytes - 44) / 16)
            return false;
        uint checksum = 0;
        for (uint i = 0; i < bytes; i++)
            checksum += mcfg[i];
        if ((checksum & 255) != 0)
            return false;
        for (uint i = 0; i < (bytes - 44) / 16; i++)
        {
            byte* entry = mcfg + 44 + i * 16;
            ulong origin = Read64(entry);
            uint segment = (uint)entry[8] | ((uint)entry[9] << 8);
            uint first = entry[10], last = entry[11];
            if (Read32(entry + 12) != 0 || first > last || origin < 0x100000 || (origin & 0xfffff) != 0 || origin > 0x100000000UL - (((ulong)last + 1) << 20))
                return false;
            ulong start = origin + ((ulong)first << 20), end = origin + (((ulong)last + 1) << 20);
            for (uint j = 0; j < i; j++)
            {
                byte* prior = mcfg + 44 + j * 16;
                ulong priorStart = Read64(prior) + ((ulong)prior[10] << 20);
                ulong priorEnd = Read64(prior) + (((ulong)prior[11] + 1) << 20);
                uint priorSegment = (uint)prior[8] | ((uint)prior[9] << 8);
                if ((start < priorEnd && priorStart < end) || (segment == priorSegment && first <= prior[11] && prior[10] <= last))
                    return false;
            }
        }

        byte* selected = mcfg + 44 + allocation * 16;
        return BoundedPCIConfigurationSpaceInterface.TryWindow(Read64(selected), (ushort)((uint)selected[8] | ((uint)selected[9] << 8)), selected[10], selected[11], firstBus, lastBus, out window);
    }

    // ACPI grants positive ECAM authority; the original owned EFI map rules out RAM.
    // Firmware may omit the aperture. Any overlap must cover the whole requested
    // window with exactly one Reserved/MMIO descriptor. Later allocations are separate.
    public static bool AdmitWindow(BoundedPciWindow window, OS.Boot.FirmwareMemoryMapRecord map)
    {
        if (!window.IsValid || !map.HasOwnedCopy() || map.DescriptorVersion != 1 || (map.BufferAddress & 7) != 0 || map.DescriptorSize < 40 || map.DescriptorSize > 256 || (map.DescriptorSize & 7) != 0 || map.DescriptorCount == 0 || map.DescriptorCount > 4096 || map.MapSize != map.DescriptorSize * map.DescriptorCount || map.MapSize > map.BufferSize || map.BufferAddress > ~0UL - map.BufferSize || (window.Start < map.BufferAddress + map.BufferSize && map.BufferAddress < window.Start + window.Bytes))
            return false;
        uint covering = 0;
        for (ulong i = 0; i < map.DescriptorCount; i++)
        {
            byte* descriptor = (byte*)(map.BufferAddress + i * map.DescriptorSize);
            uint type = Read32(descriptor);
            ulong start = Read64(descriptor + 8), pages = Read64(descriptor + 24);
            if ((start & 4095) != 0 || pages == 0 || pages > (~0UL - start) / 4096)
                return false;
            ulong end = start + pages * 4096;
            if (window.Start >= end || start >= window.Start + window.Bytes)
                continue;
            if ((type != 0 && type != 11) || start > window.Start || end < window.Start + window.Bytes || ++covering != 1)
                return false;
        }

        return true;
    }

    public static BoundedPciStatus ReadHeader(ref BoundedPciAccess access, byte bus, byte device, byte function, out BoundedPciHeader header)
    {
        header = default;
        uint* words = stackalloc uint[16];
        var status = ReadSnapshot(ref access, bus, device, function, words, 16);
        if (status != BoundedPciStatus.Success)
            return status;
        byte type = (byte)(words[3] >> 16);
        if ((type & 0x7f) > 1)
            return BoundedPciStatus.UnsupportedHeader;
        header = new BoundedPciHeader(words[0], words[2], words[1], words[6], type, (byte)words[13]);
        return BoundedPciStatus.Success;
    }

    // No writes and no reads beyond the caller's admitted function page. A stable
    // device/configuration is required; hotplug and access faults are unsupported.
    public static BoundedPciStatus ReadSnapshot(ref BoundedPciAccess access, byte bus, byte device, byte function, uint* words, uint count)
    {
        if (words == null || (count != 16 && count != 64) || ((ulong)words & 3) != 0 || (ulong)words > ~0UL - count * 4UL)
            return BoundedPciStatus.InvalidAccess;
        var status = BoundedPCIConfigurationSpaceInterface.Read32(ref access, bus, device, function, 0, out uint identity);
        if (status != BoundedPciStatus.Success)
            return status;
        if ((identity & 65535) == 65535)
            return BoundedPciStatus.AbsentDevice;
        words[0] = identity;
        for (uint i = 1; i < count; i++)
        {
            status = BoundedPCIConfigurationSpaceInterface.Read32(ref access, bus, device, function, i * 4, out uint value);
            if (status != BoundedPciStatus.Success)
                return status;
            words[i] = value;
        }

        return BoundedPciStatus.Success;
    }

    private static uint Read32(byte* p) => OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(p);
    private static ulong Read64(byte* p) => OS.Std.Binary.EndianPrimitives.ReadUInt64LittleEndian(p);
}
