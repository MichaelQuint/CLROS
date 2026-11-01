using OS.Std.Memory;

namespace OS.Storage;

public readonly struct OsPackageEnumerateFileResult
{
    private OsPackageEnumerateFileResult(
        OsPackageFileSystemStatus status,
        OsPackageStatus packageStatus,
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ulong bytesWritten,
        ulong requiredBytes)
    {
        IsInitialized = true;
        Status = status;
        PackageStatus = packageStatus;
        Header = header;
        Entry = entry;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized { get; }

    public OsPackageFileSystemStatus Status { get; }

    public OsPackageStatus PackageStatus { get; }

    public OsPackageHeader Header { get; }

    public OsPackageFileEntry Entry { get; }

    public ulong BytesWritten { get; }

    public ulong RequiredBytes { get; }

    public bool Succeeded => IsInitialized && Status == OsPackageFileSystemStatus.Success;

    public static OsPackageEnumerateFileResult Success(
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ulong bytesWritten)
    {
        return new OsPackageEnumerateFileResult(
            OsPackageFileSystemStatus.Success,
            OsPackageStatus.Success,
            header,
            entry,
            bytesWritten,
            bytesWritten);
    }

    public static OsPackageEnumerateFileResult Fail(
        OsPackageFileSystemStatus status,
        OsPackageStatus packageStatus,
        OsPackageHeader header,
        OsPackageFileEntry entry,
        ulong requiredBytes)
    {
        return new OsPackageEnumerateFileResult(
            status,
            packageStatus,
            header,
            entry,
            bytesWritten: 0,
            requiredBytes);
    }
}
