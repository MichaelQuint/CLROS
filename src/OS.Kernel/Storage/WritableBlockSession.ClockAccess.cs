using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class WritableBlockSession
{
    /// <summary>Trusted monotonic nanoseconds, exactly1 on success. The adapter
    /// retains nonthrowing code/context and the actual mapping/device authority.</summary>
    public struct ClockAccess
    {
        public void* Context;
        public delegate* unmanaged<void*, ulong*, byte> Read;
    }
}
