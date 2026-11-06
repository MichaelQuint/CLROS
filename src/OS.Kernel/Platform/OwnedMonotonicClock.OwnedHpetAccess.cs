namespace OS.Kernel.Platform;
/// <summary>
/// Adapter-owned, aligned 64-bit MMIO operations. Each call must perform exactly one
/// ordered device access. The adapter owns the exclusive device and callback lifetime.
/// </summary>

public unsafe struct OwnedHpetAccess
{
    public void* Context;
    public delegate* unmanaged<void*, uint, ulong> Read;
    public delegate* unmanaged<void*, uint, ulong, void> Write;
    public uint HardwareId;
}
