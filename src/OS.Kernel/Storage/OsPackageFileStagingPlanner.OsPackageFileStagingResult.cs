using OS.Std.Memory;

namespace OS.Storage;

public readonly struct OsPackageFileStagingResult
{
    private OsPackageFileStagingResult(
        OsPackageFileStagingStatus status,
        OsPackageFileSystemStatus openStatus,
        OsPackageStatus packageStatus,
        uint fileIndex,
        ulong packageOffset,
        ulong fileLength,
        ulong bytesWritten,
        ulong requiredBytes,
        bool copyAttempted,
        bool sourceRangeOverflow)
    {
        IsInitialized = true;
        Status = status;
        OpenStatus = openStatus;
        PackageStatus = packageStatus;
        FileIndex = fileIndex;
        PackageOffset = packageOffset;
        FileLength = fileLength;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        CopyAttempted = copyAttempted;
        SourceRangeOverflow = sourceRangeOverflow;
    }

    public bool IsInitialized { get; }

    public OsPackageFileStagingStatus Status { get; }

    public OsPackageFileSystemStatus OpenStatus { get; }

    public OsPackageStatus PackageStatus { get; }

    public uint FileIndex { get; }

    public ulong PackageOffset { get; }

    public ulong FileLength { get; }

    public ulong BytesWritten { get; }

    public ulong RequiredBytes { get; }

    public bool CopyAttempted { get; }

    public bool SourceRangeOverflow { get; }

    public bool Succeeded => IsInitialized &&
        Status == OsPackageFileStagingStatus.Success &&
        BytesWritten != 0 &&
        BytesWritten == RequiredBytes &&
        !SourceRangeOverflow;

    public static OsPackageFileStagingResult Create(
        OsPackageFileStagingStatus status,
        OsPackageFileSystemStatus openStatus,
        OsPackageStatus packageStatus,
        uint fileIndex,
        ulong packageOffset,
        ulong fileLength,
        ulong bytesWritten,
        ulong requiredBytes,
        bool copyAttempted,
        bool sourceRangeOverflow = false)
    {
        return new OsPackageFileStagingResult(
            status,
            openStatus,
            packageStatus,
            fileIndex,
            packageOffset,
            fileLength,
            bytesWritten,
            requiredBytes,
            copyAttempted,
            sourceRangeOverflow);
    }
}
