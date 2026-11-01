namespace OS.Kernel.Compatibility.LinuxX64;
/// <summary>
/// Copies admitted PT_LOAD file bytes into an exclusive caller-owned rounded
/// image span; all BSS, inter-segment gaps and page padding are zero. The caller
/// owns allocation, lifetime, physical reservations and later protected mapping.
/// This operation publishes no root, entry or TLS thread state.
/// </summary>

public static unsafe class LinuxElfMapping
{
    public static LinuxElfMaterializeStatus Materialize(byte* elf, uint sourceBytes, uint memoryLimit, uint stackBytes, byte* backing, uint backingBytes, out LinuxElfLoadPlan plan)
    {
        plan = default;
        if (LinuxElfLoader.Validate(elf, sourceBytes, memoryLimit, stackBytes, out var admitted) != LinuxElfLoadStatus.Ready)
            return LinuxElfMaterializeStatus.InvalidElf;
        if (backing == null || ((ulong)backing & 4095) != 0 || backingBytes != admitted.ImageBytes || (ulong)backing > ~0UL - backingBytes)
            return LinuxElfMaterializeStatus.InvalidStorage;
        if ((ulong)elf < (ulong)backing + backingBytes && (ulong)backing < (ulong)elf + sourceBytes)
            return LinuxElfMaterializeStatus.OverlappingStorage;
        // Source/output-plan storage is caller-owned, nonaliasing and immutable
        // for this single-CPU operation. No writes occur before admission above.
        Clear(backing, backingBytes);
        for (uint load = 0; load < admitted.LoadCount; load++)
        {
            uint row = load * 5;
            ulong destination = admitted.Loads[row] - admitted.ImageStart;
            ulong file = admitted.Loads[row + 1], bytes = admitted.Loads[row + 2];
            for (ulong i = 0; i < bytes; i++)
                backing[destination + i] = elf[file + i];
        }

        if (!ValidateReadBack(elf, sourceBytes, memoryLimit, stackBytes, admitted, backing, backingBytes))
        {
            Clear(backing, backingBytes);
            return LinuxElfMaterializeStatus.ReadBackFailed;
        }

        plan = admitted;
        return LinuxElfMaterializeStatus.Ready;
    }

    // Re-admit the geometry before trusting a mutable POD plan. Every byte of
    // the rounded span is checked, including holes that will remain unmapped.
    public static bool ValidateReadBack(byte* elf, uint sourceBytes, uint memoryLimit, uint stackBytes, LinuxElfLoadPlan plan, byte* backing, uint backingBytes)
    {
        if (!plan.Succeeded || backing == null || backingBytes != plan.ImageBytes || (ulong)backing > ~0UL - backingBytes || LinuxElfLoader.Validate(elf, sourceBytes, memoryLimit, stackBytes, out var admitted) != LinuxElfLoadStatus.Ready || !SamePlan(plan, admitted))
            return false;
        ulong cursor = 0;
        for (uint index = 0; index < plan.LoadCount; index++)
        {
            uint next = plan.LoadCount;
            ulong lowest = plan.ImageBytes;
            for (uint load = 0; load < plan.LoadCount; load++)
            {
                ulong start = plan.Loads[load * 5] - plan.ImageStart;
                if (start >= cursor && start < lowest)
                {
                    lowest = start;
                    next = load;
                }
            }

            if (next == plan.LoadCount)
                return false;
            for (; cursor < lowest; cursor++)
                if (backing[cursor] != 0)
                    return false;
            uint row = next * 5;
            ulong file = plan.Loads[row + 1], bytes = plan.Loads[row + 2], memory = plan.Loads[row + 3];
            for (ulong i = 0; i < bytes; i++)
                if (backing[cursor + i] != elf[file + i])
                    return false;
            cursor += bytes;
            ulong end = lowest + memory;
            for (; cursor < end; cursor++)
                if (backing[cursor] != 0)
                    return false;
        }

        for (; cursor < plan.ImageBytes; cursor++)
            if (backing[cursor] != 0)
                return false;
        return true;
    }

    private static bool SamePlan(LinuxElfLoadPlan a, LinuxElfLoadPlan b)
    {
        if (a.Status != b.Status || a.SourceBytes != b.SourceBytes || a.ProgramHeaderCount != b.ProgramHeaderCount || a.LoadCount != b.LoadCount || a.ImageStart != b.ImageStart || a.ImageBytes != b.ImageBytes || a.Entry != b.Entry || a.ProgramHeaders != b.ProgramHeaders || a.TlsAddress != b.TlsAddress || a.TlsFileBytes != b.TlsFileBytes || a.TlsMemoryBytes != b.TlsMemoryBytes || a.TlsAlignment != b.TlsAlignment)
            return false;
        for (uint i = 0; i < 80; i++)
            if (a.Loads[i] != b.Loads[i])
                return false;
        return true;
    }

    /// <summary>
    /// Verify genuine fixed-address load mappings after the owner has adopted
    /// and audited its user root. Only mapped pages are read: inter-load holes
    /// remain absent. Caller proves these virtual addresses are readable here.
    /// </summary>

    public static bool ValidateMappedLoads(byte* elf, uint sourceBytes, uint memoryLimit, uint stackBytes, LinuxElfLoadPlan plan)
    {
        if (LinuxElfLoader.Validate(elf, sourceBytes, memoryLimit, stackBytes, out var admitted) != LinuxElfLoadStatus.Ready || !SamePlan(plan, admitted))
            return false;
        for (uint load = 0; load < plan.LoadCount; load++)
        {
            uint row = load * 5;
            ulong address = plan.Loads[row], bytes = plan.Loads[row + 2], memory = plan.Loads[row + 3], file = plan.Loads[row + 1];
            byte* mapped = (byte*)address;
            ulong pageStart = address & ~4095UL, pageEnd = (address + memory + 4095) & ~4095UL;
            for (ulong p = pageStart; p < address; p++)
                if (*(byte*)p != 0)
                    return false;
            for (ulong i = 0; i < bytes; i++)
                if (mapped[i] != elf[file + i])
                    return false;
            for (ulong p = address + bytes; p < pageEnd; p++)
                if (*(byte*)p != 0)
                    return false;
        }

        return true;
    }

    public static bool Clear(byte* backing, uint bytes)
    {
        if (backing == null || bytes == 0 || bytes > LinuxElfLoader.MaximumImageBytes || (ulong)backing > ~0UL - bytes)
            return false;
        for (uint i = 0; i < bytes; i++)
            backing[i] = 0;
        return true;
    }
}
