using OS.Std.Memory;
using OS.Storage;

namespace OS.Pal;

public readonly struct OSHostPackageFileOpenResult
{
    public readonly OSHostStatus Status;
    public readonly OSHostHandle File;
    public readonly OSHostFileInfo Info;
    public OSHostPackageFileOpenResult(OSHostStatus status, OSHostHandle file, OSHostFileInfo info, bool isInitialized = true)
    {
        Status = status;
        File = file;
        Info = info;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;
    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostPackageFileOpenResult FromStatus(OSHostStatus status)
    {
        return new OSHostPackageFileOpenResult(status, default, default);
    }
}
