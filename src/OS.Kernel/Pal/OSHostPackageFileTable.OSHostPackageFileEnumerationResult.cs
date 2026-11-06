using OS.Std.Memory;
using OS.Storage;

namespace OS.Pal;

public readonly struct OSHostPackageFileEnumerationResult
{
    public readonly OSHostStatus Status;
    public readonly ulong BytesWritten;
    public readonly ulong RequiredBytes;
    public readonly OSHostFileInfo Info;
    public OSHostPackageFileEnumerationResult(OSHostStatus status, ulong bytesWritten, ulong requiredBytes, OSHostFileInfo info, bool isInitialized = true)
    {
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        Info = info;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostPackageFileEnumerationResult FromStatus(OSHostStatus status, ulong requiredBytes = 0, OSHostFileInfo info = default)
    {
        return new OSHostPackageFileEnumerationResult(status, bytesWritten: 0, requiredBytes, info);
    }
}
