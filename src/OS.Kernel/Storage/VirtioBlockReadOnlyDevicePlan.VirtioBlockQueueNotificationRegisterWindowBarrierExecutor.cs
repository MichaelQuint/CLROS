using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockQueueNotificationRegisterWindowBarrierExecutor
{
    public const bool RequiresBarrierReadiness = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool WritesCallerOwnedWindowBuffer = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
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

    public static VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult Execute(
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        VirtioBlockQueueNotificationRegisterWindowPlanResult plan =
            VirtioBlockQueueNotificationRegisterWindowPlanner.PlanAccess(
                notifyOffset,
                notifyConfiguration);
        if (!plan.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
                VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.PlanFailed,
                plan,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                barrier,
                registerWindowExecutionAttempted: false);
        }

        if (!barrier.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
                VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady,
                plan,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                barrier,
                registerWindowExecutionAttempted: false);
        }

        VirtioPciBarRegisterWindowAccessExecutionResult write =
            VirtioPciBarRegisterWindowAccessExecutor.Write(
                plan.AccessPlan,
                plan.WriteValue,
                notifyWindowBytes);
        if (!write.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
                VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed,
                plan,
                write.Status,
                barrier,
                registerWindowExecutionAttempted: true);
        }

        return VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success,
            plan,
            write.Status,
            barrier,
            registerWindowExecutionAttempted: true);
    }
}
