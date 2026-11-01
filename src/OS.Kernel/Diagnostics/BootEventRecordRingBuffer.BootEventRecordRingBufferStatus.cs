namespace OS.Kernel.Diagnostics;
public enum BootEventRecordRingBufferStatus
{
    Success = 0,
    NotInitialized = 1,
    NoStorage = 2,
    DestinationTooSmall = 3
}
