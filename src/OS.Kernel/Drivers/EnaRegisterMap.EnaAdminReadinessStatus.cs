namespace OS.Drivers;

public enum EnaAdminReadinessStatus
{
    Success = 0,
    RegisterApertureTooSmall = 1,
    UnsupportedContiguousQueueRequirement = 2,
    DeviceFatalError = 3,
    DeviceResetInProgress = 4,
    AdminQueueRestartInProgress = 5,
    DeviceQuiescentStateInProgress = 6,
    DeviceNotReady = 7
}
