using OS.Kernel.Platform;

namespace OS.Boot;
// Retain this original zero-initialized record in the image's adapter state.
// Self detects accidental record copies; OriginalArena/Storage bind the accepted
// owner. These are immutable admission facts, not another revocation Boolean.
public unsafe struct FirmwareAdapterAuthority
{
    internal FirmwareAdapterAuthority* Self;
    internal FirstExitOwnedArenaState* OriginalArena;
    internal byte* OriginalStorage;
    internal ulong AdapterState, AdapterStateBytes;
    internal FirmwareAdapterBackends Backends;
}
