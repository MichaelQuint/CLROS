using OS.Std.Binary;
using OS.Std.Memory;
using OS.Std.Text;

namespace OS.Storage;

public static partial class Fat32Parser
{
    public const uint MaximumSupportedSectorSize = 4096;
    public const byte DirectoryAttribute = 0x10;

    /// <summary>Maximum clusters per directory walk in convenience calls.
    /// Caller-scratch overloads use the supplied history length instead.</summary>
    public const int DefaultDirectoryClusterBudget = 4096;

    private const uint Fat32ClusterMask = 0x0FFFFFFF;
    private const uint Fat32EndOfChain = 0x0FFFFFF8;
    private const uint Fat32BadCluster = 0x0FFFFFF7;
    private const int BootSectorSignatureOffset = 510;
    private const byte LongFileNameAttribute = 0x0F;
    private const byte LongFileNameLastEntryFlag = 0x40;
    private const byte LongFileNameOrdinalMask = 0x1F;
    private const int LongFileNameEntryCharacterCount = 13;
    private const int LongFileNameMaxCharacters = 255;
    private const byte VolumeIdAttribute = 0x08;
    private const int DirectoryEntryLength = 32;

    public static Fat32BpbParseResult ReadBpb(IReadOnlyBlockDevice? device, ulong partitionStartLba)
    {
        if (device is null)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidBlockDevice);
        }

        BlockDeviceInfo info = device.Info;
        if (!IsDeviceGeometrySupported(info) || partitionStartLba >= info.BlockCount)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.UnsupportedGeometry);
        }

        int blockSize = checked((int)info.BlockSize);
        byte[] bootSector = new byte[blockSize];
        BlockReadResult bootRead = device.ReadBlocks(partitionStartLba, 1, bootSector);
        if (!bootRead.Succeeded)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.BootSectorReadFailed, bootRead.Status);
        }

        if (bootRead.BlocksRead != 1 || bootRead.BytesRead != info.BlockSize)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.BootSectorShortRead, bootRead.Status);
        }

        return ParseBpb(bootSector, info, partitionStartLba);
    }

    public static Fat32DirectoryReadResult ReadRootDirectoryEntries(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        Span<Fat32DirectoryEntry> entries)
    {
        Span<uint> history = stackalloc uint[DefaultDirectoryClusterBudget];
        return ReadRootDirectoryEntries(device, partitionStartLba, entries, history);
    }

    /// <summary>Synchronously enumerate with exclusive caller-owned cluster
    /// history. Its length bounds the directory walk; contents are overwritten
    /// and may be reused after return. Output exhaustion is not continuation;
    /// retry starts a new walk. Empty/exhausted history fails with
    /// DirectoryBudgetExceeded before reading another directory cluster.</summary>
    public static Fat32DirectoryReadResult ReadRootDirectoryEntries(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        Span<Fat32DirectoryEntry> entries,
        Span<uint> directoryHistory)
    {
        if (device is null)
        {
            return Fat32DirectoryReadResult.Fail(Fat32Status.InvalidBlockDevice);
        }

        Fat32BpbParseResult bpbResult = ReadBpb(device, partitionStartLba);
        if (!bpbResult.Succeeded)
        {
            return Fat32DirectoryReadResult.Fail(bpbResult.Status, bpbResult.BlockStatus);
        }

        return ParseDirectoryEntriesByChain(
            device,
            bpbResult.Bpb,
            bpbResult.Bpb.RootCluster,
            entries,
            Fat32Status.RootDirectoryReadFailed,
            Fat32Status.TruncatedDirectoryEntry,
            directoryHistory);
    }

    public static Fat32FileLookupResult FindRootDirectoryEntry(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? name)
    {
        Span<uint> history = stackalloc uint[DefaultDirectoryClusterBudget];
        return FindRootDirectoryEntry(device, partitionStartLba, name, history);
    }

    /// <summary>Synchronously find one root entry using exclusive reusable
    /// caller scratch. History length limits visited directory clusters;
    /// exhaustion returns DirectoryBudgetExceeded, never a missing entry.</summary>
    public static Fat32FileLookupResult FindRootDirectoryEntry(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? name,
        Span<uint> directoryHistory)
    {
        if (!IsSupportedLookupName(name))
        {
            return Fat32FileLookupResult.Fail(Fat32Status.UnsupportedLongNamePath);
        }

        if (device is null)
        {
            return Fat32FileLookupResult.Fail(Fat32Status.InvalidBlockDevice);
        }

        Fat32BpbParseResult bpbResult = ReadBpb(device, partitionStartLba);
        if (!bpbResult.Succeeded)
        {
            return Fat32FileLookupResult.Fail(bpbResult.Status, bpbResult.BlockStatus);
        }

        return FindDirectoryEntryInChain(
            device,
            bpbResult.Bpb,
            bpbResult.Bpb.RootCluster,
            name,
            Fat32Status.MissingFile,
            Fat32Status.RootDirectoryReadFailed,
            Fat32Status.TruncatedDirectoryEntry,
            directoryHistory);
    }

    public static Fat32FileLookupResult FindRootDirectory(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? directoryName)
    {
        Span<uint> history = stackalloc uint[DefaultDirectoryClusterBudget];
        return FindRootDirectory(device, partitionStartLba, directoryName, history);
    }

    /// <summary>Find a root directory with the same exclusive reusable
    /// history and cluster budget as FindRootDirectoryEntry.</summary>
    public static Fat32FileLookupResult FindRootDirectory(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? directoryName,
        Span<uint> directoryHistory)
    {
        Fat32FileLookupResult lookup = FindRootDirectoryEntry(device, partitionStartLba, directoryName, directoryHistory);
        if (!lookup.Succeeded)
        {
            return lookup.Status == Fat32Status.MissingFile
                ? Fat32FileLookupResult.Fail(Fat32Status.MissingDirectory, lookup.BlockStatus, lookup.Bpb)
                : lookup;
        }

        if (!lookup.Entry.IsDirectory)
        {
            return Fat32FileLookupResult.Fail(Fat32Status.PathEntryIsNotDirectory, lookup.Bpb);
        }

        return lookup;
    }

    public static Fat32FileLookupResult FindFirstLevelDirectoryEntry(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? path)
    {
        Span<uint> history = stackalloc uint[DefaultDirectoryClusterBudget];
        return FindFirstLevelDirectoryEntry(device, partitionStartLba, path, history);
    }

    /// <summary>Find a first-level entry with exclusive reusable caller
    /// history. Root and child walks each use its length as their cluster
    /// budget; the same scratch is reused after the root walk completes.
    /// Nested paths remain unsupported.</summary>
    public static Fat32FileLookupResult FindFirstLevelDirectoryEntry(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? path,
        Span<uint> directoryHistory)
    {
        if (!TrySplitSingleLevelPath(path, out string directoryName, out string entryName))
        {
            return Fat32FileLookupResult.Fail(Fat32Status.UnsupportedNestedPath);
        }

        Fat32FileLookupResult directoryLookup = FindRootDirectory(device, partitionStartLba, directoryName, directoryHistory);
        if (!directoryLookup.Succeeded)
        {
            return directoryLookup;
        }

        return FindDirectoryEntryInChain(
            device!,
            directoryLookup.Bpb,
            directoryLookup.Entry.FirstCluster,
            entryName,
            Fat32Status.MissingFile,
            Fat32Status.DirectoryClusterReadFailed,
            Fat32Status.DirectoryClusterShortRead,
            directoryHistory);
    }

    private static Fat32FileLookupResult FindDirectoryEntryInChain(
        IReadOnlyBlockDevice device,
        Fat32Bpb bpb,
        uint startCluster,
        string? name,
        Fat32Status missingStatus,
        Fat32Status clusterReadFailedStatus,
        Fat32Status clusterShortReadStatus,
        Span<uint> visitedClusters)
    {
        Span<byte> requestedName = stackalloc byte[11];
        bool hasShortName = TryFormatShortName(name, requestedName);
        if (!hasShortName && !IsSupportedRootLookupName(name))
        {
            return Fat32FileLookupResult.Fail(Fat32Status.UnsupportedLongNamePath, bpb);
        }

        Span<char> longName = stackalloc char[LongFileNameMaxCharacters + LongFileNameEntryCharacterCount];
        int longNameLength = 0;
        int expectedLongFileNameOrdinal = 0;
        byte longFileNameChecksum = 0;
        bool hasLongFileNameSequence = false;

        if (!IsDirectoryGeometrySupported(bpb))
        {
            return Fat32FileLookupResult.Fail(Fat32Status.UnsupportedGeometry, bpb);
        }

        Span<byte> fatBlock = stackalloc byte[bpb.BytesPerSector];
        uint currentCluster = startCluster;
        int visitedClusterCount = 0;

        while (true)
        {
            Fat32Status visit = VisitDirectoryCluster(bpb, currentCluster, visitedClusters, ref visitedClusterCount);
            if (visit != Fat32Status.Success)
            {
                return Fat32FileLookupResult.Fail(visit, bpb);
            }

            DirectoryClusterRead read = ReadDirectoryCluster(
                device,
                bpb,
                currentCluster,
                clusterReadFailedStatus,
                clusterShortReadStatus);
            if (!read.Succeeded)
            {
                return Fat32FileLookupResult.Fail(read.Status, read.BlockStatus, bpb);
            }

            ReadOnlySpan<byte> directory = read.Bytes;
            for (int offset = 0; offset < directory.Length; offset += DirectoryEntryLength)
            {
                if (offset + DirectoryEntryLength > directory.Length)
                {
                    return Fat32FileLookupResult.Fail(Fat32Status.TruncatedDirectoryEntry, bpb);
                }

                ReadOnlySpan<byte> entryBytes = directory.Slice(offset, DirectoryEntryLength);
                byte firstByte = entryBytes[0];
                if (firstByte == 0x00)
                {
                    if (hasLongFileNameSequence)
                    {
                        return Fat32FileLookupResult.Fail(Fat32Status.MalformedLongFileNameMetadata, bpb);
                    }

                    return Fat32FileLookupResult.Fail(missingStatus, bpb);
                }

                if (IsLongFileNameEntry(entryBytes))
                {
                    if (!TryAppendLongFileNameEntry(
                        entryBytes,
                        longName,
                        ref longNameLength,
                        ref expectedLongFileNameOrdinal,
                        ref longFileNameChecksum,
                        ref hasLongFileNameSequence))
                    {
                        return Fat32FileLookupResult.Fail(Fat32Status.MalformedLongFileNameMetadata, bpb);
                    }

                    continue;
                }

                if (firstByte == 0xE5 || IsVolumeIdEntry(entryBytes))
                {
                    if (hasLongFileNameSequence)
                    {
                        return Fat32FileLookupResult.Fail(Fat32Status.MalformedLongFileNameMetadata, bpb);
                    }

                    continue;
                }

                if (hasLongFileNameSequence)
                {
                    if (expectedLongFileNameOrdinal != 0
                        || longFileNameChecksum != ComputeLongFileNameChecksum(entryBytes[..11]))
                    {
                        return Fat32FileLookupResult.Fail(Fat32Status.MalformedLongFileNameMetadata, bpb);
                    }

                    if (LongNamesEqual(longName[..longNameLength], name))
                    {
                        if (!TryParseDirectoryEntry(entryBytes, out Fat32DirectoryEntry entry))
                        {
                            return Fat32FileLookupResult.Fail(Fat32Status.TruncatedDirectoryEntry, bpb);
                        }

                        return Fat32FileLookupResult.Success(bpb, entry);
                    }

                    ResetLongFileNameState(
                        longName,
                        ref longNameLength,
                        ref expectedLongFileNameOrdinal,
                        ref longFileNameChecksum,
                        ref hasLongFileNameSequence);
                }

                if (hasShortName && ShortNamesEqual(entryBytes[..11], requestedName))
                {
                    if (!TryParseDirectoryEntry(entryBytes, out Fat32DirectoryEntry entry))
                    {
                        return Fat32FileLookupResult.Fail(Fat32Status.TruncatedDirectoryEntry, bpb);
                    }

                    return Fat32FileLookupResult.Success(bpb, entry);
                }
            }

            Fat32Status nextStatus = MoveToNextDirectoryCluster(
                device,
                bpb,
                currentCluster,
                visitedClusters,
                visitedClusterCount,
                fatBlock,
                out uint nextCluster,
                out BlockDeviceStatus blockStatus);
            if (nextStatus == Fat32Status.Success)
            {
                currentCluster = nextCluster;
                continue;
            }

            return nextStatus == missingStatus
                ? Fat32FileLookupResult.Fail(missingStatus, bpb)
                : Fat32FileLookupResult.Fail(nextStatus, blockStatus, bpb);
        }
    }

    public static Fat32FileReadResult ReadRootFile(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? name,
        Span<byte> destination)
    {
        Span<uint> history = stackalloc uint[DefaultDirectoryClusterBudget];
        return ReadRootFile(device, partitionStartLba, name, destination, history);
    }

    /// <summary>Read a whole root file, bounding its directory lookup with
    /// exclusive reusable caller history. File-content chain scratch remains
    /// separately bounded by the file's required cluster count.</summary>
    public static Fat32FileReadResult ReadRootFile(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? name,
        Span<byte> destination,
        Span<uint> directoryHistory)
    {
        Fat32FileLookupResult lookup = FindRootDirectoryEntry(device, partitionStartLba, name, directoryHistory);
        if (!lookup.Succeeded)
        {
            return Fat32FileReadResult.Fail(lookup.Status, lookup.BlockStatus, lookup.Bpb);
        }

        Fat32DirectoryEntry entry = lookup.Entry;
        if (entry.IsDirectory)
        {
            return Fat32FileReadResult.Fail(Fat32Status.DirectoryReadAsFile, lookup.Bpb, entry);
        }

        if ((ulong)destination.Length < entry.FileSize)
        {
            return Fat32FileReadResult.Fail(Fat32Status.FileDestinationTooSmall, lookup.Bpb, entry);
        }

        return ReadFileByFatChain(device!, lookup.Bpb, entry, destination);
    }

    public static Fat32FileReadResult ReadFirstLevelFile(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? path,
        Span<byte> destination)
    {
        Span<uint> history = stackalloc uint[DefaultDirectoryClusterBudget];
        return ReadFirstLevelFile(device, partitionStartLba, path, destination, history);
    }

    /// <summary>Read a whole first-level file using one exclusive reusable
    /// history buffer for the independent root and child directory walks.
    /// Each walk is bounded by its length; file-content scratch is separate.</summary>
    public static Fat32FileReadResult ReadFirstLevelFile(
        IReadOnlyBlockDevice? device,
        ulong partitionStartLba,
        string? path,
        Span<byte> destination,
        Span<uint> directoryHistory)
    {
        Fat32FileLookupResult lookup = FindFirstLevelDirectoryEntry(device, partitionStartLba, path, directoryHistory);
        if (!lookup.Succeeded)
        {
            return Fat32FileReadResult.Fail(lookup.Status, lookup.BlockStatus, lookup.Bpb);
        }

        Fat32DirectoryEntry entry = lookup.Entry;
        if (entry.IsDirectory)
        {
            return Fat32FileReadResult.Fail(Fat32Status.DirectoryReadAsFile, lookup.Bpb, entry);
        }

        if ((ulong)destination.Length < entry.FileSize)
        {
            return Fat32FileReadResult.Fail(Fat32Status.FileDestinationTooSmall, lookup.Bpb, entry);
        }

        return ReadFileByFatChain(device!, lookup.Bpb, entry, destination);
    }

    private static Fat32FileReadResult ReadFileByFatChain(
        IReadOnlyBlockDevice device,
        Fat32Bpb bpb,
        Fat32DirectoryEntry entry,
        Span<byte> destination)
    {
        if (entry.FileSize == 0)
        {
            return Fat32FileReadResult.Success(bpb, entry, 0);
        }

        if (!IsValidDataCluster(bpb, entry.FirstCluster))
        {
            return Fat32FileReadResult.Fail(Fat32Status.InvalidFileCluster, bpb, entry);
        }

        ulong clustersNeeded = DivideRoundUp(entry.FileSize, bpb.ClusterSizeBytes);
        if (clustersNeeded == 0)
        {
            return Fat32FileReadResult.Success(bpb, entry, 0);
        }

        if (clustersNeeded > bpb.ClusterCount || clustersNeeded > int.MaxValue)
        {
            return Fat32FileReadResult.Fail(Fat32Status.FileClusterChainTooShort, bpb, entry);
        }

        int clusterWalkLimit = checked((int)clustersNeeded);
        Span<uint> visitedClusters = clusterWalkLimit <= 256
            ? stackalloc uint[clusterWalkLimit]
            : new uint[clusterWalkLimit];
        Span<byte> fatBlock = stackalloc byte[bpb.BytesPerSector];

        int blockSize = bpb.BytesPerSector;
        int bytesRemaining = (int)entry.FileSize;
        int destinationOffset = 0;
        int visitedClusterCount = 0;
        uint currentCluster = entry.FirstCluster;
        bool assumedContiguousClusters = true;

        while (bytesRemaining != 0)
        {
            if (!IsValidDataCluster(bpb, currentCluster))
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.InvalidFileNextCluster,
                    BlockDeviceStatus.Success,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            if (ContainsCluster(visitedClusters[..visitedClusterCount], currentCluster))
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.FileClusterChainLoop,
                    BlockDeviceStatus.Success,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            visitedClusters[visitedClusterCount] = currentCluster;
            visitedClusterCount++;

            if (!TryGetClusterFirstLba(bpb, currentCluster, out ulong clusterFirstLba))
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.InvalidFileNextCluster,
                    BlockDeviceStatus.Success,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            int bytesFromCluster = bytesRemaining > (int)bpb.ClusterSizeBytes
                ? (int)bpb.ClusterSizeBytes
                : bytesRemaining;
            Fat32FileReadResult clusterRead = ReadClusterContent(
                device,
                bpb,
                entry,
                destination,
                clusterFirstLba,
                bytesFromCluster,
                destinationOffset,
                assumedContiguousClusters);
            if (!clusterRead.Succeeded)
            {
                return clusterRead;
            }

            destinationOffset += bytesFromCluster;
            bytesRemaining -= bytesFromCluster;
            if (bytesRemaining == 0)
            {
                return Fat32FileReadResult.Success(
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            FatEntryRead fatRead = ReadFatEntry(device, bpb, currentCluster, fatBlock);
            if (!fatRead.Succeeded)
            {
                return Fat32FileReadResult.Fail(
                    fatRead.Status,
                    fatRead.BlockStatus,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            if (IsEndOfChain(fatRead.NextCluster))
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.FileClusterChainTooShort,
                    BlockDeviceStatus.Success,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            if (!IsValidDataCluster(bpb, fatRead.NextCluster))
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.InvalidFileNextCluster,
                    BlockDeviceStatus.Success,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            if (fatRead.NextCluster != currentCluster + 1)
            {
                assumedContiguousClusters = false;
            }

            currentCluster = fatRead.NextCluster;
        }

        return Fat32FileReadResult.Success(
            bpb,
            entry,
            (uint)destinationOffset,
            assumedContiguousClusters);
    }

    private static Fat32FileReadResult ReadClusterContent(
        IReadOnlyBlockDevice device,
        Fat32Bpb bpb,
        Fat32DirectoryEntry entry,
        Span<byte> destination,
        ulong clusterFirstLba,
        int bytesToRead,
        int destinationOffset,
        bool assumedContiguousClusters)
    {
        int blockSize = bpb.BytesPerSector;
        int bytesRemaining = bytesToRead;
        ulong nextLba = clusterFirstLba;

        while (bytesRemaining >= blockSize)
        {
            Span<byte> blockDestination = destination.Slice(destinationOffset, blockSize);
            BlockReadResult read = device.ReadBlocks(nextLba, 1, blockDestination);
            if (!read.Succeeded)
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.FileContentReadFailed,
                    read.Status,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            if (read.BlocksRead != 1 || read.BytesRead != (ulong)blockSize)
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.FileContentShortRead,
                    read.Status,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            destinationOffset += blockSize;
            bytesRemaining -= blockSize;
            nextLba++;
        }

        if (bytesRemaining != 0)
        {
            Span<byte> finalBlock = stackalloc byte[blockSize];
            BlockReadResult read = device.ReadBlocks(nextLba, 1, finalBlock);
            if (!read.Succeeded)
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.FileContentReadFailed,
                    read.Status,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            if (read.BlocksRead != 1 || read.BytesRead != (ulong)blockSize)
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.FileContentShortRead,
                    read.Status,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }

            BufferOperationResult copy = BufferOperations.MemoryCopy(
                finalBlock[..bytesRemaining],
                destination.Slice(destinationOffset, bytesRemaining));
            if (!copy.Succeeded)
            {
                return Fat32FileReadResult.Fail(
                    Fat32Status.FileDestinationTooSmall,
                    BlockDeviceStatus.Success,
                    bpb,
                    entry,
                    (uint)destinationOffset,
                    assumedContiguousClusters);
            }
        }

        return Fat32FileReadResult.Success(
            bpb,
            entry,
            (uint)(destinationOffset + bytesRemaining),
            assumedContiguousClusters);
    }

    private static FatEntryRead ReadFatEntry(
        IReadOnlyBlockDevice device,
        Fat32Bpb bpb,
        uint cluster,
        Span<byte> fatBlock)
    {
        ulong fatByteOffset = (ulong)cluster * 4UL;
        ulong fatSectorOffset = fatByteOffset / (uint)bpb.BytesPerSector;
        int fatEntryOffset = (int)(fatByteOffset % (uint)bpb.BytesPerSector);
        if (fatSectorOffset >= bpb.FatSizeSectors || fatEntryOffset > bpb.BytesPerSector - 4)
        {
            return FatEntryRead.Fail(Fat32Status.InvalidFileNextCluster);
        }

        if (!TryAdd(bpb.FirstFatLba, fatSectorOffset, out ulong fatEntryLba))
        {
            return FatEntryRead.Fail(Fat32Status.InvalidFileNextCluster);
        }

        BlockReadResult read = device.ReadBlocks(fatEntryLba, 1, fatBlock);
        if (!read.Succeeded)
        {
            return FatEntryRead.Fail(Fat32Status.FileFatReadFailed, read.Status);
        }

        if (read.BlocksRead != 1 || read.BytesRead != (ulong)bpb.BytesPerSector)
        {
            return FatEntryRead.Fail(Fat32Status.FileFatShortRead, read.Status);
        }

        if (!TryReadUInt32(fatBlock, fatEntryOffset, out uint rawEntry))
        {
            return FatEntryRead.Fail(Fat32Status.FileFatShortRead);
        }

        return FatEntryRead.Success(rawEntry & Fat32ClusterMask);
    }

    private static bool ContainsCluster(ReadOnlySpan<uint> clusters, uint cluster)
    {
        for (int i = 0; i < clusters.Length; i++)
        {
            if (clusters[i] == cluster)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEndOfChain(uint cluster)
    {
        return cluster >= Fat32EndOfChain;
    }

    private static bool IsValidDataCluster(Fat32Bpb bpb, uint cluster)
    {
        return cluster >= 2
            && cluster < Fat32BadCluster
            && !IsEndOfChain(cluster)
            && (ulong)(cluster - 2) < bpb.ClusterCount;
    }

    private static bool TryAppendLongFileNameEntry(
        ReadOnlySpan<byte> entryBytes,
        Span<char> longName,
        ref int longNameLength,
        ref int expectedOrdinal,
        ref byte checksum,
        ref bool hasSequence)
    {
        byte order = entryBytes[0];
        bool isLast = (order & LongFileNameLastEntryFlag) != 0;
        int ordinal = order & LongFileNameOrdinalMask;
        if (ordinal == 0
            || (order & 0xA0) != 0
            || entryBytes[12] != 0
            || !TryReadUInt16(entryBytes, 26, out ushort firstCluster)
            || firstCluster != 0)
        {
            return false;
        }

        if (isLast)
        {
            if (hasSequence)
            {
                return false;
            }

            hasSequence = true;
            expectedOrdinal = ordinal - 1;
            checksum = entryBytes[13];
            longNameLength = 0;
            ClearLongNameBuffer(longName);
        }
        else
        {
            if (!hasSequence || ordinal != expectedOrdinal || entryBytes[13] != checksum)
            {
                return false;
            }

            expectedOrdinal--;
        }

        int destinationOffset = (ordinal - 1) * LongFileNameEntryCharacterCount;
        if (destinationOffset + LongFileNameEntryCharacterCount > longName.Length
            || destinationOffset >= LongFileNameMaxCharacters)
        {
            return false;
        }

        return TryCopyLongFileNameChars(entryBytes, longName, destinationOffset, ref longNameLength);
    }

    private static bool TryCopyLongFileNameChars(
        ReadOnlySpan<byte> entryBytes,
        Span<char> longName,
        int destinationOffset,
        ref int longNameLength)
    {
        int sourceIndex = 0;
        return TryCopyLongFileNameCharRange(entryBytes, 1, 5, longName, destinationOffset, ref sourceIndex, ref longNameLength)
            && TryCopyLongFileNameCharRange(entryBytes, 14, 6, longName, destinationOffset, ref sourceIndex, ref longNameLength)
            && TryCopyLongFileNameCharRange(entryBytes, 28, 2, longName, destinationOffset, ref sourceIndex, ref longNameLength);
    }

    private static bool TryCopyLongFileNameCharRange(
        ReadOnlySpan<byte> entryBytes,
        int entryOffset,
        int count,
        Span<char> longName,
        int destinationOffset,
        ref int sourceIndex,
        ref int longNameLength)
    {
        for (int i = 0; i < count; i++)
        {
            if (!TryReadUInt16(entryBytes, entryOffset + i * 2, out ushort value))
            {
                return false;
            }

            int writeIndex = destinationOffset + sourceIndex;
            sourceIndex++;

            if (value == 0 || value == 0xFFFF)
            {
                continue;
            }

            if (writeIndex >= LongFileNameMaxCharacters)
            {
                return false;
            }

            longName[writeIndex] = (char)value;
            if (writeIndex + 1 > longNameLength)
            {
                longNameLength = writeIndex + 1;
            }
        }

        return true;
    }

    private static void ResetLongFileNameState(
        Span<char> longName,
        ref int longNameLength,
        ref int expectedOrdinal,
        ref byte checksum,
        ref bool hasSequence)
    {
        ClearLongNameBuffer(longName);
        longNameLength = 0;
        expectedOrdinal = 0;
        checksum = 0;
        hasSequence = false;
    }

    private static void ClearLongNameBuffer(Span<char> longName)
    {
        for (int i = 0; i < longName.Length; i++)
        {
            longName[i] = '\0';
        }
    }

    private static bool LongNamesEqual(ReadOnlySpan<char> actual, string? expected)
    {
        if (expected is null || actual.Length != expected.Length)
        {
            return false;
        }

        for (int i = 0; i < actual.Length; i++)
        {
            if (OS.Std.Ascii.ToUpperInvariant(actual[i]) != OS.Std.Ascii.ToUpperInvariant(expected[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static byte ComputeLongFileNameChecksum(ReadOnlySpan<byte> shortName)
    {
        byte sum = 0;
        for (int i = 0; i < shortName.Length; i++)
        {
            sum = (byte)(((sum & 1) == 1 ? 0x80 : 0) + (sum >> 1) + shortName[i]);
        }

        return sum;
    }

    private static Fat32BpbParseResult ParseBpb(
        ReadOnlySpan<byte> bootSector,
        BlockDeviceInfo info,
        ulong partitionStartLba)
    {
        if (bootSector.Length < BlockDeviceInfo.MinimumBlockSizeBytes)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.UnsupportedSectorSize);
        }

        if (bootSector[BootSectorSignatureOffset] != 0x55
            || bootSector[BootSectorSignatureOffset + 1] != 0xAA)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidBootSectorSignature);
        }

        if (!TryReadUInt16(bootSector, 11, out ushort bytesPerSector)
            || !TryReadUInt16(bootSector, 14, out ushort reservedSectorCount)
            || !TryReadUInt16(bootSector, 17, out ushort rootEntryCount)
            || !TryReadUInt16(bootSector, 19, out ushort totalSectors16)
            || !TryReadUInt16(bootSector, 22, out ushort fatSize16)
            || !TryReadUInt32(bootSector, 32, out uint totalSectors32)
            || !TryReadUInt32(bootSector, 36, out uint fatSize32)
            || !TryReadUInt16(bootSector, 40, out ushort extendedFlags)
            || !TryReadUInt16(bootSector, 42, out ushort fileSystemVersion)
            || !TryReadUInt32(bootSector, 44, out uint rootCluster))
        {
            return Fat32BpbParseResult.Fail(Fat32Status.CorruptBpb);
        }

        // This reader supports mirrored FAT0 at version zero only. Do not
        // silently read stale FAT0 when the volume selects another copy.
        if (extendedFlags != 0 || fileSystemVersion != 0)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.UnsupportedGeometry);
        }

        byte sectorsPerCluster = bootSector[13];
        byte fatCount = bootSector[16];

        if (bytesPerSector != info.BlockSize
            || !IsSupportedSectorSize(bytesPerSector))
        {
            return Fat32BpbParseResult.Fail(Fat32Status.UnsupportedSectorSize);
        }

        if (!IsValidSectorsPerCluster(sectorsPerCluster))
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidSectorsPerCluster);
        }

        if (reservedSectorCount == 0)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidReservedSectorCount);
        }

        if (fatCount == 0 || fatCount > 2)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidFatCount);
        }

        if (rootEntryCount != 0 || fatSize16 != 0)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.CorruptBpb);
        }

        if (fatSize32 == 0)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidFatSize);
        }

        if (rootCluster < 2)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidRootCluster);
        }

        if (totalSectors16 != 0 && totalSectors32 != 0)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.CorruptBpb);
        }

        uint totalSectors = totalSectors16 != 0 ? totalSectors16 : totalSectors32;
        if (totalSectors == 0)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidTotalSectors);
        }

        ulong fatRegionSectors = (ulong)fatCount * fatSize32;
        ulong firstDataSector = reservedSectorCount + fatRegionSectors;
        if (firstDataSector >= totalSectors)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidTotalSectors);
        }

        ulong dataSectors = totalSectors - firstDataSector;
        ulong clusterCount = dataSectors / sectorsPerCluster;
        if (clusterCount == 0 || clusterCount > uint.MaxValue)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidTotalSectors);
        }

        if ((ulong)(rootCluster - 2) >= clusterCount)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.InvalidRootCluster);
        }

        if (!TryAdd(partitionStartLba, totalSectors, out ulong partitionEndLba)
            || partitionEndLba > info.BlockCount)
        {
            return Fat32BpbParseResult.Fail(Fat32Status.UnsupportedGeometry);
        }

        if (!TryAdd(partitionStartLba, reservedSectorCount, out ulong firstFatLba)
            || !TryAdd(partitionStartLba, firstDataSector, out ulong firstDataLba))
        {
            return Fat32BpbParseResult.Fail(Fat32Status.UnsupportedGeometry);
        }

        ulong rootClusterOffset = (ulong)(rootCluster - 2) * sectorsPerCluster;
        if (!TryAdd(firstDataLba, rootClusterOffset, out ulong rootDirectoryFirstLba))
        {
            return Fat32BpbParseResult.Fail(Fat32Status.UnsupportedGeometry);
        }

        Fat32Bpb bpb = new Fat32Bpb(
            partitionStartLba,
            bytesPerSector,
            sectorsPerCluster,
            reservedSectorCount,
            fatCount,
            fatSize32,
            rootCluster,
            totalSectors,
            firstFatLba,
            firstDataLba,
            rootDirectoryFirstLba,
            (uint)clusterCount);
        return Fat32BpbParseResult.Success(bpb);
    }

    private static Fat32DirectoryReadResult ParseRootDirectoryEntries(
        ReadOnlySpan<byte> directory,
        Fat32Bpb bpb,
        Span<Fat32DirectoryEntry> entries)
    {
        int entriesRead = 0;
        for (int offset = 0; offset < directory.Length; offset += DirectoryEntryLength)
        {
            if (offset + DirectoryEntryLength > directory.Length)
            {
                return Fat32DirectoryReadResult.Fail(Fat32Status.TruncatedDirectoryEntry, bpb);
            }

            ReadOnlySpan<byte> entryBytes = directory.Slice(offset, DirectoryEntryLength);
            byte firstByte = entryBytes[0];
            if (firstByte == 0x00)
            {
                return Fat32DirectoryReadResult.Success(bpb, entriesRead, reachedEndMarker: true);
            }

            if (firstByte == 0xE5 || IsLongFileNameEntry(entryBytes) || IsVolumeIdEntry(entryBytes))
            {
                continue;
            }

            if (entriesRead >= entries.Length)
            {
                return Fat32DirectoryReadResult.Fail(Fat32Status.DirectoryEntryBufferTooSmall, bpb);
            }

            if (!TryParseDirectoryEntry(entryBytes, out Fat32DirectoryEntry entry))
            {
                return Fat32DirectoryReadResult.Fail(Fat32Status.TruncatedDirectoryEntry, bpb);
            }

            entries[entriesRead] = entry;
            entriesRead++;
        }

        return Fat32DirectoryReadResult.Success(bpb, entriesRead, reachedEndMarker: false);
    }

    private static Fat32DirectoryReadResult ParseDirectoryEntriesByChain(
        IReadOnlyBlockDevice device,
        Fat32Bpb bpb,
        uint startCluster,
        Span<Fat32DirectoryEntry> entries,
        Fat32Status clusterReadFailedStatus,
        Fat32Status clusterShortReadStatus,
        Span<uint> visitedClusters)
    {
        if (!IsDirectoryGeometrySupported(bpb))
        {
            return Fat32DirectoryReadResult.Fail(Fat32Status.UnsupportedGeometry, bpb);
        }

        Span<byte> fatBlock = stackalloc byte[bpb.BytesPerSector];
        uint currentCluster = startCluster;
        int visitedClusterCount = 0;
        int entriesRead = 0;

        while (true)
        {
            Fat32Status visit = VisitDirectoryCluster(bpb, currentCluster, visitedClusters, ref visitedClusterCount);
            if (visit != Fat32Status.Success)
            {
                return Fat32DirectoryReadResult.Fail(visit, bpb);
            }

            DirectoryClusterRead read = ReadDirectoryCluster(
                device,
                bpb,
                currentCluster,
                clusterReadFailedStatus,
                clusterShortReadStatus);
            if (!read.Succeeded)
            {
                return Fat32DirectoryReadResult.Fail(read.Status, read.BlockStatus, bpb);
            }

            ReadOnlySpan<byte> directory = read.Bytes;
            for (int offset = 0; offset < directory.Length; offset += DirectoryEntryLength)
            {
                if (offset + DirectoryEntryLength > directory.Length)
                {
                    return Fat32DirectoryReadResult.Fail(Fat32Status.TruncatedDirectoryEntry, bpb);
                }

                ReadOnlySpan<byte> entryBytes = directory.Slice(offset, DirectoryEntryLength);
                byte firstByte = entryBytes[0];
                if (firstByte == 0x00)
                {
                    return Fat32DirectoryReadResult.Success(bpb, entriesRead, reachedEndMarker: true);
                }

                if (firstByte == 0xE5 || IsLongFileNameEntry(entryBytes) || IsVolumeIdEntry(entryBytes))
                {
                    continue;
                }

                if (entriesRead >= entries.Length)
                {
                    return Fat32DirectoryReadResult.Fail(Fat32Status.DirectoryEntryBufferTooSmall, bpb);
                }

                if (!TryParseDirectoryEntry(entryBytes, out Fat32DirectoryEntry entry))
                {
                    return Fat32DirectoryReadResult.Fail(Fat32Status.TruncatedDirectoryEntry, bpb);
                }

                entries[entriesRead] = entry;
                entriesRead++;
            }

            Fat32Status nextStatus = MoveToNextDirectoryCluster(
                device,
                bpb,
                currentCluster,
                visitedClusters,
                visitedClusterCount,
                fatBlock,
                out uint nextCluster,
                out BlockDeviceStatus blockStatus);
            if (nextStatus == Fat32Status.Success)
            {
                currentCluster = nextCluster;
                continue;
            }

            return nextStatus == Fat32Status.MissingFile
                ? Fat32DirectoryReadResult.Success(bpb, entriesRead, reachedEndMarker: false)
                : Fat32DirectoryReadResult.Fail(nextStatus, blockStatus, bpb);
        }
    }

    private static DirectoryClusterRead ReadRootDirectoryCluster(IReadOnlyBlockDevice device, Fat32Bpb bpb)
    {
        return ReadDirectoryCluster(
            device,
            bpb,
            bpb.RootCluster,
            Fat32Status.RootDirectoryReadFailed,
            Fat32Status.TruncatedDirectoryEntry);
    }

    private static DirectoryClusterRead ReadDirectoryCluster(
        IReadOnlyBlockDevice device,
        Fat32Bpb bpb,
        uint cluster)
    {
        return ReadDirectoryCluster(
            device,
            bpb,
            cluster,
            Fat32Status.DirectoryClusterReadFailed,
            Fat32Status.DirectoryClusterShortRead);
    }

    private static DirectoryClusterRead ReadDirectoryCluster(
        IReadOnlyBlockDevice device,
        Fat32Bpb bpb,
        uint cluster,
        Fat32Status clusterReadFailedStatus,
        Fat32Status clusterShortReadStatus)
    {
        if (bpb.ClusterSizeBytes > int.MaxValue || bpb.SectorsPerCluster == 0)
        {
            return DirectoryClusterRead.Fail(Fat32Status.UnsupportedGeometry);
        }

        if (!TryGetClusterFirstLba(bpb, cluster, out ulong firstLba))
        {
            return DirectoryClusterRead.Fail(Fat32Status.InvalidDirectoryCluster);
        }

        byte[] directory = new byte[checked((int)bpb.ClusterSizeBytes)];
        BlockReadResult read = device.ReadBlocks(
            firstLba,
            bpb.SectorsPerCluster,
            directory);
        if (!read.Succeeded)
        {
            return DirectoryClusterRead.Fail(clusterReadFailedStatus, read.Status);
        }

        if (read.BlocksRead != bpb.SectorsPerCluster || read.BytesRead != bpb.ClusterSizeBytes)
        {
            return DirectoryClusterRead.Fail(clusterShortReadStatus, read.Status);
        }

        return DirectoryClusterRead.Success(directory);
    }

    private static bool IsDirectoryGeometrySupported(Fat32Bpb bpb)
    {
        return bpb.ClusterSizeBytes <= int.MaxValue
            && bpb.SectorsPerCluster != 0
            && bpb.ClusterCount != 0;
    }

    private static Fat32Status VisitDirectoryCluster(
        Fat32Bpb bpb,
        uint cluster,
        Span<uint> history,
        ref int count)
    {
        if (!IsValidDataCluster(bpb, cluster))
            return Fat32Status.InvalidDirectoryCluster;
        if (ContainsCluster(history[..count], cluster))
            return Fat32Status.DirectoryClusterChainLoop;
        if (count >= history.Length)
            return Fat32Status.DirectoryBudgetExceeded;
        history[count++] = cluster;
        return Fat32Status.Success;
    }

    private static Fat32Status MoveToNextDirectoryCluster(
        IReadOnlyBlockDevice device,
        Fat32Bpb bpb,
        uint currentCluster,
        Span<uint> visitedClusters,
        int visitedClusterCount,
        Span<byte> fatBlock,
        out uint nextCluster,
        out BlockDeviceStatus blockStatus)
    {
        nextCluster = 0;
        blockStatus = BlockDeviceStatus.Success;

        if (!IsValidDataCluster(bpb, currentCluster))
        {
            return Fat32Status.InvalidDirectoryCluster;
        }

        FatEntryRead fatRead = ReadFatEntry(device, bpb, currentCluster, fatBlock);
        blockStatus = fatRead.BlockStatus;
        if (!fatRead.Succeeded)
        {
            return MapDirectoryFatStatus(fatRead.Status);
        }

        if (IsEndOfChain(fatRead.NextCluster))
        {
            return Fat32Status.MissingFile;
        }

        if (!IsValidDataCluster(bpb, fatRead.NextCluster))
        {
            return Fat32Status.InvalidDirectoryNextCluster;
        }

        if (ContainsCluster(visitedClusters[..visitedClusterCount], fatRead.NextCluster))
        {
            return Fat32Status.DirectoryClusterChainLoop;
        }

        if (visitedClusterCount >= visitedClusters.Length)
        {
            return Fat32Status.DirectoryBudgetExceeded;
        }

        nextCluster = fatRead.NextCluster;
        return Fat32Status.Success;
    }

    private static Fat32Status MapDirectoryFatStatus(Fat32Status status)
    {
        return status switch
        {
            Fat32Status.FileFatReadFailed => Fat32Status.DirectoryFatReadFailed,
            Fat32Status.FileFatShortRead => Fat32Status.DirectoryFatShortRead,
            Fat32Status.InvalidFileNextCluster => Fat32Status.InvalidDirectoryNextCluster,
            _ => status
        };
    }

    private static bool TryParseDirectoryEntry(
        ReadOnlySpan<byte> entryBytes,
        out Fat32DirectoryEntry entry)
    {
        entry = default;
        if (!TryReadUInt16(entryBytes, 20, out ushort highCluster)
            || !TryReadUInt16(entryBytes, 26, out ushort lowCluster)
            || !TryReadUInt32(entryBytes, 28, out uint fileSize))
        {
            return false;
        }

        uint firstCluster = ((uint)highCluster << 16) | lowCluster;
        entry = new Fat32DirectoryEntry(
            ReadShortName(entryBytes[..11]),
            entryBytes[11],
            firstCluster,
            fileSize);
        return true;
    }

    private static bool IsSupportedLookupName(string? value)
    {
        Span<byte> requestedName = stackalloc byte[11];
        return TryFormatShortName(value, requestedName) || IsSupportedRootLookupName(value);
    }

    private static bool TrySplitSingleLevelPath(
        string? value,
        out string directoryName,
        out string entryName)
    {
        directoryName = string.Empty;
        entryName = string.Empty;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        int startIndex = IsPathSeparator(value[0]) ? 1 : 0;
        if (startIndex >= value.Length)
        {
            return false;
        }

        int separatorIndex = -1;
        for (int i = startIndex; i < value.Length; i++)
        {
            if (!IsPathSeparator(value[i]))
            {
                continue;
            }

            if (separatorIndex >= 0)
            {
                return false;
            }

            separatorIndex = i;
        }

        if (separatorIndex <= startIndex || separatorIndex == value.Length - 1)
        {
            return false;
        }

        directoryName = value.Substring(startIndex, separatorIndex - startIndex);
        entryName = value.Substring(separatorIndex + 1);
        return IsSupportedLookupName(directoryName) && IsSupportedLookupName(entryName);
    }

    private static bool IsPathSeparator(char value)
    {
        return value == '/' || value == '\\';
    }

    private static bool TryFormatShortName(string? value, Span<byte> destination)
    {
        if (string.IsNullOrEmpty(value) || destination.Length < 11)
        {
            return false;
        }

        for (int i = 0; i < 11; i++)
        {
            destination[i] = 0x20;
        }

        int dotIndex = -1;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '/' || c == '\\')
            {
                return false;
            }

            if (c == '.')
            {
                if (dotIndex >= 0)
                {
                    return false;
                }

                dotIndex = i;
            }
        }

        int baseLength = dotIndex >= 0 ? dotIndex : value.Length;
        int extensionLength = dotIndex >= 0 ? value.Length - dotIndex - 1 : 0;
        if (baseLength == 0 || baseLength > 8 || extensionLength > 3)
        {
            return false;
        }

        for (int i = 0; i < baseLength; i++)
        {
            if (!TryToShortNameByte(value[i], out byte b))
            {
                return false;
            }

            destination[i] = b;
        }

        for (int i = 0; i < extensionLength; i++)
        {
            if (!TryToShortNameByte(value[dotIndex + 1 + i], out byte b))
            {
                return false;
            }

            destination[8 + i] = b;
        }

        return true;
    }

    private static bool IsSupportedRootLookupName(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > LongFileNameMaxCharacters)
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '/' || c == '\\' || c < 0x20)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryToShortNameByte(char c, out byte value)
    {
        value = 0;
        if (c >= 'a' && c <= 'z')
        {
            value = (byte)(c - ('a' - 'A'));
            return true;
        }

        if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
        {
            value = (byte)c;
            return true;
        }

        switch (c)
        {
            case '$':
            case '%':
            case '\'':
            case '-':
            case '_':
            case '@':
            case '~':
            case '`':
            case '!':
            case '(':
            case ')':
            case '{':
            case '}':
            case '^':
            case '#':
            case '&':
                value = (byte)c;
                return true;
            default:
                return false;
        }
    }

    private static string ReadShortName(ReadOnlySpan<byte> bytes)
    {
        int baseLength = AsciiString.TrimTrailingSpace(bytes[..8]).Length;
        int extensionLength = AsciiString.TrimTrailingSpace(bytes.Slice(8, 3)).Length;
        Span<char> chars = stackalloc char[12];
        int index = 0;

        for (int i = 0; i < baseLength; i++)
        {
            chars[index] = (char)bytes[i];
            index++;
        }

        if (extensionLength != 0)
        {
            chars[index] = '.';
            index++;

            for (int i = 0; i < extensionLength; i++)
            {
                chars[index] = (char)bytes[8 + i];
                index++;
            }
        }

        return new string(chars[..index]);
    }

    private static bool ShortNamesEqual(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected)
    {
        return OS.Std.MemoryExtensions.SequenceEqual(actual, expected);
    }

    private static bool IsLongFileNameEntry(ReadOnlySpan<byte> entryBytes)
    {
        return (entryBytes[11] & LongFileNameAttribute) == LongFileNameAttribute;
    }

    private static bool IsVolumeIdEntry(ReadOnlySpan<byte> entryBytes)
    {
        return (entryBytes[11] & VolumeIdAttribute) != 0;
    }

    private static bool IsDeviceGeometrySupported(BlockDeviceInfo info)
    {
        return info.BlockSize >= BlockDeviceInfo.MinimumBlockSizeBytes
            && info.BlockSize <= MaximumSupportedSectorSize
            && (info.BlockSize % BlockDeviceInfo.MinimumBlockSizeBytes) == 0
            && info.BlockCount != 0
            && info.CapacityBytes != 0;
    }

    private static bool IsSupportedSectorSize(ushort bytesPerSector)
    {
        return bytesPerSector >= BlockDeviceInfo.MinimumBlockSizeBytes
            && bytesPerSector <= MaximumSupportedSectorSize
            && IsPowerOfTwo(bytesPerSector);
    }

    private static bool IsValidSectorsPerCluster(byte sectorsPerCluster)
    {
        return sectorsPerCluster != 0
            && sectorsPerCluster <= 128
            && IsPowerOfTwo(sectorsPerCluster);
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool TryReadUInt16(ReadOnlySpan<byte> bytes, int offset, out ushort value)
    {
        if (offset < 0 || bytes.Length - offset < 2)
        {
            value = 0;
            return false;
        }

        return EndianPrimitives.TryReadUInt16LittleEndian(bytes.Slice(offset, 2), out value);
    }

    private static bool TryReadUInt32(ReadOnlySpan<byte> bytes, int offset, out uint value)
    {
        if (offset < 0 || bytes.Length - offset < 4)
        {
            value = 0;
            return false;
        }

        return EndianPrimitives.TryReadUInt32LittleEndian(bytes.Slice(offset, 4), out value);
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

    private static bool TryMultiply(ulong left, ulong right, out ulong value)
    {
        if (left != 0 && right > ulong.MaxValue / left)
        {
            value = 0;
            return false;
        }

        value = left * right;
        return true;
    }

    private static bool TryGetClusterFirstLba(Fat32Bpb bpb, uint cluster, out ulong firstLba)
    {
        firstLba = 0;
        if (cluster < 2)
        {
            return false;
        }

        ulong clusterIndex = cluster - 2UL;
        if (clusterIndex >= bpb.ClusterCount)
        {
            return false;
        }

        if (!TryMultiply(clusterIndex, bpb.SectorsPerCluster, out ulong sectorOffset)
            || !TryAdd(bpb.FirstDataLba, sectorOffset, out firstLba))
        {
            return false;
        }

        if (!TryAdd(firstLba, bpb.SectorsPerCluster, out ulong exclusiveEndLba)
            || !TryAdd(bpb.PartitionStartLba, bpb.TotalSectors, out ulong partitionEndLba)
            || exclusiveEndLba > partitionEndLba)
        {
            firstLba = 0;
            return false;
        }

        return true;
    }

    private static ulong DivideRoundUp(ulong value, ulong divisor)
    {
        return value == 0 ? 0 : ((value - 1) / divisor) + 1;
    }
}
