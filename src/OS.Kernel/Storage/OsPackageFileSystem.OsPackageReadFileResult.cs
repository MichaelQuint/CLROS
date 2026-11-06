using OS.Std.Memory;

namespace OS.Storage;

public readonly struct OsPackageReadFileResult
{
    private OsPackageReadFileResult(
        OsPackageFileSystemStatus status,
        OsPackageStatus packageStatus,
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ulong bytesRead,
        ulong requiredBytes)
    {
        IsInitialized = true;
        Status = status;
        PackageStatus = packageStatus;
        Header = header;
        Entry = entry;
        BytesRead = bytesRead;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized
    {
        get;
    }

    public OsPackageFileSystemStatus Status
    {
        get;
    }

    public OsPackageStatus PackageStatus
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

    public ulong BytesRead
    {
        get;
    }

    public ulong RequiredBytes
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == OsPackageFileSystemStatus.Success;

    public static OsPackageReadFileResult Success(
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ulong bytesRead)
    {
        return new OsPackageReadFileResult(
            OsPackageFileSystemStatus.Success,
            OsPackageStatus.Success,
            header,
            entry,
            bytesRead,
            bytesRead);
    }

    public static OsPackageReadFileResult Fail(
        OsPackageFileSystemStatus status,
        OsPackageStatus packageStatus,
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ulong bytesRead,
        ulong requiredBytes)
    {
        return new OsPackageReadFileResult(
            status,
            packageStatus,
            header,
            entry,
            bytesRead,
            requiredBytes);
    }
}
