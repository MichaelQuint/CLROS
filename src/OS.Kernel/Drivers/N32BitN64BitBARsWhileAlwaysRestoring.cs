namespace OS.Drivers;

public enum PciBarProbeStatus : byte
{
    Uninitialized, Success, InvalidInput, ReadFailed, WriteFailed, AbsentDevice,
    UnsupportedHeader, UnsupportedType, InvalidMask, InvalidExtent, DecodeNotDisabled,
    RestoreFailed, BusMasterEnabled
}

/// <summary>Microsoft x64 ABI leaves: RCX context ignored, RDX address,
/// R8 output pointer/value. Return 1 after one DWORD read or DWORD/WORD write.
/// ENDBR64 entry, no stack/nonvolatile changes. Adapter authenticates RX storage.</summary>
public static unsafe class X64PciBarAccess
{
    public const uint StubBytes = 16, Bytes = 48;
    public static bool Emit(byte* code, ulong capacity)
    {
        if (code == null || capacity < Bytes || (ulong)code > ~0UL - Bytes) return false;
        for (uint i = 0; i < Bytes; i++) code[i] = Byte(i);
        return Validate(code, capacity);
    }
    public static bool Validate(byte* code, ulong capacity)
    {
        if (code == null || capacity < Bytes || (ulong)code > ~0UL - Bytes) return false;
        for (uint i = 0; i < Bytes; i++) if (code[i] != Byte(i)) return false;
        return true;
    }
    private static byte Byte(uint index)
    {
        uint stub = index / 16, offset = index % 16;
        ulong word = stub == 0 ? (offset < 8 ? 0x8941028bfa1e0ff3UL : 0x00c300000001b800UL) :
            stub == 1 ? (offset < 8 ? 0xb8028944fa1e0ff3UL : 0x000000c300000001UL) :
            (offset < 8 ? 0x02894466fa1e0ff3UL : 0x0000c300000001b8UL);
        return (byte)(word >> ((int)(offset & 7) * 8));
    }
}

public readonly struct PciBarProbeResult
{
    public readonly PciBarProbeStatus Status, OperationStatus;
    public readonly bool RestoreAttempted, Restored;
    internal PciBarProbeResult(PciBarProbeStatus operation, bool attempted, bool restored)
    { OperationStatus = operation; RestoreAttempted = attempted; Restored = restored;
      Status = attempted && !restored ? PciBarProbeStatus.RestoreFailed : operation; }
    public bool Succeeded => Status == PciBarProbeStatus.Success && RestoreAttempted && Restored;
}

