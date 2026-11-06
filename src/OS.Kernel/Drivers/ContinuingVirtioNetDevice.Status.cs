using RingEngine = global::OS.Drivers.ContinuingVirtioNet;

namespace OS.Drivers;

public static unsafe partial class ContinuingVirtioNetDevice
{
    public enum Status : byte
    {
        Uninitialized,
        Success,
        InvalidAccess,
        InvalidState,
        UnsupportedFeatures,
        SetupFailed,
        DmaRejected,
        RingFailed,
        ReadFailed,
        WriteFailed,
        OrderingFailed,
        ClockFailed,
        ClockRegressed,
        Timeout,
        Full,
        Empty,
        ShortBuffer,
        InvalidFrame,
        InvalidStorage,
        ResetFailed
    }
}
