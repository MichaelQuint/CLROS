using System.Runtime.InteropServices;
using OS.Kernel.Platform;

namespace OS.Boot;

public enum FirmwareEntropySeedStatus
{
    NotStarted = 0,
    Success,
    InvalidSource,
    InvalidRequestLength,
    ScratchTooSmall,
    DestinationTooSmall,
    AliasedBuffers,
    AlreadyPublished,
    InvalidPublication,
    FirmwareServicesUnavailable,
    SourceUnavailable,
    SourceFailed,
    WrongLength,
    InvalidSourceResult,
    ReadbackMismatch
}
