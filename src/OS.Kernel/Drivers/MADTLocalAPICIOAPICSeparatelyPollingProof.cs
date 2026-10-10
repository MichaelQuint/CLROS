using OS.Std.Binary;

namespace OS.Drivers;

public enum MadtPollingStatus : byte
{
    Uninitialized, Success, InvalidStorage, InvalidHeader, ChecksumFailed, InvalidRecord,
    UnsupportedTopology, DuplicateRecord, InvalidAddress, MissingProcessor, MissingIoApic,
    BspMismatch, TooManyRecords
}

public readonly struct MadtPollingTopology
{
    public readonly ulong LocalApicAddress, IoApicAddress;
    public readonly uint IoApicGsiBase, HeaderFlags, MaximumIsoGsi;
    public readonly byte BspApicId, ProcessorUid, IoApicId;
    public readonly ushort RecordCount, IsoCount, NmiCount;
    public readonly bool HasLocalApicOverride;
    internal MadtPollingTopology(ulong local, ulong io, uint gsiBase, uint flags, byte bsp, byte uid,
        byte ioId, ushort records, ushort isos, ushort nmis, bool addressOverride, uint maximumIsoGsi)
    {
        LocalApicAddress = local; IoApicAddress = io; IoApicGsiBase = gsiBase; HeaderFlags = flags;
        BspApicId = bsp; ProcessorUid = uid; IoApicId = ioId;
        RecordCount = records; IsoCount = isos; NmiCount = nmis; HasLocalApicOverride = addressOverride;
        MaximumIsoGsi = maximumIsoGsi;
    }
}

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

    public static MadtPollingStatus Parse(byte* retained, ulong bytes, uint bspApicId,
        out MadtPollingTopology topology)
    {
        topology = default;
        if (retained == null || bytes < 44 || bytes > MaximumBytes || (ulong)retained > ~0UL - bytes)
            return MadtPollingStatus.InvalidStorage;
        if (EndianPrimitives.ReadUInt32LittleEndian(retained) != 0x43495041 ||
            EndianPrimitives.ReadUInt32LittleEndian(retained + 4) != bytes || retained[8] == 0 || retained[8] > 6)
            return MadtPollingStatus.InvalidHeader;
        uint sum = 0;
        for (uint i = 0; i < bytes; i++) sum += retained[i];
        if ((sum & 255) != 0) return MadtPollingStatus.ChecksumFailed;
        uint flags = EndianPrimitives.ReadUInt32LittleEndian(retained + 40);
        if ((flags & ~1U) != 0) return MadtPollingStatus.InvalidHeader;
        if (bspApicId >= 255) return MadtPollingStatus.UnsupportedTopology;
        ulong localAddress = EndianPrimitives.ReadUInt32LittleEndian(retained + 36), ioAddress = 0;
        if (!Address(localAddress)) return MadtPollingStatus.InvalidAddress;
        // Four words each for processor UIDs, APIC IDs, LINT0 UIDs and LINT1 UIDs.
        ulong* seen = stackalloc ulong[16];
        uint* isaGsi = stackalloc uint[16];
        for (uint i = 0; i < 16; i++) { seen[i] = 0; isaGsi[i] = 0; }
        uint isaSources = 0, wildcardLints = 0, specificLints = 0, gsiBase = 0, maximumIsoGsi = 0;
        byte processorUid = 0, processorApicId = 0, ioId = 0;
        ushort records = 0, isoCount = 0, nmiCount = 0;
        bool enabledCpu = false, hasIo = false, hasOverride = false;
        uint offset = 44;
        while (offset < bytes)
        {
            if (records == MaximumRecords) return MadtPollingStatus.TooManyRecords;
            if (bytes - offset < 2) return MadtPollingStatus.InvalidRecord;
            byte* entry = retained + offset;
            uint length = entry[1];
            if (length < 2 || length > bytes - offset) return MadtPollingStatus.InvalidRecord;
            byte type = entry[0];
            uint expected = type == 0 ? 8U : type == 1 || type == 5 ? 12U : type == 2 ? 10U : type == 4 ? 6U : 0;
            if (expected == 0) return MadtPollingStatus.UnsupportedTopology;
            if (length != expected) return MadtPollingStatus.InvalidRecord;
            if (type == 0)
            {
                byte uid = entry[2], id = entry[3];
                uint cpuFlags = EndianPrimitives.ReadUInt32LittleEndian(entry + 4);
                if ((cpuFlags & ~3U) != 0) return MadtPollingStatus.InvalidRecord;
                ulong uidBit = 1UL << (uid & 63), idBit = 1UL << (id & 63);
                if ((seen[uid >> 6] & uidBit) != 0 || (seen[4 + (id >> 6)] & idBit) != 0)
                    return MadtPollingStatus.DuplicateRecord;
                seen[uid >> 6] |= uidBit; seen[4 + (id >> 6)] |= idBit;
                if ((cpuFlags & 1) != 0)
                {
                    if (enabledCpu) return MadtPollingStatus.UnsupportedTopology;
                    enabledCpu = true; processorUid = uid; processorApicId = id;
                }
            }
            else if (type == 1)
            {
                if (entry[3] != 0) return MadtPollingStatus.InvalidRecord;
                if (hasIo) return MadtPollingStatus.DuplicateRecord;
                ioAddress = EndianPrimitives.ReadUInt32LittleEndian(entry + 4);
                if (!Address(ioAddress)) return MadtPollingStatus.InvalidAddress;
                ioId = entry[2]; gsiBase = EndianPrimitives.ReadUInt32LittleEndian(entry + 8); hasIo = true;
            }
            else if (type == 2)
            {
                uint source = entry[3], gsi = EndianPrimitives.ReadUInt32LittleEndian(entry + 4);
                // The last DWORD contains the high GSI half followed by the two flag bytes.
                uint intiFlags = EndianPrimitives.ReadUInt32LittleEndian(entry + 6) >> 16;
                if (entry[2] != 0 || source >= 16 || !InterruptFlags(intiFlags)) return MadtPollingStatus.InvalidRecord;
                if ((isaSources & (1U << (int)source)) != 0) return MadtPollingStatus.DuplicateRecord;
                for (uint i = 0; i < 16; i++)
                    if ((isaSources & (1U << (int)i)) != 0 && isaGsi[i] == gsi) return MadtPollingStatus.DuplicateRecord;
                isaSources |= 1U << (int)source; isaGsi[source] = gsi; isoCount++;
                if (gsi > maximumIsoGsi) maximumIsoGsi = gsi;
            }
            else if (type == 4)
            {
                // Packed UID, two flag bytes and LINT fit exactly in this DWORD.
                uint value = EndianPrimitives.ReadUInt32LittleEndian(entry + 2);
                byte uid = (byte)value; uint lint = value >> 24;
                if (lint > 1 || !InterruptFlags((value >> 8) & 0xffff)) return MadtPollingStatus.InvalidRecord;
                uint lintBit = 1U << (int)lint;
                if ((wildcardLints & lintBit) != 0) return MadtPollingStatus.DuplicateRecord;
                if (uid == 255)
                {
                    if ((specificLints & lintBit) != 0) return MadtPollingStatus.DuplicateRecord;
                    wildcardLints |= lintBit;
                }
                else
                {
                    uint word = 8 + lint * 4 + (uint)(uid >> 6); ulong bit = 1UL << (uid & 63);
                    if ((seen[word] & bit) != 0) return MadtPollingStatus.DuplicateRecord;
                    seen[word] |= bit; specificLints |= lintBit;
                }
                nmiCount++;
            }
            else
            {
                if (entry[2] != 0 || entry[3] != 0) return MadtPollingStatus.InvalidRecord;
                if (hasOverride) return MadtPollingStatus.DuplicateRecord;
                localAddress = EndianPrimitives.ReadUInt64LittleEndian(entry + 4);
                if (!Address(localAddress)) return MadtPollingStatus.InvalidAddress;
                hasOverride = true;
            }
            records++; offset += length;
        }
        if (!enabledCpu) return MadtPollingStatus.MissingProcessor;
        if (processorApicId != bspApicId) return MadtPollingStatus.BspMismatch;
        if (!hasIo) return MadtPollingStatus.MissingIoApic;
        if (localAddress == ioAddress) return MadtPollingStatus.InvalidAddress;
        for (uint i = 0; i < 16; i++)
            if ((isaSources & (1U << (int)i)) != 0 && isaGsi[i] < gsiBase) return MadtPollingStatus.InvalidRecord;
        for (uint i = 0; i < 4; i++)
            if (((seen[8 + i] | seen[12 + i]) & ~seen[i]) != 0) return MadtPollingStatus.InvalidRecord;
        topology = new(localAddress, ioAddress, gsiBase, flags, processorApicId, processorUid,
            ioId, records, isoCount, nmiCount, hasOverride, maximumIsoGsi);
        return MadtPollingStatus.Success;
    }

    private static bool Address(ulong address) => address >= 0x100000 && address <= 0xfffff000 && (address & 4095) == 0;
    private static bool InterruptFlags(uint flags) => (flags & ~15U) == 0 && (flags & 3) != 2 && ((flags >> 2) & 3) != 2;
}

