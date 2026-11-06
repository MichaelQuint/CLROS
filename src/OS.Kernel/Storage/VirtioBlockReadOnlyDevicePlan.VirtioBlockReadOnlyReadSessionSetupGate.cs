using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockReadOnlyReadSessionSetupGate
{
    public const bool RequiresSuccessfulQueueSetupBarrierExecution = true;
    public const bool RequiresQueueSetupBarrierReadiness = true;
    public const bool RequiresQueueEnableAttempted = true;
    public const bool DelegatesReadSessionAfterSetup = true;
    public const bool PreservesCursorOnSetupFailure = true;
    public const bool PerformsMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool WaitsForUsedRingCompletion = false;

    public static VirtioBlockReadOnlyReadSessionSetupGateResult ReadAfterQueueSetup(
        ref VirtioBlockReadOnlyReadSession session,
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult queueSetup,
        ulong firstSector,
        uint sectorCount,
        VirtioBlockQueueNotificationBarrierReadiness notificationBarrier)
    {
        VirtioBlockQueueCursorState cursorBefore = session.Cursor;

        if (!queueSetup.IsInitialized)
        {
            return VirtioBlockReadOnlyReadSessionSetupGateResult.Reject(
                VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueSetupExecutionFailed,
                queueSetup,
                cursorBefore);
        }

        if (!queueSetup.BarrierSucceeded ||
            queueSetup.Status == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady)
        {
            return VirtioBlockReadOnlyReadSessionSetupGateResult.Reject(
                VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueSetupBarrierNotReady,
                queueSetup,
                cursorBefore);
        }

        if (!queueSetup.Succeeded)
        {
            return VirtioBlockReadOnlyReadSessionSetupGateResult.Reject(
                VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueSetupExecutionFailed,
                queueSetup,
                cursorBefore);
        }

        if (!queueSetup.QueueEnableAttempted)
        {
            return VirtioBlockReadOnlyReadSessionSetupGateResult.Reject(
                VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueEnableNotAttempted,
                queueSetup,
                cursorBefore);
        }

        VirtioBlockReadOnlyReadSessionResult readSession =
            session.Read(
                firstSector,
                sectorCount,
                notificationBarrier);

        return VirtioBlockReadOnlyReadSessionSetupGateResult.FromReadSession(
            queueSetup,
            readSession);
    }
}
