namespace OS.Kernel.Platform;
/// <summary>
/// One-use, single-CPU polled clock. No interrupts, scheduler, wall time or CPU migration.
/// Initialize only after firmware exit. No reset or source switch is allowed after publication.
/// Sample within half the counter range; the owner must prevent external counter resets.
/// </summary>

public static unsafe class OwnedMonotonicClock
{
    public const uint MaximumMotionSamples = 4096;
    private const uint Configuration = 0x10, Counter = 0xf0;
    // ACPI is the positive device authority. EFI may omit MMIO from its map;
    // an overlap is accepted only as one complete Reserved/MMIO descriptor.
    // Caller supplies the original owned map and separately excludes all later
    // owned allocations before constructing the retained mapping.
    public static bool AdmitDescription(HpetMmioDescription description, OS.Boot.FirmwareMemoryMapRecord map)
    {
        ulong address = description.Address;
        if (address < 0x100000 || address > 0xfffff000 || (address & 4095) != 0 || (description.HardwareId & 255) == 0 || !map.HasOwnedCopy() || map.DescriptorVersion != 1 || (map.BufferAddress & 7) != 0 || map.DescriptorSize < 40 || map.DescriptorSize > 256 || (map.DescriptorSize & 7) != 0 || map.DescriptorCount == 0 || map.DescriptorCount > 4096 || map.MapSize != map.DescriptorSize * map.DescriptorCount || map.MapSize > map.BufferSize || map.BufferAddress > ~0UL - map.BufferSize || (address < map.BufferAddress + map.BufferSize && map.BufferAddress < address + 4096))
            return false;
        uint covering = 0;
        for (ulong i = 0; i < map.DescriptorCount; i++)
        {
            byte* descriptor = (byte*)(map.BufferAddress + i * map.DescriptorSize);
            uint type = OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(descriptor);
            ulong start = OS.Std.Binary.EndianPrimitives.ReadUInt64LittleEndian(descriptor + 8);
            ulong pages = OS.Std.Binary.EndianPrimitives.ReadUInt64LittleEndian(descriptor + 24);
            if ((start & 4095) != 0 || pages == 0 || pages > (~0UL - start) / 4096)
                return false;
            ulong end = start + pages * 4096;
            if (address >= end || start >= address + 4096)
                continue;
            if ((type != 0 && type != 11) || start > address || end < address + 4096 || ++covering != 1)
                return false;
        }

        return true;
    }

    // HPET 1.0a, table 3.2.4. Deliberately bounded to one aligned low physical page.
    public static bool TryReadDescription(byte* table, ulong bytes, out HpetMmioDescription description)
    {
        description = default;
        if (table == null || bytes != 56 || (ulong)table > ~0UL - bytes || OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(table) != 0x54455048 || OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(table + 4) != bytes || table[8] != 1)
            return false;
        uint checksum = 0;
        for (uint i = 0; i < 56; i++)
            checksum += table[i];
        uint id = OS.Std.Binary.EndianPrimitives.ReadUInt32LittleEndian(table + 36);
        ulong address = OS.Std.Binary.EndianPrimitives.ReadUInt64LittleEndian(table + 44);
        if ((checksum & 255) != 0 || (id & 255) == 0 || table[40] != 0 || (table[41] != 0 && table[41] != 64) || table[42] != 0 || table[43] != 0 || (table[55] & 15) > 2 || address < 0x100000 || address > 0xfffff000 || (address & 4095) != 0)
            return false;
        description = new(address, id);
        return true;
    }

    public static bool Start(ref OwnedMonotonicClockState state, OwnedHpetAccess hpet, OwnedTscAccess tsc)
    {
        if (state.status != OwnedClockStatus.Uninitialized)
            return false;
        state.status = OwnedClockStatus.InvalidAccess;
        state.hpet = hpet;
        state.tsc = tsc;
        state.hpetStatus = StartHpet(ref state);
        if (state.hpetStatus == OwnedClockStatus.Ready)
            return true;
        state.status = OwnedClockStatus.TscUnavailable;
        if (!TryTscFrequency(tsc, out ulong frequency) || tsc.Read == null)
            return false;
        ulong previous = tsc.Read();
        for (uint i = 0; i < MaximumMotionSamples; i++)
        {
            ulong current = tsc.Read();
            var delta = MonotonicHpetTscClockMath.Accumulate(previous, current, 64, 0);
            if (!delta.Succeeded)
            {
                state.status = OwnedClockStatus.InvalidCounter;
                return false;
            }

            if (delta.DeltaTicks == 0)
                continue;
            state.source = OwnedClockSource.Tsc;
            state.frequency = frequency;
            state.width = 64;
            state.period = 0;
            state.previous = current;
            state.status = OwnedClockStatus.Ready;
            return true;
        }

        state.status = OwnedClockStatus.Stalled;
        return false;
    }

