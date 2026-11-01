using OS.Std.Binary;

namespace OS.Drivers;
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
