using OS.Boot;
using OS.Storage;

namespace OS.Manifest;

public enum VirtioBlockNativeAppSelectionPreflightStatus
{
    Success = 0,
    IncompleteReadSession = 1,
    InvalidBlockDevice = 2,
    ScratchPlanFailed = 3,
    SelectionLoadFailed = 4
}

public readonly struct VirtioBlockNativeAppSelectionPreflightResult
{
    private readonly bool _isInitialized;

    private VirtioBlockNativeAppSelectionPreflightResult(
        VirtioBlockNativeAppSelectionPreflightStatus status,
        bool readSessionComplete,
        VirtioBlockReadOnlyReadSessionStatus readSessionStatus,
        VirtioBlockReadQueuePipelineStatus readSessionPipelineStatus,
        VirtioBlockQueueCursorState cursorBefore,
        VirtioBlockQueueCursorState cursorAfter,
        ulong readSessionByteCount,
        BlockDeviceStatus blockDeviceStatus,
        bool adapterCreateAttempted,
        VirtioBlockReadSessionBlockDeviceCreateResult adapterCreate,
        bool scratchPlanAttempted,
        NativeAppSelectionBootScratchPlanResult scratchPlan,
        bool selectionLoadAttempted,
        NativeAppSelectionLoadResult selectionLoad)
    {
        _isInitialized = true;
        Status = status;
        ReadSessionComplete = readSessionComplete;
        ReadSessionStatus = readSessionStatus;
        ReadSessionPipelineStatus = readSessionPipelineStatus;
        CursorBefore = cursorBefore;
        CursorAfter = cursorAfter;
        ReadSessionByteCount = readSessionByteCount;
        BlockDeviceStatus = blockDeviceStatus;
        AdapterCreateAttempted = adapterCreateAttempted;
        AdapterCreate = adapterCreate;
        ScratchPlanAttempted = scratchPlanAttempted;
        ScratchPlan = scratchPlan;
        SelectionLoadAttempted = selectionLoadAttempted;
        SelectionLoad = selectionLoad;
    }

    public VirtioBlockNativeAppSelectionPreflightStatus Status { get; }

    public bool IsInitialized => _isInitialized;

    public bool ReadSessionComplete { get; }

    public VirtioBlockReadOnlyReadSessionStatus ReadSessionStatus { get; }

    public VirtioBlockReadQueuePipelineStatus ReadSessionPipelineStatus { get; }

    public VirtioBlockQueueCursorState CursorBefore { get; }

    public VirtioBlockQueueCursorState CursorAfter { get; }

    public ulong ReadSessionByteCount { get; }

    public BlockDeviceStatus BlockDeviceStatus { get; }

    public bool AdapterCreateAttempted { get; }

    public VirtioBlockReadSessionBlockDeviceCreateResult AdapterCreate { get; }

    public bool ScratchPlanAttempted { get; }

    public NativeAppSelectionBootScratchPlanResult ScratchPlan { get; }

    public bool SelectionLoadAttempted { get; }

    public NativeAppSelectionLoadResult SelectionLoad { get; }

    public bool Succeeded =>
        _isInitialized &&
        Status == VirtioBlockNativeAppSelectionPreflightStatus.Success;

    internal static VirtioBlockNativeAppSelectionPreflightResult Reject(
        VirtioBlockNativeAppSelectionPreflightStatus status,
        in VirtioBlockReadOnlyReadSessionResult session,
        BlockDeviceStatus blockDeviceStatus,
        bool scratchPlanAttempted,
        NativeAppSelectionBootScratchPlanResult scratchPlan,
        bool selectionLoadAttempted,
        NativeAppSelectionLoadResult selectionLoad,
        bool adapterCreateAttempted = false,
        VirtioBlockReadSessionBlockDeviceCreateResult adapterCreate = default)
    {
        return new VirtioBlockNativeAppSelectionPreflightResult(
            status,
            session.Succeeded,
            session.Status,
            session.Adapter.PipelineStatus,
            session.CursorBefore,
            session.CursorAfter,
            session.BlockRead.BytesRead,
            blockDeviceStatus,
            adapterCreateAttempted,
            adapterCreate,
            scratchPlanAttempted,
            scratchPlan,
            selectionLoadAttempted,
            selectionLoad);
    }

    internal static VirtioBlockNativeAppSelectionPreflightResult FromSelectionLoad(
        in VirtioBlockReadOnlyReadSessionResult session,
        NativeAppSelectionBootScratchPlanResult scratchPlan,
        NativeAppSelectionLoadResult selectionLoad,
        bool adapterCreateAttempted = false,
        VirtioBlockReadSessionBlockDeviceCreateResult adapterCreate = default)
    {
        VirtioBlockNativeAppSelectionPreflightStatus status = selectionLoad.Succeeded
            ? VirtioBlockNativeAppSelectionPreflightStatus.Success
            : VirtioBlockNativeAppSelectionPreflightStatus.SelectionLoadFailed;

        return new VirtioBlockNativeAppSelectionPreflightResult(
            status,
            session.Succeeded,
            session.Status,
            session.Adapter.PipelineStatus,
            session.CursorBefore,
            session.CursorAfter,
            session.BlockRead.BytesRead,
            BlockDeviceStatus.Success,
            adapterCreateAttempted,
            adapterCreate,
            scratchPlanAttempted: true,
            scratchPlan,
            selectionLoadAttempted: true,
            selectionLoad);
    }

    internal VirtioBlockNativeAppSelectionPreflightResult WithAdapterCreate(
        VirtioBlockReadSessionBlockDeviceCreateResult adapterCreate)
    {
        return new VirtioBlockNativeAppSelectionPreflightResult(
            Status,
            ReadSessionComplete,
            ReadSessionStatus,
            ReadSessionPipelineStatus,
            CursorBefore,
            CursorAfter,
            ReadSessionByteCount,
            BlockDeviceStatus,
            adapterCreateAttempted: true,
            adapterCreate,
            ScratchPlanAttempted,
            ScratchPlan,
            SelectionLoadAttempted,
            SelectionLoad);
    }
}

