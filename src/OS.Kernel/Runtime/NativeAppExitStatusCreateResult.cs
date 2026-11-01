using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppExitStatusCreateResult
{
    private NativeAppExitStatusCreateResult(NativeAppExitMarkerStatus status, bool isInitialized, NativeAppExitStatusRecord record)
    {
        Status = status;
        IsInitialized = isInitialized;
        Record = record;
    }

    public NativeAppExitMarkerStatus Status { get; }
    public bool IsInitialized { get; }
    public NativeAppExitStatusRecord Record { get; }
    public bool Succeeded => IsInitialized && Status == NativeAppExitMarkerStatus.Success;

    public static NativeAppExitStatusCreateResult Success(NativeAppExitStatusRecord record)
    {
        return new NativeAppExitStatusCreateResult(NativeAppExitMarkerStatus.Success, isInitialized: true, record);
    }

    public static NativeAppExitStatusCreateResult Fail(NativeAppExitMarkerStatus status)
    {
        return new NativeAppExitStatusCreateResult(status, isInitialized: true, default);
    }
}
