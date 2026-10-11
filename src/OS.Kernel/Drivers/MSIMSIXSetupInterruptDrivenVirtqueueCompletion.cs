using OS.Std.Binary;

namespace OS.Drivers;

public enum MsixStatus : byte
{
    Uninitialized, Success, InvalidStorage, AbsentDevice, UnsupportedHeader, InvalidChain,
    MissingCapability, DuplicateCapability, TruncatedCapability, InvalidCapability,
    AlreadyEnabled, Overlap, InvalidBar, OutsideBar, InvalidAddress, InvalidAccess,
    ReadFailed, WriteFailed, ReadBackFailed, PendingInterrupt
}

/// <summary>Owned single-BSP wait storage. The native ABI fixes every field offset.
/// IDTR buffers contain the architectural 10 bytes followed by six zero bytes.</summary>
public unsafe struct MsixInterruptContext
{
    public ulong EoiAddress, Count, InterruptedRip, InterruptedFlags, EntryFlags, ExitFlags, Iterations;
    public fixed byte OriginalIdtr[16];
    public fixed byte TemporaryIdtr[16];
    public fixed byte RestoredIdtr[16];
    public ulong SpuriousCount, BeforeEoiIsr, AfterEoiIsr;
}

/// <summary>Fixed Microsoft x64 leaves, separately authenticated by the adapter.
/// Wait ABI: uint (MsixInterruptContext*, uint limit). The caller must prove CPL0,
/// IF=0, exclusive admitted context/stack, two intact owned IDTs, RX code and UC
/// LAPIC EOI/ISR registers. Only this leaf runs with IF=1. It preserves R15, swaps
/// IDTR, bounds PAUSE iterations, disables IF and restores IDTR before returning.
/// The returning handler preserves RAX, captures RIP/RFLAGS and LAPIC ISR around
/// EOI, then IRETQ. Its R15 context is valid only inside the wait region. The
/// spurious gate counts and IRETQs without EOI. Never invoke these on the host.</summary>
public static unsafe class X64MsixInterrupt
{
    public const uint Bytes = 192, WaitOffset = 0, HandlerOffset = 112, SpuriousOffset = 176;
    public const uint InterruptStart = 54, InterruptEnd = 70, MaximumIterations = 1000000;
    public static bool Emit(byte* code, ulong capacity)
    {
        if (!Storage(code, capacity)) return false;
        for (uint i = 0; i < Bytes; i++) code[i] = Byte(i);
        return true;
    }
    public static bool Validate(byte* code, ulong capacity)
    {
        if (!Storage(code, capacity)) return false;
        for (uint i = 0; i < Bytes; i++) if (code[i] != Byte(i)) return false;
        return true;
    }

    public static bool PrepareTemporaryIdt(byte* original, byte* temporary, ulong capacity, ulong originalStubs, ulong code)
    {
        if (!Tables(original, temporary, capacity, originalStubs, code)) return false;
        for (uint i = 0; i < 4096; i++) temporary[i] = original[i];
        HandlerAddress(temporary + BoundedXApicSetup.CompletionVector * 16, code + HandlerOffset);
        HandlerAddress(temporary + BoundedXApicSetup.SpuriousVector * 16, code + SpuriousOffset);
        return ValidateTemporaryIdt(original, temporary, capacity, originalStubs, code);
    }

