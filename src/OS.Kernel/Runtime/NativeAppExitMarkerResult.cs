using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppExitMarkerResult
{
    public NativeAppExitMarkerResult(NativeAppExitMarkerStatus status, int bytesWritten, int requiredBytes, OSHostStatus logStatus)
    {
        Status = status;
        IsInitialized = true;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        LogStatus = logStatus;
    }

    public NativeAppExitMarkerStatus Status { get; }
    public bool IsInitialized { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public OSHostStatus LogStatus { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppExitMarkerStatus.Success;
}