public enum XApicSetupStatus : byte
{
    Uninitialized, Success, InvalidAccess, InvalidCpu, InvalidTopology, ReadFailed,
    WriteFailed, UnsupportedHardware, ReadBackFailed, PendingInterrupt
}

/// <summary>Exclusive single-BSP access with IF clear. The adapter must authenticate
/// the two identity UC/RW/NX pages, actual CPUID/MSR/RFLAGS and each native callback.
/// Each MMIO callback performs exactly one ordered 32-bit access; 1 means success.
/// IOREGSEL/window operations must not be interleaved with another user.</summary>
public unsafe struct XApicSetupAccess
{
    public void* Context;
    public delegate* unmanaged<void*, ulong, uint*, byte> Read32;
    public delegate* unmanaged<void*, ulong, uint, byte> Write32;
    public delegate* unmanaged<void*, ushort, byte*, byte> ReadPort8;
    public delegate* unmanaged<void*, ushort, byte, byte> WritePort8;
}

public struct XApicSetupResult
{
    public XApicSetupStatus Status;
    public uint LocalVersion, IoVersion, MaskedLocalEntries, MaskedIoEntries, TaskPriority, Spurious;
    public bool LegacyPicMasked;
}

/// <summary>Bounded integrated xAPIC and 82093-compatible IOAPIC setup. All LVT/IO
/// sources stay masked, timer stopped, TPR blocks classes 0..13. Only the later
/// owned MSI-X vector and spurious return gate may run with IF enabled. This code
/// never enables CPU interrupts, acknowledges an interrupt or changes APIC mode.
/// Any failure after writes requires the caller to keep IF clear and stop.
/// Register authority: Intel SDM Vol3A June2024 chapter11 and 82093AA section3.</summary>
public static unsafe class BoundedXApicSetup
{
    public const byte CompletionVector = 0xe0, SpuriousVector = 0xff;
    public const uint TaskPriority = 0xd0, SpuriousValue = 0x1ff, MaskedVector = 0x10051;

