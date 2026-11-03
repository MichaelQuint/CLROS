using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Storage;

public readonly struct OsPackageFilePathCopyResult
{
    private OsPackageFilePathCopyResult(
        OsPackageStatus status,
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ulong bytesWritten,
        ulong requiredBytes)
    {
        IsInitialized = true;
        Status = status;
        Header = header;
        Entry = entry;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized
    {
        get;
    }

    public OsPackageStatus Status
    {
        get;
    }

    public OsPackageHeader Header
    {
        get;
    }

    public OsPackageFileEntry Entry
    {
        get;
    }

    public ulong BytesWritten
    {
        get;
    }

    public ulong RequiredBytes
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == OsPackageStatus.Success;

    public static OsPackageFilePathCopyResult Success(
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ulong bytesWritten)
    {
        return new OsPackageFilePathCopyResult(
            OsPackageStatus.Success,
            header,
            entry,
            bytesWritten,
            bytesWritten);
    }

    public static OsPackageFilePathCopyResult Fail(
        OsPackageStatus status,
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ulong requiredBytes = 0)
    {
        return new OsPackageFilePathCopyResult(
            status,
            header,
            entry,
            bytesWritten: 0,
            requiredBytes);
    }
}
