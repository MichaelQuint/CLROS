using OS.Std.Memory;
using OS.Storage;

namespace OS.Pal;
public readonly struct OSHostPackageFileReadResult
{
    public readonly OSHostStatus Status;
    public readonly ulong BytesRead;
    public readonly ulong RequiredBytes;
    public OSHostPackageFileReadResult(OSHostStatus status, ulong bytesRead, ulong requiredBytes, bool isInitialized = true)
    {
        Status = status;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostPackageFileReadResult FromStatus(OSHostStatus status, ulong requiredBytes = 0)
    {
        return new OSHostPackageFileReadResult(status, bytesRead: 0, requiredBytes);
    }
}
