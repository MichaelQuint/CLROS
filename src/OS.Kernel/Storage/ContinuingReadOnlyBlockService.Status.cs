using OS.Drivers;

namespace OS.Storage;

public static unsafe partial class ContinuingReadOnlyBlockService
{
    public enum Status : byte
    {
        Uninitialized, Success, InvalidAccess, UnsupportedFeatures, InvalidState,
        SetupFailed, InvalidCapacity, ConfigurationChanged, InvalidNotify,
        ReadFailed, WriteFailed, StatusRejected, OrderingFailed, BusMasterFailed,
        DmaRejected, ClockFailed, ClockRegressed, GenerationExhausted,
        SectorOutOfRange, Busy, Idle, Pending, Ready, Cancelled, StaleToken,
        ShortBuffer, InvalidStorage, InvalidCompletion, DeviceIoError,
        UnsupportedRequest, Timeout, ResetFailed, ObservationRejected
    }
}
