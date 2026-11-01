using OS.Std.Memory;

namespace OS.Storage;

public readonly struct OsPackageOpenFileResult
{
    private OsPackageOpenFileResult(
        OsPackageFileSystemStatus status,
        OsPackageStatus packageStatus,
        OsPackageHeader header,
        OsPackageFileEntry entry)
    {
        IsInitialized = true;
        Status = status;
        PackageStatus = packageStatus;
        Header = header;
        Entry = entry;
    }

    public bool IsInitialized { get; }

    public OsPackageFileSystemStatus Status { get; }

    public OsPackageStatus PackageStatus { get; }

    public OsPackageHeader Header { get; }

    public OsPackageFileEntry Entry { get; }

    public ulong FileLength => Entry.ContentLength;

    public bool Succeeded => IsInitialized && Status == OsPackageFileSystemStatus.Success;

    public static OsPackageOpenFileResult Success(
        OsPackageHeader header,
        OsPackageFileEntry entry)
    {
        return new OsPackageOpenFileResult(
            OsPackageFileSystemStatus.Success,
            OsPackageStatus.Success,
            header,
            entry);
    }

    public static OsPackageOpenFileResult Fail(
        OsPackageFileSystemStatus status,
        OsPackageStatus packageStatus)
    {
        return new OsPackageOpenFileResult(status, packageStatus, default, default);
    }
}
