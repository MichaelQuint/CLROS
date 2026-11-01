using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class ContinuingReadOnlyBlockService
{
    /// <summary>Trusted live monotonic nanoseconds; return exactly1 on success.
    /// Context/code/backing remain valid through cleanup, and never throw.</summary>
    public struct ClockAccess
    {
        public void* Context;
        public delegate* unmanaged<void*, ulong*, byte> Read;
    }
}
