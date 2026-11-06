using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;

public readonly struct NativeAppExitParseResult
{
    private NativeAppExitParseResult(NativeAppExitParseStatus status, bool isInitialized, NativeAppExitStatusRecord record, int bytesRead)
    {
        Status = status;
        IsInitialized = isInitialized;
        Record = record;
        BytesRead = bytesRead;
    }

    public NativeAppExitParseStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public NativeAppExitStatusRecord Record
    {
        get;
    }
    public int BytesRead
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeAppExitParseStatus.Success;

    public static NativeAppExitParseResult Success(NativeAppExitStatusRecord record, int bytesRead)
    {
        return new NativeAppExitParseResult(NativeAppExitParseStatus.Success, isInitialized: true, record, bytesRead);
    }

    public static NativeAppExitParseResult Fail(NativeAppExitParseStatus status)
    {
        return new NativeAppExitParseResult(status, isInitialized: true, default, bytesRead: 0);
    }
}
