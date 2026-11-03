namespace OS.Storage;

public static partial class OsPackageLoader
{
    public const string PackagePath = "OS/APP.PKG";

    public static OsPackageLoadResult LoadFromPrimaryGptEsp(
        IReadOnlyBlockDevice? disk,
        Span<byte> packageBuffer)
    {
        GptEfiSystemPartitionLocationResult esp = GptParser.LocatePrimaryEfiSystemPartition(disk);
        if (!esp.Succeeded)
        {
            return OsPackageLoadResult.GptFail(esp.Status, esp.BlockStatus);
        }

        PartitionBlockDeviceCreateResult partition = PartitionBlockDevice.Create(
            disk,
            esp.PartitionStartLba,
            esp.PartitionBlockCount);
        if (!partition.Succeeded || partition.Device is null)
        {
            return OsPackageLoadResult.PartitionFail(partition.Status);
        }

        Fat32FileReadResult file = Fat32Parser.ReadFirstLevelFile(
            partition.Device,
            partitionStartLba: 0,
            PackagePath,
            packageBuffer);
        if (!file.Succeeded)
        {
            return OsPackageLoadResult.Fat32Fail(file, checked((uint)packageBuffer.Length));
        }

        OsPackageParseResult parsed = OsPackageParser.Parse(
            packageBuffer.Slice(0, checked((int)file.BytesRead)));
        if (!parsed.Succeeded)
        {
            return OsPackageLoadResult.PackageFail(
                parsed.Status,
                file.BytesRead,
                file.FileSize,
                checked((uint)packageBuffer.Length));
        }

        return OsPackageLoadResult.Success(
            parsed.Header,
            file.BytesRead,
            file.FileSize,
            checked((uint)packageBuffer.Length));
    }

    public static OsPackageLoadResult LoadFromPrimaryGptEspWithCallerBuffers(
        IReadOnlyBlockDevice? disk,
        Span<byte> primaryHeaderSector,
        Span<byte> partitionEntrySectors,
        Span<GptPartitionDescriptor> partitions,
        Span<byte> packageBuffer)
    {
        GptEspLocationReadinessResult esp = LocatePrimaryGptEspWithCallerBuffers(
            disk,
            primaryHeaderSector,
            partitionEntrySectors,
            partitions);
        if (!esp.Succeeded)
        {
            return OsPackageLoadResult.GptFail(esp);
        }

        PartitionBlockDeviceCreateResult partition = PartitionBlockDevice.Create(
            disk,
            esp.PartitionStartLba,
            esp.PartitionBlockCount);
        if (!partition.Succeeded || partition.Device is null)
        {
            return OsPackageLoadResult.PartitionFail(partition.Status, esp);
        }

        Fat32FileReadResult file = Fat32Parser.ReadFirstLevelFile(
            partition.Device,
            partitionStartLba: 0,
            PackagePath,
            packageBuffer);
        if (!file.Succeeded)
        {
            return OsPackageLoadResult.Fat32Fail(file, checked((uint)packageBuffer.Length), esp);
        }

        OsPackageParseResult parsed = OsPackageParser.Parse(
            packageBuffer.Slice(0, checked((int)file.BytesRead)));
        if (!parsed.Succeeded)
        {
            return OsPackageLoadResult.PackageFail(
                parsed.Status,
                file.BytesRead,
                file.FileSize,
                checked((uint)packageBuffer.Length),
                esp);
        }

        return OsPackageLoadResult.Success(
            parsed.Header,
            file.BytesRead,
            file.FileSize,
            checked((uint)packageBuffer.Length),
            esp);
    }

