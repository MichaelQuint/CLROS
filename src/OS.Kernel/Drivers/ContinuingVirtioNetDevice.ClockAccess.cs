using RingEngine = global::OS.Drivers.ContinuingVirtioNet;

namespace OS.Drivers;
public static unsafe partial class ContinuingVirtioNetDevice
{
    /// <summary>Trusted live monotonic nanoseconds. Return1 only for a real
    /// successful read; context/code/backing remain mapped through Stop.</summary>

    public struct ClockAccess
    {
        public void* Context;
        public delegate* unmanaged<void*, ulong*, byte> Read;
    }
}