    public static XApicSetupResult Run(MadtPollingTopology t, uint cpuidFeatures, uint actualBspId,
        ulong apicBaseMsr, ulong rflags, XApicSetupAccess a)
    {
        XApicSetupResult r = default;
        r.Status = XApicSetupStatus.InvalidAccess;
        if (a.Read32 == null || a.Write32 == null ||
            ((t.HeaderFlags & 1) != 0 && (a.ReadPort8 == null || a.WritePort8 == null))) return r;
        r.Status = XApicSetupStatus.InvalidTopology;
        if (t.RecordCount < 2 || t.BspApicId == 255 || t.IoApicId > 15 || (t.HeaderFlags & ~1U) != 0 ||
            !Page(t.LocalApicAddress) || !Page(t.IoApicAddress) || t.LocalApicAddress == t.IoApicAddress) return r;
        r.Status = XApicSetupStatus.InvalidCpu;
        // MSR/APIC feature bits; BSP, globally enabled xAPIC with an exact below-4G base.
        if ((cpuidFeatures & 0x220) != 0x220 || actualBspId != t.BspApicId ||
            apicBaseMsr != (t.LocalApicAddress | 0x900) || (rflags & 0x202) != 2) return r;
        r.Status = Configure(t, a, ref r);
        return r;
    }

