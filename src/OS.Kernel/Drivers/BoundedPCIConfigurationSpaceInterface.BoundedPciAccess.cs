namespace OS.Drivers;
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