    public static bool ValidateTemporaryIdt(byte* original, byte* temporary, ulong capacity, ulong originalStubs, ulong code)
    {
        if (!Tables(original, temporary, capacity, originalStubs, code)) return false;
        for (uint i = 0; i < 4096; i++)
        {
            uint vector = i / 16, offset = i & 15;
            ulong target = vector == BoundedXApicSetup.CompletionVector ? code + HandlerOffset :
                vector == BoundedXApicSetup.SpuriousVector ? code + SpuriousOffset : 0;
            byte expected = original[i];
            if (target != 0)
            {
                if (offset < 2) expected = (byte)(target >> ((int)offset * 8));
                else if (offset >= 6 && offset < 12) expected = (byte)(target >> ((int)(offset - 4) * 8));
            }
            if (temporary[i] != expected) return false;
        }
        return true;
    }
    private static bool Tables(byte* original, byte* temporary, ulong capacity, ulong originalStubs, ulong code)
    {
        if (capacity != 4096 || ((ulong)temporary & 15) != 0 || !Range((ulong)temporary, 4096) || !Range(code, Bytes) ||
            !OS.Kernel.Diagnostics.PopulateAllN256IDTVectorsOwnedGatesNever.ValidateReadBack(
                original, 4096, (ulong)original, originalStubs).Succeeded) return false;
        ulong t = (ulong)temporary, o = (ulong)original;
        return !(t < o + 4096 && o < t + 4096) && !(t < code + Bytes && code < t + 4096) &&
            !(t < originalStubs + 4096 && originalStubs < t + 4096) && !(o < code + Bytes && code < o + 4096);
    }
    private static void HandlerAddress(byte* gate, ulong handler)
    {
        gate[0] = (byte)handler; gate[1] = (byte)(handler >> 8);
        for (uint i = 0; i < 6; i++) gate[6 + i] = (byte)(handler >> ((int)(i + 2) * 8));
    }

    // These numeric observations are necessary but do not authenticate mappings,
    // table contents, code or DMA. The adapter must independently prove those.
    public static bool ValidateCompletion(MsixInterruptContext* c, ulong code, ulong originalIdt,
        ulong temporaryIdt, ulong localApic, uint limit, uint returned)
    {
        return ValidWait(c, code, originalIdt, temporaryIdt, localApic, limit) && returned == 1 &&
            c->Count == 1 && c->SpuriousCount == 0 && c->BeforeEoiIsr == 1 && c->AfterEoiIsr == 0 &&
            (c->InterruptedFlags & 0x202) == 0x202 && c->InterruptedRip >= code + InterruptStart &&
            c->InterruptedRip <= code + InterruptEnd;
    }

    // A timeout is recoverable only after the leaf ran its entire bounded wait
    // and restored IF/IDTR. A failed admission or unexpected interrupt is not
    // a timeout receipt. The adapter must mask delivery and audit live state.
    public static bool ValidatePollingRecovery(MsixInterruptContext* c, ulong code, ulong originalIdt,
        ulong temporaryIdt, ulong localApic, uint limit, uint returned)
    {
        return ValidWait(c, code, originalIdt, temporaryIdt, localApic, limit) && returned == 0 &&
            c->Iterations == limit && c->Count == 0 && c->SpuriousCount == 0 &&
            c->InterruptedRip == 0 && c->InterruptedFlags == 0 && c->BeforeEoiIsr == 0 && c->AfterEoiIsr == 0;
    }