    private static XApicSetupStatus Configure(MadtPollingTopology t, XApicSetupAccess a, ref XApicSetupResult r)
    {
        uint localId = 0, localVersion = 0, ioId = 0, ioVersion = 0;
        if (!Read(a, t.LocalApicAddress + 0x20, &localId) || !Read(a, t.LocalApicAddress + 0x30, &localVersion))
            return XApicSetupStatus.ReadFailed;
        var status = IoRead(t, a, 0, &ioId);
        if (status != XApicSetupStatus.Success) return status;
        status = IoRead(t, a, 1, &ioVersion);
        if (status != XApicSetupStatus.Success) return status;
        r.LocalVersion = localVersion; r.IoVersion = ioVersion;
        uint maxLvt = (localVersion >> 16) & 255, maxIo = (ioVersion >> 16) & 255;
        // Conventional LVT layout, up to 24 IO pins, and the reviewed 82093AA
        // (0x11) or QEMU (0x20) IOREGSEL/IOWIN interface. No direct IO EOI use.
        if (localId != (uint)t.BspApicId << 24 || ioId != (uint)t.IoApicId << 24 ||
            (localVersion & 255) < 0x10 || (localVersion & 255) > 0x15 || maxLvt < 3 || maxLvt > 6 ||
            (localVersion & 0xfe00ff00) != 0 || (ioVersion & 0xff00ff00) != 0 ||
            ((ioVersion & 255) != 0x11 && (ioVersion & 255) != 0x20) || maxIo > 23 ||
            t.IoApicGsiBase > ~0U - maxIo ||
            (t.IsoCount != 0 && t.MaximumIsoGsi > t.IoApicGsiBase + maxIo))
            return XApicSetupStatus.UnsupportedHardware;
        if ((t.HeaderFlags & 1) != 0)
        {
            byte master = 0, slave = 0;
            if (a.WritePort8(a.Context, 0x21, 255) != 1 || a.WritePort8(a.Context, 0xa1, 255) != 1)
                return XApicSetupStatus.WriteFailed;
            if (a.ReadPort8(a.Context, 0x21, &master) != 1 || a.ReadPort8(a.Context, 0xa1, &slave) != 1)
                return XApicSetupStatus.ReadFailed;
            if (master != 255 || slave != 255) return XApicSetupStatus.ReadBackFailed;
            r.LegacyPicMasked = true;
        }
        // Mask each old route before touching its destination; no source is enabled.
        for (uint i = 0; i <= maxIo; i++)
        {
            status = IoWriteChecked(t, a, 0x10 + 2 * i, MaskedVector);
            if (status != XApicSetupStatus.Success) return status;
            status = IoWriteChecked(t, a, 0x11 + 2 * i, (uint)t.BspApicId << 24);
            if (status != XApicSetupStatus.Success) return status;
            r.MaskedIoEntries++;
        }
        // Timer, LINT0/1 and error are mandatory; performance/thermal/CMCI follow maxLVT.
        for (uint i = 0; i <= maxLvt; i++)
        {
            uint offset = i == 0 ? 0x320U : i == 1 ? 0x350U : i == 2 ? 0x360U :
                i == 3 ? 0x370U : i == 4 ? 0x340U : i == 5 ? 0x330U : 0x2f0U;
            status = WriteChecked(a, t.LocalApicAddress + offset, MaskedVector);
            if (status != XApicSetupStatus.Success) return status;
            r.MaskedLocalEntries++;
        }
        status = WriteChecked(a, t.LocalApicAddress + 0x380, 0);
        if (status != XApicSetupStatus.Success) return status;
        status = WriteChecked(a, t.LocalApicAddress + 0x80, TaskPriority);
        if (status != XApicSetupStatus.Success) return status;
        r.TaskPriority = TaskPriority;
        status = WriteChecked(a, t.LocalApicAddress + 0xf0, SpuriousValue);
        if (status != XApicSetupStatus.Success) return status;
        r.Spurious = SpuriousValue;
        return CheckQuiescent(t.LocalApicAddress, a);
    }

