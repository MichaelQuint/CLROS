using OS.Kernel.Platform;

namespace OS.Boot;

public enum FirmwareAdapterStatus : uint
{
    Unbound,
    Success,
    AlreadyBound,
    InvalidAuthority,
    ServicesUnavailable,
    BackendUnavailable,
    InvalidRange,
    AliasedStorage,
    FirmwareError
}
