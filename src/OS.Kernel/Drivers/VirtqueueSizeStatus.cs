namespace OS.Drivers;
public enum VirtqueueSizeStatus : byte
{
    Uninitialized,
    Success,
    InvalidRequested,
    InvalidMaximum,
    Unavailable,
    ExceedsMaximum,
    InvalidIndex,
    InvalidProgress
}