public static class VirtioBlockNativeAppSelectionPreflight
{
    public const bool ValidatesCallerOwnedReadSession = true;
    public const bool RejectsIncompleteReadSessionBeforeScratchValidation = true;
    public const bool ValidatesCallerOwnedBlockDeviceGeometry = true;
    public const bool UsesCompletedReadSessionBlockDeviceAdapter = true;
    public const bool ValidatesBootScratchPlan = true;
    public const bool DelegatesNativeAppSelectionLoader = true;
    public const bool PreservesReadSessionProvenance = true;
    public const bool PreservesAdapterCreateProvenance = true;
    public const bool PreservesScratchPlanProvenance = true;
    public const bool PreservesSelectionLoadProvenance = true;
    public const bool AllocatesBuffers = false;
    public const bool NotifiesLiveDevice = false;
    public const bool WaitsForHardware = false;
    public const bool PerformsDmaMapping = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool ReadsRealDisk = false;
    public const bool ExecutesAppCode = false;
    public const bool StartsHostedDotNet = false;
    public const bool StartsKestrel = false;

    public static VirtioBlockNativeAppSelectionPreflightResult LoadSelectionFromCompletedReadSession(
        in VirtioBlockReadOnlyReadSessionResult session,
        BlockDeviceInfo expectedInfo,
        ReadOnlyMemory<byte> completedDestination,
        Span<byte> primaryHeaderSector,
        Span<byte> partitionEntrySectors,
        Span<GptPartitionDescriptor> partitions,
        Span<byte> manifestBuffer,
        Span<byte> packageBuffer,
        BootInfo bootInfo,
        NativeAppLaunchRuntimeCapabilities runtimeCapabilities)
    {
        VirtioBlockReadSessionBlockDeviceCreateResult adapterCreate =
            VirtioBlockReadSessionBlockDevice.Create(
                session,
                expectedInfo,
                completedDestination,
                out VirtioBlockReadSessionBlockDevice? disk);
        if (!adapterCreate.Succeeded || disk is null)
        {
            return VirtioBlockNativeAppSelectionPreflightResult.Reject(
                VirtioBlockNativeAppSelectionPreflightStatus.InvalidBlockDevice,
                session,
                adapterCreate.BlockDeviceStatus,
                scratchPlanAttempted: false,
                default,
                selectionLoadAttempted: false,
                default,
                adapterCreateAttempted: true,
                adapterCreate);
        }

        VirtioBlockNativeAppSelectionPreflightResult selection =
            LoadSelection(
                session,
                disk,
                primaryHeaderSector,
                partitionEntrySectors,
                partitions,
                manifestBuffer,
                packageBuffer,
                bootInfo,
                runtimeCapabilities);

        return selection.WithAdapterCreate(adapterCreate);
    }

    public static VirtioBlockNativeAppSelectionPreflightResult LoadSelection(
        in VirtioBlockReadOnlyReadSessionResult session,
        IReadOnlyBlockDevice? disk,
        Span<byte> primaryHeaderSector,
        Span<byte> partitionEntrySectors,
        Span<GptPartitionDescriptor> partitions,
        Span<byte> manifestBuffer,
        Span<byte> packageBuffer,
        BootInfo bootInfo,
        NativeAppLaunchRuntimeCapabilities runtimeCapabilities)
    {
        if (!session.Succeeded)
        {
            return VirtioBlockNativeAppSelectionPreflightResult.Reject(
                VirtioBlockNativeAppSelectionPreflightStatus.IncompleteReadSession,
                session,
                BlockDeviceStatus.Success,
                scratchPlanAttempted: false,
                default,
                selectionLoadAttempted: false,
                default);
        }

        BlockDeviceInfoResult blockInfo = disk is null
            ? BlockDeviceInfoResult.Fail(BlockDeviceStatus.InvalidBlockCount)
            : BlockDeviceInfo.Create(disk.Info.BlockSize, disk.Info.BlockCount);
        if (!blockInfo.Succeeded)
        {
            return VirtioBlockNativeAppSelectionPreflightResult.Reject(
                VirtioBlockNativeAppSelectionPreflightStatus.InvalidBlockDevice,
                session,
                blockInfo.Status,
                scratchPlanAttempted: false,
                default,
                selectionLoadAttempted: false,
                default);
        }

        NativeAppSelectionBootScratchPlanResult scratchPlan =
            NativeAppSelectionBootScratchPlan.ValidateDefault(
                primaryHeaderSector,
                partitionEntrySectors,
                partitions,
                manifestBuffer,
                packageBuffer);
        if (!scratchPlan.Succeeded)
        {
            return VirtioBlockNativeAppSelectionPreflightResult.Reject(
                VirtioBlockNativeAppSelectionPreflightStatus.ScratchPlanFailed,
                session,
                BlockDeviceStatus.Success,
                scratchPlanAttempted: true,
                scratchPlan,
                selectionLoadAttempted: false,
                default);
        }

        NativeAppSelectionLoadResult selectionLoad =
            NativeAppSelectionLoader.LoadFromPrimaryGptEspWithCallerBuffers(
                disk,
                primaryHeaderSector,
                partitionEntrySectors,
                partitions,
                manifestBuffer,
                packageBuffer,
                bootInfo,
                runtimeCapabilities);

        return VirtioBlockNativeAppSelectionPreflightResult.FromSelectionLoad(
            session,
            scratchPlan,
            selectionLoad);
    }
}
