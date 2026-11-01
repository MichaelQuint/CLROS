using OS.Manifest;
using OS.Std;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Pal;
public readonly struct OSHostConfigQueryResult
{
    public OSHostConfigQueryResult(OSHostStatus status, ulong bytesWritten, ulong requiredBytes, bool isInitialized = true)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        IsInitialized = isInitialized;
    }

    public OSHostStatus Status { get; }
    public ulong BytesWritten { get; }
    public ulong RequiredBytes { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostConfigQueryResult FromStatus(OSHostStatus status, ulong requiredBytes = 0)
    {
        return new OSHostConfigQueryResult(status, bytesWritten: 0, requiredBytes);
    }
}
