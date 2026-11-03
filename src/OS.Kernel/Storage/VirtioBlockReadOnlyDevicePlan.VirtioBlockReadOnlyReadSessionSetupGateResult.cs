using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadOnlyReadSessionSetupGateResult
{
    private VirtioBlockReadOnlyReadSessionSetupGateResult(
        VirtioBlockReadOnlyReadSessionSetupGateStatus status,
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult queueSetup,
        VirtioBlockReadOnlyReadSessionResult readSession,
        bool readSessionAttempted,
        VirtioBlockQueueCursorState cursorBefore,
        VirtioBlockQueueCursorState cursorAfter,
        bool isInitialized)
    {
        Status = status;
        QueueSetup = queueSetup;
        ReadSession = readSession;
        ReadSessionAttempted = readSessionAttempted;
        CursorBefore = cursorBefore;
        CursorAfter = cursorAfter;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadOnlyReadSessionSetupGateStatus Status
    {
        get;
    }

    public VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult QueueSetup
    {
        get;
    }

    public VirtioBlockReadOnlyReadSessionResult ReadSession
    {
        get;
    }

    public bool ReadSessionAttempted
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

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus QueueSetupStatus =>
        QueueSetup.Status;

    public VirtioBlockQueueMmioSetupPlanStatus QueueSetupPlanStatus =>
        QueueSetup.SetupPlanStatus;

    public bool QueueSetupBarrierSucceeded => QueueSetup.BarrierSucceeded;

    public bool QueueEnableAttempted => QueueSetup.QueueEnableAttempted;

    public uint QueueSetupBarrierSequence => QueueSetup.BarrierSequence;

    public VirtioBlockReadOnlyReadSessionStatus ReadSessionStatus =>
        ReadSessionAttempted ?
            ReadSession.Status :
            VirtioBlockReadOnlyReadSessionStatus.Success;

    public VirtioBlockReadQueuePipelineStatus ReadSessionPipelineStatus =>
        ReadSessionAttempted ?
            ReadSession.Adapter.PipelineStatus :
            VirtioBlockReadQueuePipelineStatus.Success;

    public BlockReadResult BlockRead =>
        ReadSessionAttempted ?
            ReadSession.BlockRead :
            BlockReadResult.Fail(BlockDeviceStatus.IoError);

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadOnlyReadSessionSetupGateStatus.Success;

    internal static VirtioBlockReadOnlyReadSessionSetupGateResult Reject(
        VirtioBlockReadOnlyReadSessionSetupGateStatus status,
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult queueSetup,
        VirtioBlockQueueCursorState cursor)
    {
        return new VirtioBlockReadOnlyReadSessionSetupGateResult(
            status,
            queueSetup,
            default,
            readSessionAttempted: false,
            cursor,
            cursor,
            isInitialized: true);
    }

    internal static VirtioBlockReadOnlyReadSessionSetupGateResult FromReadSession(
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult queueSetup,
        VirtioBlockReadOnlyReadSessionResult readSession)
    {
        VirtioBlockReadOnlyReadSessionSetupGateStatus status = readSession.Succeeded ?
            VirtioBlockReadOnlyReadSessionSetupGateStatus.Success :
            VirtioBlockReadOnlyReadSessionSetupGateStatus.ReadSessionFailed;
        return new VirtioBlockReadOnlyReadSessionSetupGateResult(
            status,
            queueSetup,
            readSession,
            readSessionAttempted: true,
            readSession.CursorBefore,
            readSession.CursorAfter,
            isInitialized: true);
    }
}