    private static bool ValidWait(MsixInterruptContext* c, ulong code, ulong originalIdt,
        ulong temporaryIdt, ulong localApic, uint limit)
    {
        if (c == null || ((ulong)c & 7) != 0 || (ulong)c > ~0UL - (ulong)sizeof(MsixInterruptContext) ||
            !Range(code, Bytes) || !Range(originalIdt, 4096) || !Range(temporaryIdt, 4096) ||
            (originalIdt < temporaryIdt + 4096 && temporaryIdt < originalIdt + 4096) ||
            localApic < 0x100000 || localApic > 0xfffff000 || (localApic & 4095) != 0 ||
            c->EoiAddress != localApic + 0xb0 || limit == 0 || limit > MaximumIterations ||
            c->Iterations == 0 || c->Iterations > limit || (c->EntryFlags & 0x202) != 2 ||
            (c->ExitFlags & 0x202) != 2) return false;
        return Idtr(c->OriginalIdtr, originalIdt) && Idtr(c->RestoredIdtr, originalIdt) && Idtr(c->TemporaryIdtr, temporaryIdt);
    }
    private static bool Idtr(byte* bytes, ulong address)
    {
        if (bytes[0] != 255 || bytes[1] != 15 || EndianPrimitives.ReadUInt64LittleEndian(bytes + 2) != address) return false;
        for (uint i = 10; i < 16; i++) if (bytes[i] != 0) return false;
        return true;
    }
    private static bool Range(ulong start, ulong bytes) => start >= 4096 && start <= 0x0000800000000000UL - bytes;
    private static bool Storage(byte* p, ulong capacity) => p != null && capacity >= Bytes && (ulong)p <= ~0UL - Bytes;
    private static byte Byte(uint i)
    {
        ulong word = (i / 8) switch
        {
            // Wait: admission; preserve R15; SIDT/LIDT; STI; bounded loop;
            // CLI; restore/read IDTR; capture IF; restore R15; return count.
            0 => 0x8948589cfa1e0ff3UL, 1 => 0x7500000200a92041UL,
            2 => 0x40fa814d74d28551UL, 3 => 0x7983484577000f42UL,
            4 => 0x894957413e750008UL, 5 => 0x0f41384f010f41cfUL,
            6 => 0x90f3fbc031485f01UL, 7 => 0x00087f8349c0ff48UL,
            8 => 0x49faf072d0390475UL, 9 => 0x385f010f41304789UL,
            10 => 0x49589c584f010f41UL, 11 => 0x4108478b41284789UL,
            12 => 0x000000c3c031c35fUL, 13 => 0,
            // Returning vector 0xe0 gate. With RAX pushed, RIP/RFLAGS are at
            // RSP+8/+24 in the 64-bit interrupt frame. ISR7 is EOI+0xc0.
            14 => 0x448b4850fa1e0ff3UL, 15 => 0x8b48104789490824UL,
            16 => 0x4918478949182444UL, 17 => 0x000000c0808b078bUL,
            18 => 0xc7078b4970478949UL, 19 => 0xc0808b0000000000UL,
            20 => 0x4978478949000000UL, 21 => 0x0000cf48580847ffUL,
            // Spurious vector never writes EOI.
            22 => 0x6847ff49fa1e0ff3UL, 23 => 0x000000000000cf48UL,
            _ => 0
        };
        return (byte)(word >> ((int)(i & 7) * 8));
    }
}

public readonly struct MsixCapability
{
    public readonly byte CapabilityOffset, TableBar, PbaBar;
    public readonly ushort Control, TableSize;
    public readonly uint TableOffset, PbaOffset;
    internal MsixCapability(byte offset, ushort control, uint table, uint pba)
    {
        CapabilityOffset = offset; Control = control; TableSize = (ushort)((control & 0x7ff) + 1);
        TableBar = (byte)(table & 7); TableOffset = table & ~7U;
        PbaBar = (byte)(pba & 7); PbaOffset = pba & ~7U;
    }
    public bool IsValid => CapabilityOffset >= 0x40 && CapabilityOffset <= 0xf4 && (CapabilityOffset & 3) == 0 &&
        (Control & 0xb800) == 0 && TableSize == (Control & 0x7ff) + 1 && TableBar < 6 && PbaBar < 6 &&
        (TableOffset & 7) == 0 && (PbaOffset & 7) == 0;
}

public readonly struct MsixRegion
{
    public readonly ulong PhysicalStart, PageStart;
    public readonly uint Bytes, PageCount;
    internal MsixRegion(ulong start, uint bytes)
    {
        PhysicalStart = start; Bytes = bytes; PageStart = start & ~4095UL;
        PageCount = (uint)(((start & 4095) + bytes + 4095) / 4096);
    }
    public bool IsValid => Bytes != 0 && Bytes <= 32768 && PhysicalStart >= 0x100000 &&
        (PhysicalStart & 7) == 0 && PhysicalStart <= 0x0000800000000000UL - Bytes &&
        PageStart == (PhysicalStart & ~4095UL) && PageCount == ((PhysicalStart & 4095) + Bytes + 4095) / 4096;
}

/// <summary>The adapter authenticates the config DWORD's 16-bit control access,
/// common configuration and complete identity UC/RW/NX table/PBA mappings. Each
/// callback performs one ordered width-correct access; 1 means success. IF remains
/// clear, the device is reset/negotiated exclusively and no other queue is active.</summary>
public unsafe struct MsixRegisterAccess
{
    public void* Context;
    public delegate* unmanaged<void*, ulong, ushort*, byte> Read16;
    public delegate* unmanaged<void*, ulong, uint, byte> Write16;
    public delegate* unmanaged<void*, ulong, uint*, byte> Read32;
    public delegate* unmanaged<void*, ulong, uint, byte> Write32;
}

