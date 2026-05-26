using OS.Std.Memory;

namespace OS.Storage;

public enum OsPackageFileSystemStatus
{
    Success = 0,
    PackageParseFailed = 1,
    InvalidPath = 2,
    MissingFile = 3,
    DestinationTooSmall = 4,
    InvalidReadRange = 5,
    FileIndexOutOfRange = 6
}

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

    public bool IsInitialized { get; }

    public OsPackageFileSystemStatus Status { get; }

    public OsPackageStatus PackageStatus { get; }

    public OsPackageHeader Header { get; }

    public OsPackageFileEntry Entry { get; }

    public ulong BytesRead { get; }

    public ulong RequiredBytes { get; }

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

public static class OsPackageFileSystem
{
    public static OsPackageOpenFileResult OpenFile(
        ReadOnlySpan<byte> package,
        ReadOnlySpan<byte> path)
    {
        OsPackageFileLookupResult lookup = OsPackageParser.FindFile(package, path);
        if (!lookup.Succeeded)
        {
            return OsPackageOpenFileResult.Fail(MapPackageStatus(lookup.Status), lookup.Status);
        }

        return OsPackageOpenFileResult.Success(lookup.Header, lookup.Entry);
    }

    public static OsPackageEnumerateFileResult EnumerateFile(
        ReadOnlySpan<byte> package,
        uint fileIndex,
        Span<byte> pathDestination)
    {
        OsPackageFilePathCopyResult copy = OsPackageParser.CopyFilePathByIndex(
            package,
            fileIndex,
            pathDestination);
        if (!copy.Succeeded)
        {
            return OsPackageEnumerateFileResult.Fail(
                MapPackageStatus(copy.Status),
                copy.Status,
                copy.Header,
                copy.Entry,
                copy.RequiredBytes);
        }

        return OsPackageEnumerateFileResult.Success(
            copy.Header,
            copy.Entry,
            copy.BytesWritten);
    }

    public static OsPackageReadFileResult ReadFile(
        ReadOnlySpan<byte> package,
        ReadOnlySpan<byte> path,
        Span<byte> destination)
    {
        OsPackageFileContentResult content = OsPackageParser.GetFileContent(package, path);
        if (!content.Succeeded)
        {
            return OsPackageReadFileResult.Fail(
                MapPackageStatus(content.Status),
                content.Status,
                content.Header,
                content.Entry,
                bytesRead: 0,
                requiredBytes: 0);
        }

        if ((ulong)destination.Length < content.Entry.ContentLength)
        {
            return OsPackageReadFileResult.Fail(
                OsPackageFileSystemStatus.DestinationTooSmall,
                OsPackageStatus.Success,
                content.Header,
                content.Entry,
                bytesRead: 0,
                requiredBytes: content.Entry.ContentLength);
        }

        BufferOperationResult copy = BufferOperations.MemoryCopy(content.Content, destination);
        if (!copy.Succeeded)
        {
            return OsPackageReadFileResult.Fail(
                OsPackageFileSystemStatus.DestinationTooSmall,
                OsPackageStatus.Success,
                content.Header,
                content.Entry,
                bytesRead: 0,
                requiredBytes: content.Entry.ContentLength);
        }

        return OsPackageReadFileResult.Success(
            content.Header,
            content.Entry,
            content.Entry.ContentLength);
    }

    public static OsPackageReadFileResult ReadFileRange(
        ReadOnlySpan<byte> package,
        ReadOnlySpan<byte> path,
        ulong fileOffset,
        ulong byteCount,
        Span<byte> destination)
    {
        OsPackageFileContentResult content = OsPackageParser.GetFileContent(package, path);
        if (!content.Succeeded)
        {
            return OsPackageReadFileResult.Fail(
                MapPackageStatus(content.Status),
                content.Status,
                content.Header,
                content.Entry,
                bytesRead: 0,
                requiredBytes: 0);
        }

        if (fileOffset > content.Entry.ContentLength
            || byteCount > content.Entry.ContentLength - fileOffset
            || fileOffset > int.MaxValue
            || byteCount > int.MaxValue)
        {
            return OsPackageReadFileResult.Fail(
                OsPackageFileSystemStatus.InvalidReadRange,
                OsPackageStatus.Success,
                content.Header,
                content.Entry,
                bytesRead: 0,
                requiredBytes: byteCount);
        }

        if ((ulong)destination.Length < byteCount)
        {
            return OsPackageReadFileResult.Fail(
                OsPackageFileSystemStatus.DestinationTooSmall,
                OsPackageStatus.Success,
                content.Header,
                content.Entry,
                bytesRead: 0,
                requiredBytes: byteCount);
        }

        ReadOnlySpan<byte> source = content.Content.Slice((int)fileOffset, (int)byteCount);
        BufferOperationResult copy = BufferOperations.MemoryCopy(source, destination);
        if (!copy.Succeeded)
        {
            return OsPackageReadFileResult.Fail(
                OsPackageFileSystemStatus.DestinationTooSmall,
                OsPackageStatus.Success,
                content.Header,
                content.Entry,
                bytesRead: 0,
                requiredBytes: byteCount);
        }

        return OsPackageReadFileResult.Success(content.Header, content.Entry, byteCount);
    }

    private static OsPackageFileSystemStatus MapPackageStatus(OsPackageStatus status)
    {
        return status switch
        {
            OsPackageStatus.Success => OsPackageFileSystemStatus.Success,
            OsPackageStatus.InvalidPath => OsPackageFileSystemStatus.InvalidPath,
            OsPackageStatus.MissingFile => OsPackageFileSystemStatus.MissingFile,
            OsPackageStatus.FileIndexOutOfRange => OsPackageFileSystemStatus.FileIndexOutOfRange,
            OsPackageStatus.DestinationTooSmall => OsPackageFileSystemStatus.DestinationTooSmall,
            _ => OsPackageFileSystemStatus.PackageParseFailed
        };
    }
}
