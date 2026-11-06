using OS.Boot;
using OS.Storage;

namespace OS.Manifest;

public readonly struct VirtioBlockNativeAppSelectionPreflightResult
{
    private readonly bool _isInitialized;
    private VirtioBlockNativeAppSelectionPreflightResult(VirtioBlockNativeAppSelectionPreflightStatus status, bool readSessionComplete, VirtioBlockReadOnlyReadSessionStatus readSessionStatus, VirtioBlockReadQueuePipelineStatus readSessionPipelineStatus, VirtioBlockQueueCursorState cursorBefore, VirtioBlockQueueCursorState cursorAfter, ulong readSessionByteCount, BlockDeviceStatus blockDeviceStatus, bool adapterCreateAttempted, VirtioBlockReadSessionBlockDeviceCreateResult adapterCreate, bool scratchPlanAttempted, NativeAppSelectionBootScratchPlanResult scratchPlan, bool selectionLoadAttempted, NativeAppSelectionLoadResult selectionLoad)
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

    public VirtioBlockNativeAppSelectionPreflightStatus Status
    {
        get;
    }
    public bool IsInitialized => _isInitialized;
    public bool ReadSessionComplete
    {
        get;
    }
    public VirtioBlockReadOnlyReadSessionStatus ReadSessionStatus
    {
        get;
    }
    public VirtioBlockReadQueuePipelineStatus ReadSessionPipelineStatus
    {
        get;
    }
    public VirtioBlockQueueCursorState CursorBefore
    {
        get;
    }
    public VirtioBlockQueueCursorState CursorAfter
    {
        get;
    }
    public ulong ReadSessionByteCount
    {
        get;
    }
    public BlockDeviceStatus BlockDeviceStatus
    {
        get;
    }
    public bool AdapterCreateAttempted
    {
        get;
    }
    public VirtioBlockReadSessionBlockDeviceCreateResult AdapterCreate
    {
        get;
    }
    public bool ScratchPlanAttempted
    {
        get;
    }
    public NativeAppSelectionBootScratchPlanResult ScratchPlan
    {
        get;
    }
    public bool SelectionLoadAttempted
    {
        get;
    }
    public NativeAppSelectionLoadResult SelectionLoad
    {
        get;
    }
    public bool Succeeded => _isInitialized && Status == VirtioBlockNativeAppSelectionPreflightStatus.Success;

    internal static VirtioBlockNativeAppSelectionPreflightResult Reject(VirtioBlockNativeAppSelectionPreflightStatus status, in VirtioBlockReadOnlyReadSessionResult session, BlockDeviceStatus blockDeviceStatus, bool scratchPlanAttempted, NativeAppSelectionBootScratchPlanResult scratchPlan, bool selectionLoadAttempted, NativeAppSelectionLoadResult selectionLoad, bool adapterCreateAttempted = false, VirtioBlockReadSessionBlockDeviceCreateResult adapterCreate = default)
    {
        return new VirtioBlockNativeAppSelectionPreflightResult(status, session.Succeeded, session.Status, session.Adapter.PipelineStatus, session.CursorBefore, session.CursorAfter, session.BlockRead.BytesRead, blockDeviceStatus, adapterCreateAttempted, adapterCreate, scratchPlanAttempted, scratchPlan, selectionLoadAttempted, selectionLoad);
    }

    internal static VirtioBlockNativeAppSelectionPreflightResult FromSelectionLoad(in VirtioBlockReadOnlyReadSessionResult session, NativeAppSelectionBootScratchPlanResult scratchPlan, NativeAppSelectionLoadResult selectionLoad, bool adapterCreateAttempted = false, VirtioBlockReadSessionBlockDeviceCreateResult adapterCreate = default)
    {
        VirtioBlockNativeAppSelectionPreflightStatus status = selectionLoad.Succeeded ? VirtioBlockNativeAppSelectionPreflightStatus.Success : VirtioBlockNativeAppSelectionPreflightStatus.SelectionLoadFailed;
        return new VirtioBlockNativeAppSelectionPreflightResult(status, session.Succeeded, session.Status, session.Adapter.PipelineStatus, session.CursorBefore, session.CursorAfter, session.BlockRead.BytesRead, BlockDeviceStatus.Success, adapterCreateAttempted, adapterCreate, scratchPlanAttempted: true, scratchPlan, selectionLoadAttempted: true, selectionLoad);
    }

    internal VirtioBlockNativeAppSelectionPreflightResult WithAdapterCreate(VirtioBlockReadSessionBlockDeviceCreateResult adapterCreate)
    {
        return new VirtioBlockNativeAppSelectionPreflightResult(Status, ReadSessionComplete, ReadSessionStatus, ReadSessionPipelineStatus, CursorBefore, CursorAfter, ReadSessionByteCount, BlockDeviceStatus, adapterCreateAttempted: true, adapterCreate, ScratchPlanAttempted, ScratchPlan, SelectionLoadAttempted, SelectionLoad);
    }
}
