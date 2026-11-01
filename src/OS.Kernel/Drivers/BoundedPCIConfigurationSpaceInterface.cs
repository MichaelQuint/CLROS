namespace OS.Drivers;
public static unsafe class BoundedPCIConfigurationSpaceInterface
{
    public static bool TryWindow(ulong busZeroBase, ushort segment, byte allocationFirst, byte allocationLast, byte firstBus, byte lastBus, out BoundedPciWindow window)
    {
        window = default;
        // Check before constructing: no wrapped intermediate address or length.
        if (busZeroBase < 0x100000 || (busZeroBase & 0xfffff) != 0 || allocationFirst > firstBus || firstBus > lastBus || lastBus > allocationLast || lastBus - firstBus >= 4 || busZeroBase > 0x100000000UL - (((ulong)allocationLast + 1) << 20))
            return false;
        window = new BoundedPciWindow(busZeroBase, segment, allocationFirst, allocationLast, firstBus, lastBus);
        return true;
    }

    public static bool TryOpen(BoundedPciWindow window, void* context, delegate* unmanaged<void*, ulong, uint> read, out BoundedPciAccess access)
    {
        access = default;
        if (!window.IsValid || read == null)
            return false;
        access.window = window;
        access.context = context;
        access.read = read;
        return true;
    }

    public static void Revoke(ref BoundedPciAccess access)
    {
        access = default;
    }

    public static BoundedPciStatus Address(BoundedPciWindow window, byte bus, byte device, byte function, uint offset, out ulong address)
    {
        address = 0;
        if (!window.IsValid)
            return BoundedPciStatus.InvalidWindow;
        if (bus < window.FirstBus || bus > window.LastBus)
            return BoundedPciStatus.OutsideWindow;
        if (device > 31 || function > 7)
            return BoundedPciStatus.InvalidTuple;
        if (offset > 4092 || (offset & 3) != 0)
            return BoundedPciStatus.InvalidRegister;
        // MCFG base always corresponds to bus zero, including nonzero StartBus.
        address = window.BusZeroBase + ((ulong)bus << 20) + ((ulong)device << 15) + ((ulong)function << 12) + offset;
        return BoundedPciStatus.Success;
    }

    public static BoundedPciStatus Read32(ref BoundedPciAccess access, byte bus, byte device, byte function, uint offset, out uint value)
    {
        value = 0;
        if (!access.IsActive)
            return BoundedPciStatus.InvalidAccess;
        BoundedPciStatus status = Address(access.window, bus, device, function, offset, out ulong address);
        if (status != BoundedPciStatus.Success)
            return status;
        value = access.read(access.context, address);
        return BoundedPciStatus.Success;
    }
}
