using OS.Kernel.Platform;

namespace OS.Boot;
// One caller-owned, serialized attempt. Do not copy/reset a live state to retry.
public unsafe struct TypedExitBootServicesState
{
    internal delegate* unmanaged<void*, ulong, ulong> Function;
    internal void* Handle;
    internal ulong Key;
    internal ulong ReturnedStatus;
    internal TypedExitBootServicesPhase PhaseValue;
    public TypedExitBootServicesPhase Phase => PhaseValue;
    public ulong ImageHandle => (ulong)Handle;
    public ulong MapKey => Key;
    public ulong FirmwareStatus => ReturnedStatus;
}