/// <summary>
/// Adapter supplies exclusive quiescent device ownership, UC/RW/NX ECAM mapping,
/// and non-throwing callbacks returning 1 on success (0 may still have written).
/// Only type-0 BARs and the 16-bit Command register are written. No MMIO payload
/// access, bus-master enable, hotplug, CPU-fault recovery or concurrent probing.
/// Every path after the first write attempts restoration; failed restoration
/// prevents re-enabling decode. Hardware faults cannot promise restoration.
/// </summary>
public static unsafe class N32BitN64BitBARsWhileAlwaysRestoring
{
    public static PciBarProbeResult Probe(BoundedPciWindow window, byte bus, byte device, byte function,
        void* context, delegate* unmanaged<void*, ulong, uint*, byte> read,
        delegate* unmanaged<void*, ulong, uint, byte> write32,
        delegate* unmanaged<void*, ulong, uint, byte> write16,
        PciSizedBar* output, uint capacity)
    {
        if (read == null || write32 == null || write16 == null || output == null || capacity < 6 ||
            ((ulong)output & 7) != 0 || (ulong)output > ~0UL - (ulong)(6 * sizeof(PciSizedBar)) ||
            BoundedPCIConfigurationSpaceInterface.Address(window, bus, device, function, 0, out ulong page) != BoundedPciStatus.Success)
            return new(PciBarProbeStatus.InvalidInput, false, false);
        if ((ulong)output < window.Start + window.Bytes && window.Start < (ulong)output + (ulong)(6 * sizeof(PciSizedBar)))
            return new(PciBarProbeStatus.InvalidInput, false, false);
        uint identity = 0, header = 0, commandStatus = 0;
        if (read(context, page, &identity) != 1 || read(context, page + 12, &header) != 1 ||
            read(context, page + 4, &commandStatus) != 1) return new(PciBarProbeStatus.ReadFailed, false, false);
        if ((identity & 0xffff) == 0xffff) return new(PciBarProbeStatus.AbsentDevice, false, false);
        if (((header >> 16) & 0x7f) != 0) return new(PciBarProbeStatus.UnsupportedHeader, false, false);
        if ((commandStatus & 4) != 0) return new(PciBarProbeStatus.BusMasterEnabled, false, false);
        uint* original = stackalloc uint[6];
        PciSizedBar* sized = stackalloc PciSizedBar[6];
        for (uint i = 0; i < 6; i++)
        {
            sized[i] = default;
            if (read(context, page + 16 + i * 4, original + i) != 1) return new(PciBarProbeStatus.ReadFailed, false, false);
        }
        for (uint i = 0; i < 6; i++)
        {
            var format = BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Format(original[i], i, out var kind);
            if (format != PciBarProbeStatus.Success) return new(format, false, false);
            if (kind == PciSizedBarKind.Memory64) i++;
        }
        uint command = commandStatus & 0xffff, disabled = command & ~7u, observed = 0;
        // Once any write is attempted, no early return may bypass this cleanup.
        PciBarProbeStatus status = write16(context, page + 4, disabled) == 1 ? PciBarProbeStatus.Success : PciBarProbeStatus.WriteFailed;
        if (status == PciBarProbeStatus.Success)
        {
            if (read(context, page + 4, &observed) != 1) status = PciBarProbeStatus.ReadFailed;
            else if ((observed & 0xffff) != disabled) status = PciBarProbeStatus.DecodeNotDisabled;
        }
        for (uint i = 0; i < 6 && status == PciBarProbeStatus.Success; i++)
        {
            bool wide = (original[i] & 7) == 4;
            ulong address = page + 16 + i * 4;
            uint low = 0, high = 0;
            if (write32(context, address, 0xffffffff) != 1 || (wide && write32(context, address + 4, 0xffffffff) != 1))
                status = PciBarProbeStatus.WriteFailed;
            else if (read(context, address, &low) != 1 || (wide && read(context, address + 4, &high) != 1))
                status = PciBarProbeStatus.ReadFailed;
            else status = BARPrefetchTypeRegressionTestsDerivedMOSAFailures.Decode(original[i], wide ? original[i + 1] : 0,
                low, high, i, out sized[i]);
            // Restore each probe promptly, then repeat all restoration/readback below even on failure.
            bool upper = !wide || write32(context, address + 4, original[i + 1]) == 1;
            bool lower = write32(context, address, original[i]) == 1;
            if (!upper || !lower) status = PciBarProbeStatus.WriteFailed;
            if (wide) { sized[i + 1] = new(PciSizedBarKind.UpperHalf, 0, 0, false); i++; }
        }
        bool restored = true;
        for (int i = 5; i >= 0; i--)
            if (write32(context, page + 16 + (uint)i * 4, original[i]) != 1) restored = false;
        for (uint i = 0; i < 6; i++)
        {
            observed = 0;
            if (read(context, page + 16 + i * 4, &observed) != 1 || observed != original[i]) restored = false;
        }
        // On uncertain BAR restore, leave both decoding and bus mastering disabled.
        uint finalCommand = restored ? command : disabled;
        bool commandWritten = write16(context, page + 4, finalCommand) == 1;
        observed = 0;
        bool commandRead = read(context, page + 4, &observed) == 1;
        restored = restored && commandWritten && commandRead && (observed & 0xffff) == command;
        if (status == PciBarProbeStatus.Success && restored)
            for (uint i = 0; i < 6; i++) output[i] = sized[i];
        return new(status, true, restored);
    }
}