public struct MsixProgrammingResult
{
    public MsixStatus Status;
    public uint MaskedEntries, PendingWords, MessageAddress, MessageData;
    public ushort Control, ConfigVector;
}

/// <summary>Programs only MSI-X entry zero for vector 0xe0, physical BSP delivery.
/// All other entries stay masked; configuration-change interrupts use NO_VECTOR.
/// Prepare leaves the function mask set, so the queue can be published before
/// the bounded wait explicitly unmasks it. Failure does not imply rollback; the
/// adapter must keep IF clear, mask/disable and reset or retain all backing.</summary>
public static unsafe class BoundedMsixProgramming
{
    public static MsixProgrammingResult Prepare(MsixCapability cap, MsixRegions regions, ulong configPage,
        VirtioMappedRegion common, byte bspId, MsixRegisterAccess a)
    {
        MsixProgrammingResult r = default; r.Status = MsixStatus.InvalidAccess;
        if (!ControlAccess(cap, configPage, a) || a.Read32 == null || a.Write32 == null ||
            !common.IsValid || common.Bytes < 56 || (common.VirtualStart & 3) != 0 || bspId == 255) return r;
        r.Status = MsixStatus.InvalidAddress;
        if (!regions.Table.IsValid || !regions.Pba.IsValid || regions.Table.Bytes != (uint)cap.TableSize * 16 ||
            regions.Pba.Bytes != ((uint)cap.TableSize + 63) / 64 * 8 ||
            (regions.Table.PhysicalStart < regions.Pba.PhysicalStart + regions.Pba.Bytes &&
             regions.Pba.PhysicalStart < regions.Table.PhysicalStart + regions.Table.Bytes)) return r;
        r.Status = Program(cap, regions, configPage, common, bspId, a, ref r);
        return r;
    }

    private static MsixStatus Program(MsixCapability cap, MsixRegions regions, ulong configPage,
        VirtioMappedRegion common, byte bspId, MsixRegisterAccess a, ref MsixProgrammingResult r)
    {
        ulong control = configPage + cap.CapabilityOffset + 2;
        ushort observed = 0;
        if (a.Read16(a.Context, control, &observed) != 1) return MsixStatus.ReadFailed;
        if (observed != cap.Control) return MsixStatus.ReadBackFailed;
        r.Control = (ushort)((cap.Control & 0x7ff) | 0xc000);
        var status = Write16(a, control, r.Control);
        if (status != MsixStatus.Success) return status;
        for (uint i = 0; i < cap.TableSize; i++)
        {
            status = Write32(a, regions.Table.PhysicalStart + i * 16 + 12, 1);
            if (status != MsixStatus.Success) return status;
            r.MaskedEntries++;
        }
        for (uint i = 0; i < regions.Pba.Bytes; i += 4)
        {
            uint pending = 0;
            if (a.Read32(a.Context, regions.Pba.PhysicalStart + i, &pending) != 1) return MsixStatus.ReadFailed;
            if (pending != 0) return MsixStatus.PendingInterrupt;
            r.PendingWords++;
        }
        r.MessageAddress = 0xfee00000U | ((uint)bspId << 12);
        r.MessageData = BoundedXApicSetup.CompletionVector;
        status = Write32(a, regions.Table.PhysicalStart, r.MessageAddress);
        if (status != MsixStatus.Success) return status;
        status = Write32(a, regions.Table.PhysicalStart + 4, 0);
        if (status != MsixStatus.Success) return status;
        status = Write32(a, regions.Table.PhysicalStart + 8, r.MessageData);
        if (status != MsixStatus.Success) return status;
        status = Write16(a, common.VirtualStart + 16, 0xffff);
        if (status != MsixStatus.Success) return status;
        r.ConfigVector = 0xffff;
        // Entry zero becomes eligible, but the function mask still blocks delivery.
        return Write32(a, regions.Table.PhysicalStart + 12, 0);
    }

