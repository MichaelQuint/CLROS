using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class WritableBlockSession
{
    public enum Status : byte
    {
        Uninitialized, Success, InvalidAccess, UnsupportedFeatures, ReadOnlyDevice,
        UnsupportedFlush, InvalidState, SetupFailed, InvalidCapacity, ConfigurationChanged,
        InvalidNotify, ReadFailed, WriteFailed, StatusRejected, OrderingFailed,
        BusMasterFailed, DmaRejected, ClockFailed, ClockRegressed, GenerationExhausted,
        SectorOutOfRange, Busy, Idle, Pending, Cancelled, StaleToken, InvalidStorage,
        InvalidCompletion, DeviceIoError, UnsupportedRequest, Timeout, ResetFailed, Ready
    }
}