    public static bool TryTscFrequency(OwnedTscAccess tsc, out ulong frequency)
    {
        frequency = 0;
        // Intel SDM CPUID.15H supplies a ratio to the independently enumerated crystal.
        // Do not substitute CPU nominal/base MHz or guess an absent crystal frequency.
        if (!tsc.IntelVendor || tsc.MaximumBasicLeaf < 0x15 || tsc.MaximumExtendedLeaf < 0x80000007 || (tsc.Leaf1Edx & 16) == 0 || (tsc.Leaf80000007Edx & 256) == 0 || tsc.Denominator == 0 || tsc.Numerator == 0 || tsc.CrystalHz == 0)
            return false;
        ulong product = (ulong)tsc.CrystalHz * tsc.Numerator;
        ulong value = product / tsc.Denominator;
        // This bounded fallback requires an exact integral frequency in a reviewed range.
        if (product % tsc.Denominator != 0 || value < 1000000 || value > 10000000000)
            return false;
        frequency = value;
        return true;
    }

    public static bool Read(ref OwnedMonotonicClockState state)
    {
        if (state.status != OwnedClockStatus.Ready)
            return false;
        ulong current;
        if (state.source == OwnedClockSource.Hpet)
        {
            if ((state.hpet.Read(state.hpet.Context, Configuration) & 3) != 1 || state.hpet.Read(state.hpet.Context, 0) != state.capabilities)
            {
                state.status = OwnedClockStatus.ConfigurationFailed;
                return false;
            }

            current = state.hpet.Read(state.hpet.Context, Counter);
        }
        else
            current = state.tsc.Read();
        var delta = MonotonicHpetTscClockMath.Accumulate(state.previous, current, state.width, state.ticks);
        if (!delta.Succeeded)
        {
            state.status = OwnedClockStatus.InvalidCounter;
            return false;
        }

        state.previous = current;
        state.ticks = delta.AccumulatedTicks;
        return true;
    }

    private static OwnedClockStatus StartHpet(ref OwnedMonotonicClockState state)
    {
        OwnedHpetAccess io = state.hpet;
        if (io.Context == null || io.Read == null || io.Write == null)
            return OwnedClockStatus.InvalidAccess;
        ulong capabilities = io.Read(io.Context, 0);
        var decoded = MonotonicHpetTscClockMath.DecodeHpetCapabilities(capabilities);
        if (!decoded.Succeeded || (uint)capabilities != io.HardwareId || (capabilities & 255) == 0)
            return OwnedClockStatus.InvalidCapabilities;
        // Preserve reserved/OEM bits. Disable every timer's interrupt and FSB delivery,
        // then leave legacy replacement disabled. This device has one exclusive owner.
        uint timers = (uint)((capabilities >> 8) & 31) + 1;
        for (uint i = 0; i < timers; i++)
        {
            uint offset = 0x100 + i * 0x20;
            ulong timer = io.Read(io.Context, offset);
            io.Write(io.Context, offset, timer & ~0x4004UL);
            if ((io.Read(io.Context, offset) & 0x4004) != 0)
                return OwnedClockStatus.ConfigurationFailed;
        }

        ulong config = io.Read(io.Context, Configuration) & ~2UL;
        io.Write(io.Context, Configuration, config);
        if (io.Read(io.Context, Configuration) != config)
            return OwnedClockStatus.ConfigurationFailed;
        if ((config & 1) == 0 || !Moving(io, decoded.CounterWidth, out _))
        {
            state.recoveries = 1;
            io.Write(io.Context, Configuration, config & ~1UL);
            if (io.Read(io.Context, Configuration) != (config & ~1UL))
                return OwnedClockStatus.ConfigurationFailed;
            io.Write(io.Context, Counter, 0);
            if (io.Read(io.Context, Counter) != 0)
                return OwnedClockStatus.ConfigurationFailed;
            io.Write(io.Context, Configuration, config | 1);
        }

        if (io.Read(io.Context, Configuration) != (config | 1) || io.Read(io.Context, 0) != capabilities)
            return OwnedClockStatus.ConfigurationFailed;
        if (!Moving(io, decoded.CounterWidth, out ulong current))
            return OwnedClockStatus.Stalled;
        state.source = OwnedClockSource.Hpet;
        state.capabilities = capabilities;
        state.frequency = decoded.FrequencyHz;
        state.period = decoded.PeriodFemtoseconds;
        state.width = decoded.CounterWidth;
        state.previous = current;
        state.status = OwnedClockStatus.Ready;
        return OwnedClockStatus.Ready;
    }

    private static bool Moving(OwnedHpetAccess io, uint width, out ulong current)
    {
        ulong previous = io.Read(io.Context, Counter);
        current = previous;
        for (uint i = 0; i < MaximumMotionSamples; i++)
        {
            current = io.Read(io.Context, Counter);
            var delta = MonotonicHpetTscClockMath.Accumulate(previous, current, width, 0);
            if (!delta.Succeeded)
                return false;
            if (delta.DeltaTicks != 0)
                return true;
        }

        return false;
    }
}
