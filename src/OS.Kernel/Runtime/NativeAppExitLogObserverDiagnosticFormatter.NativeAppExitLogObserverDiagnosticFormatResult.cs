using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppExitLogObserverDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;
    public NativeAppExitLogObserverDiagnosticFormatResult(NativeAppExitLogObserverDiagnosticFormatStatus status, NativeAppExitLogObserverStatus observerStatus, int bytesWritten, int requiredBytes, int availableBytes, uint flags = InitializedFlag)
    {
        Status = status;
        ObserverStatus = observerStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public NativeAppExitLogObserverDiagnosticFormatStatus Status { get; }
    public NativeAppExitLogObserverStatus ObserverStatus { get; }
    public int BytesWritten { get; }
    public int RequiredBytes { get; }
    public int AvailableBytes { get; }
    public uint Flags { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == NativeAppExitLogObserverDiagnosticFormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes && RequiredBytes <= AvailableBytes;

    public static NativeAppExitLogObserverDiagnosticFormatResult Fail(NativeAppExitLogObserverDiagnosticFormatStatus status, NativeAppExitLogObserverStatus observerStatus, int requiredBytes, int availableBytes)
    {
        return new NativeAppExitLogObserverDiagnosticFormatResult(status, observerStatus, bytesWritten: 0, requiredBytes, availableBytes);
    }
}
