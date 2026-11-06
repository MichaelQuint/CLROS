using OS.Kernel.Platform;

namespace OS.Boot;
// Typed adapter thunks, not a category-switch callback. The integration must
// authenticate their original code/protocol provenance and retain Context in its
// admitted image state. Optional null thunks represent unavailable services.
// Thunks must obey the supplied lengths, not mutate authority, throw or retain
// caller buffers, and return their actual firmware status. They own protocol
// semantics (including partial writes/error outputs), not the revocation guard.
public unsafe struct FirmwareAdapterBackends
{
    public void* Context;
    public ulong ContextBytes;
    public delegate* unmanaged<void*, char*, ulong, ulong> Console;
    public delegate* unmanaged<void*, byte*, ulong, FirmwareAdapterMemoryMapInfo*, ulong> MemoryMap;
    public delegate* unmanaged<void*, ulong, ulong, byte*, ulong, ulong*, ulong> FileSystem;
    public delegate* unmanaged<void*, ulong*, ulong> Timer;
    public delegate* unmanaged<void*, byte*, ulong, ulong> Random;
    public delegate* unmanaged<void*, ulong, ulong, ulong*, ulong> ConfigurationTable;
    public delegate* unmanaged<void*, ulong, byte*, ulong, ulong> PhysicalMemory;
    public delegate* unmanaged<void*, FirmwareAdapterFramebufferInfo*, ulong> Framebuffer;
}
