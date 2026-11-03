namespace OS.Boot;

public unsafe struct WorkloadServiceLoopCallbacks
{
    // Exactly 1 means success. Byte results preserve the native/host unmanaged ABI.
    public void* Context;
    public delegate* unmanaged<void*, ulong*, byte> ReadClock;
    public delegate* unmanaged<void*, WorkloadServiceLoopState*, uint, ulong, byte> Service;
    public delegate* unmanaged<void*, uint, ulong, byte> Stop;
}
