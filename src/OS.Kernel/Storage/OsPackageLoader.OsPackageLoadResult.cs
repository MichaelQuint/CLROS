namespace OS.Storage;

public readonly struct OsPackageLoadResult
{
    private OsPackageLoadResult(
        OsPackageLoadStatus status,
        GptStatus gptStatus,
        GptPartitionTableStatus gptTableStatus,
        GptPartitionTableCrcStatus gptCrcStatus,
        BlockDeviceStatus blockStatus,
        Fat32Status fat32Status,
        OsPackageStatus packageStatus,
        uint gptPartitionEntryIndex,
        ulong gptPartitionEntryBytesRequired,
        ulong gptPartitionEntrySectorBytesRequired,
        uint expectedGptHeaderCrc32,
        uint computedGptHeaderCrc32,
        uint expectedGptPartitionEntryArrayCrc32,
        uint computedGptPartitionEntryArrayCrc32,
        OsPackageHeader packageHeader,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable,
        ulong gptHeaderBytesRequired = 0,
        ulong gptHeaderBufferBytesAvailable = 0,
        ulong gptPartitionEntryBufferBytesAvailable = 0,
        GptPartitionTableSource gptTableSource = GptPartitionTableSource.Unknown)
    {
        IsInitialized = true;
        Status = status;
        GptStatus = gptStatus;
        GptTableStatus = gptTableStatus;
        GptCrcStatus = gptCrcStatus;
        GptTableSource = gptTableSource;
        BlockStatus = blockStatus;
        Fat32Status = fat32Status;
        PackageStatus = packageStatus;
        GptPartitionEntryIndex = gptPartitionEntryIndex;
        GptPartitionEntryBytesRequired = gptPartitionEntryBytesRequired;
        GptPartitionEntrySectorBytesRequired = gptPartitionEntrySectorBytesRequired;
        GptHeaderBytesRequired = gptHeaderBytesRequired;
        GptHeaderBufferBytesAvailable = gptHeaderBufferBytesAvailable;
        GptPartitionEntryBufferBytesAvailable = gptPartitionEntryBufferBytesAvailable;
        ExpectedGptHeaderCrc32 = expectedGptHeaderCrc32;
        ComputedGptHeaderCrc32 = computedGptHeaderCrc32;
        ExpectedGptPartitionEntryArrayCrc32 = expectedGptPartitionEntryArrayCrc32;
        ComputedGptPartitionEntryArrayCrc32 = computedGptPartitionEntryArrayCrc32;
        PackageHeader = packageHeader;
        BytesRead = bytesRead;
        PackageBytesRequired = packageBytesRequired;
        PackageBufferBytesAvailable = packageBufferBytesAvailable;
    }

    public bool IsInitialized { get; }

    public OsPackageLoadStatus Status { get; }

    public GptStatus GptStatus { get; }

    public GptPartitionTableStatus GptTableStatus { get; }

    public GptPartitionTableCrcStatus GptCrcStatus { get; }

    public GptPartitionTableSource GptTableSource { get; }

    public BlockDeviceStatus BlockStatus { get; }

    public Fat32Status Fat32Status { get; }

    public OsPackageStatus PackageStatus { get; }

    public uint GptPartitionEntryIndex { get; }

    public ulong GptPartitionEntryBytesRequired { get; }

    public ulong GptPartitionEntrySectorBytesRequired { get; }

    public ulong GptHeaderBytesRequired { get; }

    public ulong GptHeaderBufferBytesAvailable { get; }

    public ulong GptPartitionEntryBufferBytesAvailable { get; }

    public uint ExpectedGptHeaderCrc32 { get; }

    public uint ComputedGptHeaderCrc32 { get; }

    public uint ExpectedGptPartitionEntryArrayCrc32 { get; }

    public uint ComputedGptPartitionEntryArrayCrc32 { get; }

    public OsPackageHeader PackageHeader { get; }

    public uint BytesRead { get; }

    public uint PackageBytesRequired { get; }

    public uint PackageBufferBytesAvailable { get; }

    public bool Succeeded => IsInitialized && Status == OsPackageLoadStatus.Success;

    public static OsPackageLoadResult Success(OsPackageHeader packageHeader, uint bytesRead)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Success,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            packageHeader,
            bytesRead,
            bytesRead,
            bytesRead);
    }

    public static OsPackageLoadResult Success(
        OsPackageHeader packageHeader,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Success,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            packageHeader,
            bytesRead,
            packageBytesRequired,
            packageBufferBytesAvailable);
    }

    public static OsPackageLoadResult Success(
        OsPackageHeader packageHeader,
        uint bytesRead,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Success,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            packageHeader,
            bytesRead,
            bytesRead,
            bytesRead,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult Success(
        OsPackageHeader packageHeader,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Success,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            packageHeader,
            bytesRead,
            packageBytesRequired,
            packageBufferBytesAvailable,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult GptFail(
        GptStatus gptStatus,
        BlockDeviceStatus blockStatus)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.GptFailed,
            gptStatus,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            blockStatus,
            Fat32Status.Success,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            0,
            0,
            0);
    }

    public static OsPackageLoadResult GptFail(GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.GptFailed,
            gpt.GptStatus,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            gpt.BlockStatus,
            Fat32Status.Success,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            0,
            0,
            0,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult PartitionFail(BlockDeviceStatus blockStatus)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PartitionDeviceFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            blockStatus,
            Fat32Status.Success,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            0,
            0,
            0);
    }

    public static OsPackageLoadResult PartitionFail(
        BlockDeviceStatus blockStatus,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PartitionDeviceFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            blockStatus,
            Fat32Status.Success,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            0,
            0,
            0,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult Fat32Fail(
        Fat32Status fat32Status,
        BlockDeviceStatus blockStatus)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Fat32ReadFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            blockStatus,
            fat32Status,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            0,
            0,
            0);
    }

    public static OsPackageLoadResult Fat32Fail(
        Fat32FileReadResult file,
        uint packageBufferBytesAvailable)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Fat32ReadFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            file.BlockStatus,
            file.Status,
            OsPackageStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            file.BytesRead,
            file.FileSize,
            packageBufferBytesAvailable);
    }

    public static OsPackageLoadResult Fat32Fail(
        Fat32Status fat32Status,
        BlockDeviceStatus blockStatus,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Fat32ReadFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            blockStatus,
            fat32Status,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            0,
            0,
            0,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult Fat32Fail(
        Fat32FileReadResult file,
        uint packageBufferBytesAvailable,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.Fat32ReadFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            file.BlockStatus,
            file.Status,
            OsPackageStatus.Success,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            file.BytesRead,
            file.FileSize,
            packageBufferBytesAvailable,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult PackageFail(
        OsPackageStatus packageStatus,
        uint bytesRead)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PackageParseFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            packageStatus,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            bytesRead,
            bytesRead,
            bytesRead);
    }

    public static OsPackageLoadResult PackageFail(
        OsPackageStatus packageStatus,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PackageParseFailed,
            GptStatus.Success,
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.NotValidated,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            packageStatus,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            default,
            bytesRead,
            packageBytesRequired,
            packageBufferBytesAvailable);
    }

    public static OsPackageLoadResult PackageFail(
        OsPackageStatus packageStatus,
        uint bytesRead,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PackageParseFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            packageStatus,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            bytesRead,
            bytesRead,
            bytesRead,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }

    public static OsPackageLoadResult PackageFail(
        OsPackageStatus packageStatus,
        uint bytesRead,
        uint packageBytesRequired,
        uint packageBufferBytesAvailable,
        GptEspLocationReadinessResult gpt)
    {
        return new OsPackageLoadResult(
            OsPackageLoadStatus.PackageParseFailed,
            GptStatus.Success,
            gpt.GptTableStatus,
            gpt.GptCrcStatus,
            BlockDeviceStatus.Success,
            Fat32Status.Success,
            packageStatus,
            gpt.PartitionEntryIndex,
            gpt.PartitionEntryBytesRequired,
            gpt.PartitionEntrySectorBytesRequired,
            gpt.ExpectedHeaderCrc32,
            gpt.ComputedHeaderCrc32,
            gpt.ExpectedPartitionEntryArrayCrc32,
            gpt.ComputedPartitionEntryArrayCrc32,
            default,
            bytesRead,
            packageBytesRequired,
            packageBufferBytesAvailable,
            gptHeaderBytesRequired: gpt.HeaderBytesRequired,
            gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable,
            gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable,
            gptTableSource: gpt.TableSource);
    }
}