    public static MsixStatus SetFunctionMask(MsixCapability cap, ulong configPage, bool masked, MsixRegisterAccess a)
    {
        if (!ControlAccess(cap, configPage, a)) return MsixStatus.InvalidAccess;
        ulong control = configPage + cap.CapabilityOffset + 2; ushort observed = 0;
        if (a.Read16(a.Context, control, &observed) != 1) return MsixStatus.ReadFailed;
        ushort enabled = (ushort)((cap.Control & 0x7ff) | 0x8000);
        if ((observed & ~0x4000) != enabled) return MsixStatus.ReadBackFailed;
        return Write16(a, control, (ushort)(enabled | (masked ? 0x4000 : 0)));
    }

    public static MsixStatus Disable(MsixCapability cap, ulong configPage, MsixRegisterAccess a)
    {
        if (!ControlAccess(cap, configPage, a)) return MsixStatus.InvalidAccess;
        ulong control = configPage + cap.CapabilityOffset + 2; ushort observed = 0;
        if (a.Read16(a.Context, control, &observed) != 1) return MsixStatus.ReadFailed;
        if ((observed & 0x3fff) != (cap.Control & 0x7ff)) return MsixStatus.ReadBackFailed;
        var status = Write16(a, control, (ushort)(observed | 0x4000));
        return status == MsixStatus.Success ? Write16(a, control, cap.Control) : status;
    }

    private static bool ControlAccess(MsixCapability cap, ulong page, MsixRegisterAccess a) =>
        cap.IsValid && page >= 0x100000 && page <= 0x0000800000000000UL - 4096 && (page & 4095) == 0 &&
        a.Read16 != null && a.Write16 != null;
    private static MsixStatus Write16(MsixRegisterAccess a, ulong address, ushort value)
    {
        if (a.Write16(a.Context, address, value) != 1) return MsixStatus.WriteFailed;
        ushort observed = 0;
        if (a.Read16(a.Context, address, &observed) != 1) return MsixStatus.ReadFailed;
        return observed == value ? MsixStatus.Success : MsixStatus.ReadBackFailed;
    }
    private static MsixStatus Write32(MsixRegisterAccess a, ulong address, uint value)
    {
        if (a.Write32(a.Context, address, value) != 1) return MsixStatus.WriteFailed;
        uint observed = 0;
        if (a.Read32(a.Context, address, &observed) != 1) return MsixStatus.ReadFailed;
        return observed == value ? MsixStatus.Success : MsixStatus.ReadBackFailed;
    }
}

public readonly struct MsixRegions
{
    public readonly MsixRegion Table, Pba;
    internal MsixRegions(MsixRegion table, MsixRegion pba) { Table = table; Pba = pba; }
}

/// <summary>
/// Numeric declarations from stable caller-owned snapshots, never MMIO access or
/// interrupt setup. The adapter authenticates device/BAR identity, lifetime and
/// UC/RW/NX mappings, including disjointness from RAM and other device resources.
/// Table and PBA bytes must not overlap; their mapping pages may be shared.
/// Physical ranges are restricted to the lower 48-bit canonical address domain.
/// </summary>
public static unsafe class MSIMSIXSetupInterruptDrivenVirtqueueCompletion
{
    public static MsixStatus Discover(byte* config, uint bytes, out MsixCapability capability)
    {
        capability = default;
        if (bytes != 256) return MsixStatus.InvalidStorage;
        var walk = PciCapabilityWalk.Validate(config, bytes);
        if (walk.Status == PciCapabilityWalkStatus.InvalidStorage) return MsixStatus.InvalidStorage;
        if (walk.Status == PciCapabilityWalkStatus.AbsentDevice) return MsixStatus.AbsentDevice;
        if (walk.Status == PciCapabilityWalkStatus.UnsupportedHeader) return MsixStatus.UnsupportedHeader;
        if (!walk.Succeeded) return MsixStatus.InvalidChain;
        if ((config[0x0e] & 0x7f) != 0) return MsixStatus.UnsupportedHeader;
        MsixCapability pending = default;
        uint node = config[0x34]; ulong occupied = 0;
        for (uint i = 0; i < walk.Count; i++)
        {
            bool msix = config[node] == 0x11;
            if (msix && node > 244) return MsixStatus.TruncatedCapability;
            // Unknown capability payloads remain opaque; protect every linked header
            // against the entire known 12-byte MSI-X record, in either list order.
            ulong extent = (msix ? 7UL : 1UL) << (int)(node >> 2);
            if ((occupied & extent) != 0) return MsixStatus.Overlap;
            occupied |= extent;
            if (msix)
            {
                if (pending.IsValid) return MsixStatus.DuplicateCapability;
                ushort control = (ushort)(EndianPrimitives.ReadUInt32LittleEndian(config + node) >> 16);
                if ((control & 0x3800) != 0) return MsixStatus.InvalidCapability;
                if ((control & 0x8000) != 0) return MsixStatus.AlreadyEnabled;
                pending = new((byte)node, control, EndianPrimitives.ReadUInt32LittleEndian(config + node + 4),
                    EndianPrimitives.ReadUInt32LittleEndian(config + node + 8));
                if (!pending.IsValid) return MsixStatus.InvalidCapability;
            }
            node = config[node + 1];
        }
        if (!pending.IsValid) return MsixStatus.MissingCapability;
        capability = pending;
        return MsixStatus.Success;
    }

