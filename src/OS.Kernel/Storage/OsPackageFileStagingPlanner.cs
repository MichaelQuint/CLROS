using OS.Std.Memory;

namespace OS.Storage;

public static class OsPackageFileStagingPlanner
{
    public const bool RequiresCallerOwnedPackageBytes = true;
    public const bool RequiresOpenedPackageFile = true;
    public const bool WritesCallerOwnedDestinationOnly = true;
    public const bool ReadsLiveStorage = false;
    public const bool CallsPalFileApis = false;
    public const bool AllocatesStagingMemory = false;
    public const bool MapsExecutablePages = false;
    public const bool ExecutesAppCode = false;

    public static OsPackageFileStagingResult StageOpenedFile(
        ReadOnlySpan<byte> packageBytes,
        in OsPackageOpenFileResult openedFile,
        Span<byte> destination)
    {
        if (!openedFile.Succeeded)
        {
            return OsPackageFileStagingResult.Create(
                OsPackageFileStagingStatus.OpenFileFailed,
                openedFile.Status,
                openedFile.PackageStatus,
                openedFile.Entry.Index,
                openedFile.Entry.ContentOffset,
                openedFile.Entry.ContentLength,
                bytesWritten: 0,
                requiredBytes: openedFile.Entry.ContentLength,
                copyAttempted: false);
        }

        OsPackageFileEntry entry = openedFile.Entry;
        if (entry.ContentLength == 0)
        {
            return OsPackageFileStagingResult.Create(
                OsPackageFileStagingStatus.EmptyFile,
                openedFile.Status,
                openedFile.PackageStatus,
                entry.Index,
                entry.ContentOffset,
                entry.ContentLength,
                bytesWritten: 0,
                requiredBytes: 1,
                copyAttempted: false);
        }

        if (!TryGetPackageRange(
            packageBytes.Length,
            entry.ContentOffset,
            entry.ContentLength,
            out int sourceOffset,
            out int sourceLength,
            out bool sourceRangeOverflow))
        {
            return OsPackageFileStagingResult.Create(
                OsPackageFileStagingStatus.PackageRangeInvalid,
                openedFile.Status,
                openedFile.PackageStatus,
                entry.Index,
                entry.ContentOffset,
                entry.ContentLength,
                bytesWritten: 0,
                requiredBytes: entry.ContentLength,
                copyAttempted: false,
                sourceRangeOverflow: sourceRangeOverflow);
        }

        if ((ulong)destination.Length < entry.ContentLength)
        {
            return OsPackageFileStagingResult.Create(
                OsPackageFileStagingStatus.DestinationTooSmall,
                openedFile.Status,
                openedFile.PackageStatus,
                entry.Index,
                entry.ContentOffset,
                entry.ContentLength,
                bytesWritten: 0,
                requiredBytes: entry.ContentLength,
                copyAttempted: false);
        }

        ReadOnlySpan<byte> source = packageBytes.Slice(sourceOffset, sourceLength);
        BufferOperationResult copy = BufferOperations.MemoryCopy(source, destination);
        if (!copy.Succeeded)
        {
            return OsPackageFileStagingResult.Create(
                OsPackageFileStagingStatus.DestinationTooSmall,
                openedFile.Status,
                openedFile.PackageStatus,
                entry.Index,
                entry.ContentOffset,
                entry.ContentLength,
                bytesWritten: 0,
                requiredBytes: entry.ContentLength,
                copyAttempted: true);
        }

        return OsPackageFileStagingResult.Create(
            OsPackageFileStagingStatus.Success,
            openedFile.Status,
            openedFile.PackageStatus,
            entry.Index,
            entry.ContentOffset,
            entry.ContentLength,
            bytesWritten: entry.ContentLength,
            requiredBytes: entry.ContentLength,
            copyAttempted: true);
    }

    private static bool TryGetPackageRange(
        int packageLength,
        ulong offset,
        ulong length,
        out int sourceOffset,
        out int sourceLength,
        out bool sourceRangeOverflow)
    {
        sourceOffset = 0;
        sourceLength = 0;
        sourceRangeOverflow = false;

        if (!TryAdd(offset, length, out ulong end))
        {
            sourceRangeOverflow = true;
            return false;
        }

        if (offset > int.MaxValue ||
            length > int.MaxValue ||
            end > (ulong)packageLength)
        {
            return false;
        }

        sourceOffset = (int)offset;
        sourceLength = (int)length;
        return true;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > ulong.MaxValue - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }
}
