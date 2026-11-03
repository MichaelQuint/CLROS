using OS.Storage;

namespace OS.Manifest;

public readonly struct NativeManifestLoadResult
{
    private NativeManifestLoadResult(NativeManifestLoadStatus status, bool isInitialized, GptStatus gptStatus, GptPartitionTableStatus gptTableStatus, GptPartitionTableCrcStatus gptCrcStatus, BlockDeviceStatus blockStatus, Fat32Status fat32Status, NativeManifestParseStatus manifestStatus, uint gptPartitionEntryIndex, ulong gptPartitionEntryBytesRequired, ulong gptPartitionEntrySectorBytesRequired, uint expectedGptHeaderCrc32, uint computedGptHeaderCrc32, uint expectedGptPartitionEntryArrayCrc32, uint computedGptPartitionEntryArrayCrc32, NativeManifest manifest, uint bytesRead, uint manifestBytesRequired, uint manifestBufferBytesAvailable, ulong gptHeaderBytesRequired = 0, ulong gptHeaderBufferBytesAvailable = 0, ulong gptPartitionEntryBufferBytesAvailable = 0, GptPartitionTableSource gptTableSource = GptPartitionTableSource.Unknown)
    {
        Status = status;
        IsInitialized = isInitialized;
        GptStatus = gptStatus;
        GptTableStatus = gptTableStatus;
        GptCrcStatus = gptCrcStatus;
        GptTableSource = gptTableSource;
        BlockStatus = blockStatus;
        Fat32Status = fat32Status;
        ManifestStatus = manifestStatus;
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
        Manifest = manifest;
        BytesRead = bytesRead;
        ManifestBytesRequired = manifestBytesRequired;
        ManifestBufferBytesAvailable = manifestBufferBytesAvailable;
    }

    public NativeManifestLoadStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public GptStatus GptStatus
    {
        get;
    }
    public GptPartitionTableStatus GptTableStatus
    {
        get;
    }
    public GptPartitionTableCrcStatus GptCrcStatus
    {
        get;
    }
    public GptPartitionTableSource GptTableSource
    {
        get;
    }
    public BlockDeviceStatus BlockStatus
    {
        get;
    }
    public Fat32Status Fat32Status
    {
        get;
    }
    public NativeManifestParseStatus ManifestStatus
    {
        get;
    }
    public uint GptPartitionEntryIndex
    {
        get;
    }
    public ulong GptPartitionEntryBytesRequired
    {
        get;
    }
    public ulong GptPartitionEntrySectorBytesRequired
    {
        get;
    }
    public ulong GptHeaderBytesRequired
    {
        get;
    }
    public ulong GptHeaderBufferBytesAvailable
    {
        get;
    }
    public ulong GptPartitionEntryBufferBytesAvailable
    {
        get;
    }
    public uint ExpectedGptHeaderCrc32
    {
        get;
    }
    public uint ComputedGptHeaderCrc32
    {
        get;
    }
    public uint ExpectedGptPartitionEntryArrayCrc32
    {
        get;
    }
    public uint ComputedGptPartitionEntryArrayCrc32
    {
        get;
    }
    public NativeManifest Manifest
    {
        get;
    }
    public uint BytesRead
    {
        get;
    }
    public uint ManifestBytesRequired
    {
        get;
    }
    public uint ManifestBufferBytesAvailable
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeManifestLoadStatus.Success;

    public static NativeManifestLoadResult Success(NativeManifest manifest, uint bytesRead)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.Success, isInitialized: true, GptStatus.Success, GptPartitionTableStatus.Success, GptPartitionTableCrcStatus.NotValidated, BlockDeviceStatus.Success, Fat32Status.Success, NativeManifestParseStatus.Success, 0, 0, 0, 0, 0, 0, 0, manifest, bytesRead, bytesRead, bytesRead);
    }

    public static NativeManifestLoadResult Success(NativeManifest manifest, uint bytesRead, uint manifestBytesRequired, uint manifestBufferBytesAvailable)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.Success, isInitialized: true, GptStatus.Success, GptPartitionTableStatus.Success, GptPartitionTableCrcStatus.NotValidated, BlockDeviceStatus.Success, Fat32Status.Success, NativeManifestParseStatus.Success, 0, 0, 0, 0, 0, 0, 0, manifest, bytesRead, manifestBytesRequired, manifestBufferBytesAvailable);
    }

    public static NativeManifestLoadResult Success(NativeManifest manifest, uint bytesRead, GptEspLocationReadinessResult gpt)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.Success, isInitialized: true, GptStatus.Success, gpt.GptTableStatus, gpt.GptCrcStatus, BlockDeviceStatus.Success, Fat32Status.Success, NativeManifestParseStatus.Success, gpt.PartitionEntryIndex, gpt.PartitionEntryBytesRequired, gpt.PartitionEntrySectorBytesRequired, gpt.ExpectedHeaderCrc32, gpt.ComputedHeaderCrc32, gpt.ExpectedPartitionEntryArrayCrc32, gpt.ComputedPartitionEntryArrayCrc32, manifest, bytesRead, bytesRead, bytesRead, gptHeaderBytesRequired: gpt.HeaderBytesRequired, gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable, gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable, gptTableSource: gpt.TableSource);
    }

    public static NativeManifestLoadResult Success(NativeManifest manifest, uint bytesRead, uint manifestBytesRequired, uint manifestBufferBytesAvailable, GptEspLocationReadinessResult gpt)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.Success, isInitialized: true, GptStatus.Success, gpt.GptTableStatus, gpt.GptCrcStatus, BlockDeviceStatus.Success, Fat32Status.Success, NativeManifestParseStatus.Success, gpt.PartitionEntryIndex, gpt.PartitionEntryBytesRequired, gpt.PartitionEntrySectorBytesRequired, gpt.ExpectedHeaderCrc32, gpt.ComputedHeaderCrc32, gpt.ExpectedPartitionEntryArrayCrc32, gpt.ComputedPartitionEntryArrayCrc32, manifest, bytesRead, manifestBytesRequired, manifestBufferBytesAvailable, gptHeaderBytesRequired: gpt.HeaderBytesRequired, gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable, gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable, gptTableSource: gpt.TableSource);
    }

    public static NativeManifestLoadResult GptFail(GptStatus gptStatus, BlockDeviceStatus blockStatus)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.GptFailed, isInitialized: true, gptStatus, GptPartitionTableStatus.Success, GptPartitionTableCrcStatus.NotValidated, blockStatus, Fat32Status.Success, NativeManifestParseStatus.Success, 0, 0, 0, 0, 0, 0, 0, default, 0, 0, 0);
    }

    public static NativeManifestLoadResult GptFail(GptEspLocationReadinessResult gpt)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.GptFailed, isInitialized: true, gpt.GptStatus, gpt.GptTableStatus, gpt.GptCrcStatus, gpt.BlockStatus, Fat32Status.Success, NativeManifestParseStatus.Success, gpt.PartitionEntryIndex, gpt.PartitionEntryBytesRequired, gpt.PartitionEntrySectorBytesRequired, gpt.ExpectedHeaderCrc32, gpt.ComputedHeaderCrc32, gpt.ExpectedPartitionEntryArrayCrc32, gpt.ComputedPartitionEntryArrayCrc32, default, 0, 0, 0, gptHeaderBytesRequired: gpt.HeaderBytesRequired, gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable, gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable, gptTableSource: gpt.TableSource);
    }

    public static NativeManifestLoadResult PartitionFail(BlockDeviceStatus blockStatus)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.PartitionDeviceFailed, isInitialized: true, GptStatus.Success, GptPartitionTableStatus.Success, GptPartitionTableCrcStatus.NotValidated, blockStatus, Fat32Status.Success, NativeManifestParseStatus.Success, 0, 0, 0, 0, 0, 0, 0, default, 0, 0, 0);
    }

    public static NativeManifestLoadResult PartitionFail(BlockDeviceStatus blockStatus, GptEspLocationReadinessResult gpt)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.PartitionDeviceFailed, isInitialized: true, GptStatus.Success, gpt.GptTableStatus, gpt.GptCrcStatus, blockStatus, Fat32Status.Success, NativeManifestParseStatus.Success, gpt.PartitionEntryIndex, gpt.PartitionEntryBytesRequired, gpt.PartitionEntrySectorBytesRequired, gpt.ExpectedHeaderCrc32, gpt.ComputedHeaderCrc32, gpt.ExpectedPartitionEntryArrayCrc32, gpt.ComputedPartitionEntryArrayCrc32, default, 0, 0, 0, gptHeaderBytesRequired: gpt.HeaderBytesRequired, gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable, gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable, gptTableSource: gpt.TableSource);
    }

    public static NativeManifestLoadResult Fat32Fail(Fat32Status fat32Status, BlockDeviceStatus blockStatus)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.Fat32ReadFailed, isInitialized: true, GptStatus.Success, GptPartitionTableStatus.Success, GptPartitionTableCrcStatus.NotValidated, blockStatus, fat32Status, NativeManifestParseStatus.Success, 0, 0, 0, 0, 0, 0, 0, default, 0, 0, 0);
    }

    public static NativeManifestLoadResult Fat32Fail(Fat32FileReadResult file, uint manifestBufferBytesAvailable)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.Fat32ReadFailed, isInitialized: true, GptStatus.Success, GptPartitionTableStatus.Success, GptPartitionTableCrcStatus.NotValidated, file.BlockStatus, file.Status, NativeManifestParseStatus.Success, 0, 0, 0, 0, 0, 0, 0, default, file.BytesRead, file.FileSize, manifestBufferBytesAvailable);
    }

    public static NativeManifestLoadResult Fat32Fail(Fat32Status fat32Status, BlockDeviceStatus blockStatus, GptEspLocationReadinessResult gpt)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.Fat32ReadFailed, isInitialized: true, GptStatus.Success, gpt.GptTableStatus, gpt.GptCrcStatus, blockStatus, fat32Status, NativeManifestParseStatus.Success, gpt.PartitionEntryIndex, gpt.PartitionEntryBytesRequired, gpt.PartitionEntrySectorBytesRequired, gpt.ExpectedHeaderCrc32, gpt.ComputedHeaderCrc32, gpt.ExpectedPartitionEntryArrayCrc32, gpt.ComputedPartitionEntryArrayCrc32, default, 0, 0, 0, gptHeaderBytesRequired: gpt.HeaderBytesRequired, gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable, gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable, gptTableSource: gpt.TableSource);
    }

    public static NativeManifestLoadResult Fat32Fail(Fat32FileReadResult file, uint manifestBufferBytesAvailable, GptEspLocationReadinessResult gpt)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.Fat32ReadFailed, isInitialized: true, GptStatus.Success, gpt.GptTableStatus, gpt.GptCrcStatus, file.BlockStatus, file.Status, NativeManifestParseStatus.Success, gpt.PartitionEntryIndex, gpt.PartitionEntryBytesRequired, gpt.PartitionEntrySectorBytesRequired, gpt.ExpectedHeaderCrc32, gpt.ComputedHeaderCrc32, gpt.ExpectedPartitionEntryArrayCrc32, gpt.ComputedPartitionEntryArrayCrc32, default, file.BytesRead, file.FileSize, manifestBufferBytesAvailable, gptHeaderBytesRequired: gpt.HeaderBytesRequired, gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable, gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable, gptTableSource: gpt.TableSource);
    }

    public static NativeManifestLoadResult ManifestFail(NativeManifestParseStatus manifestStatus, uint bytesRead)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.ManifestParseFailed, isInitialized: true, GptStatus.Success, GptPartitionTableStatus.Success, GptPartitionTableCrcStatus.NotValidated, BlockDeviceStatus.Success, Fat32Status.Success, manifestStatus, 0, 0, 0, 0, 0, 0, 0, default, bytesRead, bytesRead, bytesRead);
    }

    public static NativeManifestLoadResult ManifestFail(NativeManifestParseStatus manifestStatus, uint bytesRead, uint manifestBytesRequired, uint manifestBufferBytesAvailable)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.ManifestParseFailed, isInitialized: true, GptStatus.Success, GptPartitionTableStatus.Success, GptPartitionTableCrcStatus.NotValidated, BlockDeviceStatus.Success, Fat32Status.Success, manifestStatus, 0, 0, 0, 0, 0, 0, 0, default, bytesRead, manifestBytesRequired, manifestBufferBytesAvailable);
    }

    public static NativeManifestLoadResult ManifestFail(NativeManifestParseStatus manifestStatus, uint bytesRead, GptEspLocationReadinessResult gpt)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.ManifestParseFailed, isInitialized: true, GptStatus.Success, gpt.GptTableStatus, gpt.GptCrcStatus, BlockDeviceStatus.Success, Fat32Status.Success, manifestStatus, gpt.PartitionEntryIndex, gpt.PartitionEntryBytesRequired, gpt.PartitionEntrySectorBytesRequired, gpt.ExpectedHeaderCrc32, gpt.ComputedHeaderCrc32, gpt.ExpectedPartitionEntryArrayCrc32, gpt.ComputedPartitionEntryArrayCrc32, default, bytesRead, bytesRead, bytesRead, gptHeaderBytesRequired: gpt.HeaderBytesRequired, gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable, gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable, gptTableSource: gpt.TableSource);
    }

    public static NativeManifestLoadResult ManifestFail(NativeManifestParseStatus manifestStatus, uint bytesRead, uint manifestBytesRequired, uint manifestBufferBytesAvailable, GptEspLocationReadinessResult gpt)
    {
        return new NativeManifestLoadResult(NativeManifestLoadStatus.ManifestParseFailed, isInitialized: true, GptStatus.Success, gpt.GptTableStatus, gpt.GptCrcStatus, BlockDeviceStatus.Success, Fat32Status.Success, manifestStatus, gpt.PartitionEntryIndex, gpt.PartitionEntryBytesRequired, gpt.PartitionEntrySectorBytesRequired, gpt.ExpectedHeaderCrc32, gpt.ComputedHeaderCrc32, gpt.ExpectedPartitionEntryArrayCrc32, gpt.ComputedPartitionEntryArrayCrc32, default, bytesRead, manifestBytesRequired, manifestBufferBytesAvailable, gptHeaderBytesRequired: gpt.HeaderBytesRequired, gptHeaderBufferBytesAvailable: gpt.HeaderBufferBytesAvailable, gptPartitionEntryBufferBytesAvailable: gpt.PartitionEntryBufferBytesAvailable, gptTableSource: gpt.TableSource);
    }
}