    public static MsixStatus Plan(MsixCapability capability, PciSizedBar* sixBars, uint count,
        out MsixRegions regions)
    {
        regions = default;
        if (sixBars == null || count != 6 || ((ulong)sixBars & 7) != 0 ||
            (ulong)sixBars > ~0UL - 6UL * (ulong)sizeof(PciSizedBar)) return MsixStatus.InvalidStorage;
        if (!capability.IsValid) return MsixStatus.InvalidCapability;
        uint tableBytes = (uint)capability.TableSize * 16;
        uint pbaBytes = ((uint)capability.TableSize + 63) / 64 * 8;
        var status = Region(sixBars, capability.TableBar, capability.TableOffset, tableBytes, out var table);
        if (status != MsixStatus.Success) return status;
        status = Region(sixBars, capability.PbaBar, capability.PbaOffset, pbaBytes, out var pba);
        if (status != MsixStatus.Success) return status;
        if (table.PhysicalStart < pba.PhysicalStart + pba.Bytes && pba.PhysicalStart < table.PhysicalStart + table.Bytes)
            return MsixStatus.Overlap;
        var tableBar = sixBars[capability.TableBar]; var pbaBar = sixBars[capability.PbaBar];
        if (capability.TableBar != capability.PbaBar && tableBar.Base < pbaBar.Base + pbaBar.Bytes &&
            pbaBar.Base < tableBar.Base + tableBar.Bytes) return MsixStatus.Overlap;
        regions = new(table, pba);
        return MsixStatus.Success;
    }

    private static MsixStatus Region(PciSizedBar* bars, byte index, uint offset, uint bytes, out MsixRegion region)
    {
        region = default; var bar = bars[index];
        if ((bar.Kind != PciSizedBarKind.Memory32 && bar.Kind != PciSizedBarKind.Memory64) ||
            (index > 0 && bars[index - 1].Kind == PciSizedBarKind.Memory64) ||
            (bar.Kind == PciSizedBarKind.Memory64 && (index == 5 || bars[index + 1].Kind != PciSizedBarKind.UpperHalf)) ||
            !bar.IsAssigned || bar.Bytes < 16 || (bar.Bytes & (bar.Bytes - 1)) != 0 ||
            (bar.Base & (bar.Bytes - 1)) != 0 || bar.Base > ~0UL - bar.Bytes ||
            (bar.Kind == PciSizedBarKind.Memory32 && bar.Base + bar.Bytes > 0x100000000UL))
            return MsixStatus.InvalidBar;
        if (bar.Base < 0x100000 || bar.Base + bar.Bytes > 0x0000800000000000UL) return MsixStatus.InvalidAddress;
        if ((ulong)offset + bytes > bar.Bytes) return MsixStatus.OutsideBar;
        region = new(bar.Base + offset, bytes);
        return MsixStatus.Success;
    }
}
