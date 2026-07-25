namespace OS.Pal;

public sealed class OSHostPackageFileFacade
{
    private readonly OSHostPackageFileTable? table;

    public OSHostPackageFileFacade(OSHostPackageFileTable? table)
    {
        this.table = table;
    }

    public OSHostPackageFileOpenResult OpenPackageFile(
        OSHostPackagePath path,
        ReadOnlySpan<byte> pathBytes)
    {
        if (!TryGetTable(out OSHostPackageFileTable activeTable)
            || path.Flags != 0
            || path.PathUtf8.Length != (ulong)pathBytes.Length)
        {
            return OSHostPackageFileOpenResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return activeTable.Open(pathBytes);
    }

    public OSHostPackageFileReadResult ReadFileAt(
        OSHostHandle file,
        ulong fileOffset,
        OSHostByteSpan destination,
        Span<byte> destinationBytes)
    {
        if (!TryGetTable(out OSHostPackageFileTable activeTable))
        {
            return OSHostPackageFileReadResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return activeTable.ReadAt(
            file,
            fileOffset,
            destination.Length,
            destinationBytes);
    }

    public OSHostStatus GetFileInfo(OSHostHandle file, out OSHostFileInfo info)
    {
        info = default;
        return TryGetTable(out OSHostPackageFileTable activeTable)
            ? activeTable.GetInfo(file, out info)
            : OSHostStatus.InvalidArgument;
    }

    public OSHostStatus CloseFile(OSHostHandle file)
    {
        return TryGetTable(out OSHostPackageFileTable activeTable)
            ? activeTable.Close(file)
            : OSHostStatus.InvalidArgument;
    }

    public OSHostPackageFileEnumerationResult EnumeratePackage(
        uint fileIndex,
        OSHostByteSpan pathDestination,
        Span<byte> pathDestinationBytes)
    {
        if (!TryGetTable(out OSHostPackageFileTable activeTable)
            || pathDestination.Length != (ulong)pathDestinationBytes.Length)
        {
            return OSHostPackageFileEnumerationResult.FromStatus(
                OSHostStatus.InvalidArgument);
        }

        return activeTable.Enumerate(fileIndex, pathDestinationBytes);
    }

    private bool TryGetTable(out OSHostPackageFileTable activeTable)
    {
        if (table is null)
        {
            activeTable = null!;
            return false;
        }

        activeTable = table;
        return true;
    }
}
