namespace OS.Drivers;

public enum BoundedPciStatus : byte
{
    Uninitialized, Success, InvalidWindow, InvalidAccess, OutsideWindow,
    InvalidTuple, InvalidRegister, AbsentDevice, UnsupportedHeader, InvalidBridge,
    Capacity, ReadBudget
}

/// <summary>Numeric MCFG authority, not proof that memory has been mapped.</summary>
public readonly struct BoundedPciWindow
{
    public readonly ulong BusZeroBase, Start, Bytes;
    public readonly ushort Segment;
    public readonly byte AllocationFirst, AllocationLast, FirstBus, LastBus;
    internal BoundedPciWindow(ulong origin, ushort segment, byte first, byte last, byte start, byte end)
    {
        BusZeroBase = origin; Segment = segment; AllocationFirst = first; AllocationLast = last;
        FirstBus = start; LastBus = end; Start = origin + ((ulong)start << 20);
        Bytes = ((ulong)end - start + 1) << 20;
    }
    public bool IsValid => BusZeroBase >= 0x100000 && (BusZeroBase & 0xfffff) == 0 &&
        AllocationFirst <= FirstBus && FirstBus <= LastBus && LastBus <= AllocationLast &&
        LastBus - FirstBus < 4 && BusZeroBase <= 0x100000000UL - (((ulong)AllocationLast + 1) << 20) &&
        Start == BusZeroBase + ((ulong)FirstBus << 20) && Bytes == ((ulong)LastBus - FirstBus + 1) << 20;
}

/// <summary>
/// Exclusive adapter-owned session. The adapter proves UC/NX mapping, native callback
/// lifetime and hardware authority before opening. Keep one mutable instance; copying
/// it does not create an independently revocable capability. No hotplug or config writes.
/// </summary>
public unsafe struct BoundedPciAccess
{
    internal BoundedPciWindow window;
    internal void* context;
    internal delegate* unmanaged<void*, ulong, uint> read;
    public readonly BoundedPciWindow Window => window;
    public readonly bool IsActive => read != null && window.IsValid;
}

public static unsafe class BoundedPCIConfigurationSpaceInterface
{
    public static bool TryWindow(ulong busZeroBase, ushort segment, byte allocationFirst,
        byte allocationLast, byte firstBus, byte lastBus, out BoundedPciWindow window)
    {
        window = default;
        // Check before constructing: no wrapped intermediate address or length.
        if (busZeroBase < 0x100000 || (busZeroBase & 0xfffff) != 0 ||
            allocationFirst > firstBus || firstBus > lastBus || lastBus > allocationLast ||
            lastBus - firstBus >= 4 || busZeroBase > 0x100000000UL - (((ulong)allocationLast + 1) << 20)) return false;
        window = new BoundedPciWindow(busZeroBase, segment, allocationFirst, allocationLast, firstBus, lastBus);
        return true;
    }

    public static bool TryOpen(BoundedPciWindow window, void* context,
        delegate* unmanaged<void*, ulong, uint> read, out BoundedPciAccess access)
    {
        access = default;
        if (!window.IsValid || read == null) return false;
        access.window = window; access.context = context; access.read = read;
        return true;
    }

    public static void Revoke(ref BoundedPciAccess access) { access = default; }

    public static BoundedPciStatus Address(BoundedPciWindow window, byte bus, byte device,
        byte function, uint offset, out ulong address)
    {
        address = 0;
        if (!window.IsValid) return BoundedPciStatus.InvalidWindow;
        if (bus < window.FirstBus || bus > window.LastBus) return BoundedPciStatus.OutsideWindow;
        if (device > 31 || function > 7) return BoundedPciStatus.InvalidTuple;
        if (offset > 4092 || (offset & 3) != 0) return BoundedPciStatus.InvalidRegister;
        // MCFG base always corresponds to bus zero, including nonzero StartBus.
        address = window.BusZeroBase + ((ulong)bus << 20) + ((ulong)device << 15) + ((ulong)function << 12) + offset;
        return BoundedPciStatus.Success;
    }

    public static BoundedPciStatus Read32(ref BoundedPciAccess access, byte bus, byte device,
        byte function, uint offset, out uint value)
    {
        value = 0;
        if (!access.IsActive) return BoundedPciStatus.InvalidAccess;
        BoundedPciStatus status = Address(access.window, bus, device, function, offset, out ulong address);
        if (status != BoundedPciStatus.Success) return status;
        value = access.read(access.context, address);
        return BoundedPciStatus.Success;
    }
}

/// <summary>Microsoft x64 ABI: ignore RCX context; read one DWORD from RDX into EAX.</summary>
public static unsafe class X64PciConfigurationRead
{
    public const uint Bytes = 16;
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
    private static byte Byte(uint i) => i < 8 ? (byte)(0x00c3028bfa1e0ff3UL >> ((int)i * 8)) : (byte)0;
}
