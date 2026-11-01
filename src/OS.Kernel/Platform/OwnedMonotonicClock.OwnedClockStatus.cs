namespace OS.Kernel.Platform;
public enum OwnedClockStatus : byte
{
    Uninitialized,
    Ready,
    InvalidAccess,
    InvalidCapabilities,
    ConfigurationFailed,
    Stalled,
    TscUnavailable,
    InvalidCounter
}