    internal static GptEspLocationReadinessResult LocatePrimaryGptEspWithCallerBuffers(
        IReadOnlyBlockDevice? disk,
        Span<byte> primaryHeaderSector,
        Span<byte> partitionEntrySectors,
        Span<GptPartitionDescriptor> partitions)
    {
        if (disk is null)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.InvalidBlockDevice,
                GptStatus.InvalidBlockDevice);
        }

        uint blockSize = disk.Info.BlockSize;
        if ((ulong)primaryHeaderSector.Length < blockSize)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.HeaderBufferTooSmall,
                GptStatus.PrimaryHeaderShortRead,
                BlockDeviceStatus.DestinationTooSmall,
                headerBytesRequired: blockSize,
                headerBufferBytesAvailable: checked((ulong)primaryHeaderSector.Length),
                partitionEntryBufferBytesAvailable: checked((ulong)partitionEntrySectors.Length));
        }

        GptEspLocationReadinessResult primary = LocateGptEspWithCallerBuffers(
            disk,
            primaryHeaderSector,
            partitionEntrySectors,
            partitions,
            GptPartitionTableReadKind.Primary);
        if (primary.Succeeded)
        {
            return primary;
        }

        GptEspLocationReadinessResult backup = LocateGptEspWithCallerBuffers(
            disk,
            primaryHeaderSector,
            partitionEntrySectors,
            partitions,
            GptPartitionTableReadKind.Backup);
        return backup.Succeeded ? backup : primary;
    }

    private static GptEspLocationReadinessResult LocateGptEspWithCallerBuffers(
        IReadOnlyBlockDevice disk,
        Span<byte> headerSector,
        Span<byte> partitionEntrySectors,
        Span<GptPartitionDescriptor> partitions,
        GptPartitionTableReadKind tableKind)
    {
        GptPartitionTableSource tableSource = ToGptPartitionTableSource(tableKind);
        uint blockSize = disk.Info.BlockSize;
        if (tableKind == GptPartitionTableReadKind.Backup && disk.Info.BlockCount == 0)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.ParserPlanningFailed,
                GptStatus.UnsupportedGeometry,
                BlockDeviceStatus.InvalidBlockCount,
                tableSource: tableSource);
        }

        Span<byte> headerReadBuffer = headerSector.Slice(0, checked((int)blockSize));
        BlockReadResult headerRead = disk.ReadBlocks(
            tableKind == GptPartitionTableReadKind.Primary
                ? GptPartitionTableParser.PrimaryHeaderLba
                : disk.Info.BlockCount - 1,
            1,
            headerReadBuffer);
        if (!headerRead.Succeeded)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.PrimaryHeaderReadFailed,
                GptStatus.PrimaryHeaderReadFailed,
                headerRead.Status,
                tableSource: tableSource);
        }

        if (headerRead.BlocksRead != 1 || headerRead.BytesRead != blockSize)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.PrimaryHeaderShortRead,
                GptStatus.PrimaryHeaderShortRead,
                headerRead.Status,
                tableSource: tableSource);
        }

        GptPartitionTableParseResult planning = ParseGptPartitionTable(
            headerReadBuffer,
            ReadOnlySpan<byte>.Empty,
            disk.Info.BlockCount,
            blockSize,
            Span<GptPartitionDescriptor>.Empty,
            tableKind);
        if (planning.Status != GptPartitionTableStatus.PartitionEntryBufferTooSmall)
        {
            return GptEspLocationReadinessResult.Fail(
                planning.Succeeded
                    ? GptEspLocationReadinessStatus.EfiSystemPartitionNotFound
                    : GptEspLocationReadinessStatus.ParserPlanningFailed,
                planning,
                tableSource: tableSource);
        }

        ulong partitionEntrySectorBytesRequired = planning.PartitionEntrySectorsRequired * blockSize;
        if (partitionEntrySectorBytesRequired > int.MaxValue
            || (ulong)partitionEntrySectors.Length < partitionEntrySectorBytesRequired)
        {
            return FailPartitionEntryBufferTooSmall(
                planning,
                partitionEntrySectorBytesRequired,
                checked((ulong)partitionEntrySectors.Length),
                blockSize,
                blockSize,
                tableSource);
        }

        if (planning.PartitionEntrySectorsRequired > uint.MaxValue)
        {
            return FailPartitionEntryBufferTooSmall(
                planning,
                partitionEntrySectorBytesRequired,
                checked((ulong)partitionEntrySectors.Length),
                blockSize,
                blockSize,
                tableSource);
        }

        Span<byte> partitionEntryReadBuffer = partitionEntrySectors.Slice(
            0,
            checked((int)partitionEntrySectorBytesRequired));
        BlockReadResult partitionEntryRead = disk.ReadBlocks(
            planning.Header.PartitionEntryLba,
            checked((uint)planning.PartitionEntrySectorsRequired),
            partitionEntryReadBuffer);
        if (!partitionEntryRead.Succeeded)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.PartitionEntryReadFailed,
                GptStatus.PartitionEntryReadFailed,
                partitionEntryRead.Status,
                tableSource: tableSource);
        }

        if (partitionEntryRead.BlocksRead != planning.PartitionEntrySectorsRequired
            || partitionEntryRead.BytesRead != partitionEntrySectorBytesRequired)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.PartitionEntryShortRead,
                GptStatus.PartitionEntryShortRead,
                partitionEntryRead.Status,
                tableSource: tableSource);
        }

        GptPartitionTableParseResult parsed = ParseGptPartitionTable(
            headerReadBuffer,
            partitionEntryReadBuffer,
            disk.Info.BlockCount,
            blockSize,
            partitions,
            tableKind);
        if (!parsed.Succeeded)
        {
            return GptEspLocationReadinessResult.Fail(
                GptEspLocationReadinessStatus.ParserFailed,
                parsed,
                tableSource: tableSource);
        }

        GptPartitionSelectorStatus selectorStatus =
            GptPartitionSelector.SelectByPartitionTypeGuid(
                parsed,
                partitions.Slice(0, parsed.PartitionsWritten),
                GptParser.EfiSystemPartitionTypeGuid,
                out GptPartitionSelectorResult selectedPartition);
        if (selectorStatus == GptPartitionSelectorStatus.Success && selectedPartition.Succeeded)
        {
            return GptEspLocationReadinessResult.Success(
                parsed,
                selectedPartition.Descriptor,
                blockSize,
                tableSource);
        }

        if (selectorStatus == GptPartitionSelectorStatus.NotFound)
        {
            return GptEspLocationReadinessResult.FailMissingEsp(
                parsed,
                partitionEntrySectorBytesRequired,
                tableSource);
        }

        return GptEspLocationReadinessResult.Fail(
            GptEspLocationReadinessStatus.ParserFailed,
            parsed,
            tableSource: tableSource);
    }

    private static GptPartitionTableParseResult ParseGptPartitionTable(
        ReadOnlySpan<byte> headerSector,
        ReadOnlySpan<byte> partitionEntrySectors,
        ulong blockCount,
        uint sectorSizeBytes,
        Span<GptPartitionDescriptor> partitions,
        GptPartitionTableReadKind tableKind)
    {
        return tableKind == GptPartitionTableReadKind.Primary
            ? GptPartitionTableParser.ParsePrimary(
                headerSector,
                partitionEntrySectors,
                blockCount,
                sectorSizeBytes,
                partitions)
            : GptPartitionTableParser.ParseBackup(
                headerSector,
                partitionEntrySectors,
                blockCount,
                sectorSizeBytes,
                partitions);
    }

    private static GptPartitionTableSource ToGptPartitionTableSource(GptPartitionTableReadKind tableKind)
    {
        return tableKind == GptPartitionTableReadKind.Primary
            ? GptPartitionTableSource.Primary
            : GptPartitionTableSource.Backup;
    }

    private static GptEspLocationReadinessResult FailPartitionEntryBufferTooSmall(
        GptPartitionTableParseResult planning,
        ulong partitionEntrySectorBytesRequired,
        ulong partitionEntryBufferBytesAvailable,
        ulong headerBytesRequired,
        ulong headerBufferBytesAvailable,
        GptPartitionTableSource tableSource)
    {
        return GptEspLocationReadinessResult.FailPartitionEntryBufferTooSmall(
            planning,
            partitionEntrySectorBytesRequired,
            partitionEntryBufferBytesAvailable,
            headerBytesRequired,
            headerBufferBytesAvailable,
            tableSource);
    }
}
