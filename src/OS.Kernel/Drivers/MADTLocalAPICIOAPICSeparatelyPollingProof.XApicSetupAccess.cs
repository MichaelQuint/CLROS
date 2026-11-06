using OS.Std.Binary;

namespace OS.Drivers;
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
