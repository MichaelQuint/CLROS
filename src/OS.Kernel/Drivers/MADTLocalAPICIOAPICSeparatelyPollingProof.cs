using OS.Std.Binary;

namespace OS.Drivers;
/// <summary>
/// Parses an exact, stable retained MADT byte range (revisions 1..6). Physical
/// addresses are numbers only, bounded to aligned pages below 4 GiB. One enabled
/// legacy xAPIC CPU must match the adapter-supplied actual BSP ID; disabled CPU
/// records are permitted. ISO/NMI declarations are validated but remain inactive:
/// this parser neither enables/routes interrupts nor authenticates CPUs or mappings.
/// Upper GSI coverage requires the adapter's actual IOAPIC version/read-back.
/// </summary>

public static unsafe class MADTLocalAPICIOAPICSeparatelyPollingProof
{
    public const ulong MaximumBytes = 4096;
    public const uint MaximumRecords = 256;
    public static MadtPollingStatus Parse(byte* retained, ulong bytes, uint bspApicId, out MadtPollingTopology topology)
    {
        topology = default;
        if (retained == null || bytes < 44 || bytes > MaximumBytes || (ulong)retained > ~0UL - bytes)
            return MadtPollingStatus.InvalidStorage;
        if (EndianPrimitives.ReadUInt32LittleEndian(retained) != 0x43495041 || EndianPrimitives.ReadUInt32LittleEndian(retained + 4) != bytes || retained[8] == 0 || retained[8] > 6)
            return MadtPollingStatus.InvalidHeader;
        uint sum = 0;
        for (uint i = 0; i < bytes; i++)
            sum += retained[i];
        if ((sum & 255) != 0)
            return MadtPollingStatus.ChecksumFailed;
        uint flags = EndianPrimitives.ReadUInt32LittleEndian(retained + 40);
        if ((flags & ~1U) != 0)
            return MadtPollingStatus.InvalidHeader;
        if (bspApicId >= 255)
            return MadtPollingStatus.UnsupportedTopology;
        ulong localAddress = EndianPrimitives.ReadUInt32LittleEndian(retained + 36), ioAddress = 0;
        if (!Address(localAddress))
            return MadtPollingStatus.InvalidAddress;
        // Four words each for processor UIDs, APIC IDs, LINT0 UIDs and LINT1 UIDs.
        ulong* seen = stackalloc ulong[16];
        uint* isaGsi = stackalloc uint[16];
        for (uint i = 0; i < 16; i++)
        {
            seen[i] = 0;
            isaGsi[i] = 0;
        }

        uint isaSources = 0, wildcardLints = 0, specificLints = 0, gsiBase = 0, maximumIsoGsi = 0;
        byte processorUid = 0, processorApicId = 0, ioId = 0;
        ushort records = 0, isoCount = 0, nmiCount = 0;
        bool enabledCpu = false, hasIo = false, hasOverride = false;
        uint offset = 44;
        while (offset < bytes)
        {
            if (records == MaximumRecords)
                return MadtPollingStatus.TooManyRecords;
            if (bytes - offset < 2)
                return MadtPollingStatus.InvalidRecord;
            byte* entry = retained + offset;
            uint length = entry[1];
            if (length < 2 || length > bytes - offset)
                return MadtPollingStatus.InvalidRecord;
            byte type = entry[0];
            uint expected = type == 0 ? 8U : type == 1 || type == 5 ? 12U : type == 2 ? 10U : type == 4 ? 6U : 0;
            if (expected == 0)
                return MadtPollingStatus.UnsupportedTopology;
            if (length != expected)
                return MadtPollingStatus.InvalidRecord;
            if (type == 0)
            {
                byte uid = entry[2], id = entry[3];
                uint cpuFlags = EndianPrimitives.ReadUInt32LittleEndian(entry + 4);
                if ((cpuFlags & ~3U) != 0)
                    return MadtPollingStatus.InvalidRecord;
                ulong uidBit = 1UL << (uid & 63), idBit = 1UL << (id & 63);
                if ((seen[uid >> 6] & uidBit) != 0 || (seen[4 + (id >> 6)] & idBit) != 0)
                    return MadtPollingStatus.DuplicateRecord;
                seen[uid >> 6] |= uidBit;
                seen[4 + (id >> 6)] |= idBit;
                if ((cpuFlags & 1) != 0)
                {
                    if (enabledCpu)
                        return MadtPollingStatus.UnsupportedTopology;
                    enabledCpu = true;
                    processorUid = uid;
                    processorApicId = id;
                }
            }
            else if (type == 1)
            {
                if (entry[3] != 0)
                    return MadtPollingStatus.InvalidRecord;
                if (hasIo)
                    return MadtPollingStatus.DuplicateRecord;
                ioAddress = EndianPrimitives.ReadUInt32LittleEndian(entry + 4);
                if (!Address(ioAddress))
                    return MadtPollingStatus.InvalidAddress;
                ioId = entry[2];
                gsiBase = EndianPrimitives.ReadUInt32LittleEndian(entry + 8);
                hasIo = true;
            }
            else if (type == 2)
            {
                uint source = entry[3], gsi = EndianPrimitives.ReadUInt32LittleEndian(entry + 4);
                // The last DWORD contains the high GSI half followed by the two flag bytes.
                uint intiFlags = EndianPrimitives.ReadUInt32LittleEndian(entry + 6) >> 16;
                if (entry[2] != 0 || source >= 16 || !InterruptFlags(intiFlags))
                    return MadtPollingStatus.InvalidRecord;
                if ((isaSources & (1U << (int)source)) != 0)
                    return MadtPollingStatus.DuplicateRecord;
                for (uint i = 0; i < 16; i++)
                    if ((isaSources & (1U << (int)i)) != 0 && isaGsi[i] == gsi)
                        return MadtPollingStatus.DuplicateRecord;
                isaSources |= 1U << (int)source;
                isaGsi[source] = gsi;
                isoCount++;
                if (gsi > maximumIsoGsi)
                    maximumIsoGsi = gsi;
            }
            else if (type == 4)
            {
                // Packed UID, two flag bytes and LINT fit exactly in this DWORD.
                uint value = EndianPrimitives.ReadUInt32LittleEndian(entry + 2);
                byte uid = (byte)value;
                uint lint = value >> 24;
                if (lint > 1 || !InterruptFlags((value >> 8) & 0xffff))
                    return MadtPollingStatus.InvalidRecord;
                uint lintBit = 1U << (int)lint;
                if ((wildcardLints & lintBit) != 0)
                    return MadtPollingStatus.DuplicateRecord;
                if (uid == 255)
                {
                    if ((specificLints & lintBit) != 0)
                        return MadtPollingStatus.DuplicateRecord;
                    wildcardLints |= lintBit;
                }
                else
                {
                    uint word = 8 + lint * 4 + (uint)(uid >> 6);
                    ulong bit = 1UL << (uid & 63);
                    if ((seen[word] & bit) != 0)
                        return MadtPollingStatus.DuplicateRecord;
                    seen[word] |= bit;
                    specificLints |= lintBit;
                }

                nmiCount++;
            }
            else
            {
                if (entry[2] != 0 || entry[3] != 0)
                    return MadtPollingStatus.InvalidRecord;
                if (hasOverride)
                    return MadtPollingStatus.DuplicateRecord;
                localAddress = EndianPrimitives.ReadUInt64LittleEndian(entry + 4);
                if (!Address(localAddress))
                    return MadtPollingStatus.InvalidAddress;
                hasOverride = true;
            }

            records++;
            offset += length;
        }

        if (!enabledCpu)
            return MadtPollingStatus.MissingProcessor;
        if (processorApicId != bspApicId)
            return MadtPollingStatus.BspMismatch;
        if (!hasIo)
            return MadtPollingStatus.MissingIoApic;
        if (localAddress == ioAddress)
            return MadtPollingStatus.InvalidAddress;
        for (uint i = 0; i < 16; i++)
            if ((isaSources & (1U << (int)i)) != 0 && isaGsi[i] < gsiBase)
                return MadtPollingStatus.InvalidRecord;
        for (uint i = 0; i < 4; i++)
            if (((seen[8 + i] | seen[12 + i]) & ~seen[i]) != 0)
                return MadtPollingStatus.InvalidRecord;
        topology = new(localAddress, ioAddress, gsiBase, flags, processorApicId, processorUid, ioId, records, isoCount, nmiCount, hasOverride, maximumIsoGsi);
        return MadtPollingStatus.Success;
    }

    private static bool Address(ulong address) => address >= 0x100000 && address <= 0xfffff000 && (address & 4095) == 0;
    private static bool InterruptFlags(uint flags) => (flags & ~15U) == 0 && (flags & 3) != 2 && ((flags >> 2) & 3) != 2;
}
