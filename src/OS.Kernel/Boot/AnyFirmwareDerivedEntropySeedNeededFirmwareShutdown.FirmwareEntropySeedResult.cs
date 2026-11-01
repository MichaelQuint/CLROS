using System.Runtime.InteropServices;
using OS.Kernel.Platform;

namespace OS.Boot;
public readonly struct FirmwareEntropySeedResult
{
    internal FirmwareEntropySeedResult(FirmwareEntropySeedStatus status, int requestedBytes, PlatformRandomResult sourceResult = default)
    {
        Status = status;
        RequestedBytes = requestedBytes;
        SourceResult = sourceResult;
    }

    public FirmwareEntropySeedStatus Status { get; }
    public int RequestedBytes { get; }
    public PlatformRandomResult SourceResult { get; }
    public bool Succeeded => Status == FirmwareEntropySeedStatus.Success;
}