    // Recheck immediately before enabling the owned MSI-X table entry. A pending
    // lower-priority firmware vector remains blocked by TPR; no existing ISR is ours.
    public static XApicSetupStatus CheckQuiescent(ulong localAddress, XApicSetupAccess a)
    {
        if (!Page(localAddress) || a.Read32 == null) return XApicSetupStatus.InvalidAccess;
        uint value = 0;
        for (uint i = 0; i < 8; i++)
        {
            if (!Read(a, localAddress + 0x100 + i * 16, &value)) return XApicSetupStatus.ReadFailed;
            if (value != 0) return XApicSetupStatus.PendingInterrupt;
        }
        if (!Read(a, localAddress + 0x270, &value)) return XApicSetupStatus.ReadFailed;
        if (value != 0) return XApicSetupStatus.PendingInterrupt;
        return XApicSetupStatus.Success;
    }

    private static bool Page(ulong v) => v >= 0x100000 && v <= 0xfffff000 && (v & 4095) == 0;
    private static bool Read(XApicSetupAccess a, ulong address, uint* value) => a.Read32(a.Context, address, value) == 1;
    private static XApicSetupStatus WriteChecked(XApicSetupAccess a, ulong address, uint value)
    {
        if (a.Write32(a.Context, address, value) != 1) return XApicSetupStatus.WriteFailed;
        uint observed = 0;
        if (!Read(a, address, &observed)) return XApicSetupStatus.ReadFailed;
        return observed == value ? XApicSetupStatus.Success : XApicSetupStatus.ReadBackFailed;
    }
    private static XApicSetupStatus IoRead(MadtPollingTopology t, XApicSetupAccess a, uint index, uint* value)
    {
        if (a.Write32(a.Context, t.IoApicAddress, index) != 1) return XApicSetupStatus.WriteFailed;
        return Read(a, t.IoApicAddress + 16, value) ? XApicSetupStatus.Success : XApicSetupStatus.ReadFailed;
    }
    private static XApicSetupStatus IoWriteChecked(MadtPollingTopology t, XApicSetupAccess a, uint index, uint value)
    {
        if (a.Write32(a.Context, t.IoApicAddress, index) != 1 || a.Write32(a.Context, t.IoApicAddress + 16, value) != 1)
            return XApicSetupStatus.WriteFailed;
        uint observed = 0;
        if (!Read(a, t.IoApicAddress + 16, &observed)) return XApicSetupStatus.ReadFailed;
        return observed == value ? XApicSetupStatus.Success : XApicSetupStatus.ReadBackFailed;
    }
}

public struct XApicCpuObservation
{
    public uint FeaturesEdx, BspId;
    public ulong ApicBaseMsr, Rflags;
}

/// <summary>uint (XApicCpuObservation*), Microsoft x64 ABI. Preserves RBX, checks
/// CPL0 and IF=0 before CPUID.1/MSR access, and requires MSR/APIC feature bits before
/// RDMSR IA32_APIC_BASE. Partial observations on refusal are not valid. The caller
/// authenticates writable output and immutable RX code; never invoke on the host.</summary>
public static unsafe class X64ApicObservation
{
    public const uint Bytes = 96;
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
    private static bool Storage(byte* p, ulong capacity) => p != null && capacity >= Bytes && (ulong)p <= ~0UL - Bytes;
    private static byte Byte(uint i)
    {
        // Independent decoded review: endbr64; preserve RBX; CPL/IF admission;
        // CPUID.1 -> EDX and EBX[31:24]; feature admission; RDMSR 0x1b; restore.
        ulong word = (i / 8) switch
        {
            0 => 0xc8894953fa1e0ff3UL, 1 => 0x9c487503a8c88c66UL,
            2 => 0x000200c1f7415941UL, 3 => 0x00000001b83c7500UL,
            4 => 0x18ebc1108941a20fUL, 5 => 0x1048894d04588941UL,
            6 => 0xfa8100000220e281UL, 7 => 0x1bb9197500000220UL,
            8 => 0xe2c148320f000000UL, 9 => 0x08408949d0094820UL,
            10 => 0x31c35b00000001b8UL, 11 => 0x0000000000c35bc0UL,
            _ => 0
        };
        return (byte)(word >> ((int)(i & 7) * 8));
    }
}
