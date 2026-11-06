using OS.Std.Memory;

namespace OS.Storage;

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
