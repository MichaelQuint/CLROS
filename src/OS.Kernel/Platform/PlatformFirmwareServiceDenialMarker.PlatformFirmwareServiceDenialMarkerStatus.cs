using OS.Std.Text;

namespace OS.Kernel.Platform;

public enum PlatformFirmwareServiceDenialMarkerStatus
{
    Success = 0,
    InvalidResult = 1,
    AllowedCall = 2,
    InvalidOperation = 3,
    DestinationTooSmall = 4
}
