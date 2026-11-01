namespace OS.Drivers;
public static class VirtioNetQueueNotificationRegisterWindowBarrierExecutor
{
    public const bool RequiresBarrierReadiness = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool WritesCallerOwnedWindowBuffer = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public static VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult Execute(VirtioNetQueueNotificationPlanResult notificationPlan, VirtioNetQueueNotificationBarrierReadiness barrier, VirtioPciBarRegisterWindowBinding notifyConfiguration, Span<byte> notifyWindowBytes)
    {
        VirtioNetQueueNotificationRegisterWindowPlanResult plan = VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess(notificationPlan, notifyConfiguration);
        if (!plan.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(MapPlanStatus(plan.Status), plan, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, barrier, registerWindowExecutionAttempted: false);
        }

        if (!barrier.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady, plan, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, barrier, registerWindowExecutionAttempted: false);
        }

        VirtioPciBarRegisterWindowAccessExecutionResult write = VirtioPciBarRegisterWindowAccessExecutor.Write(plan.AccessPlan, plan.WriteValue, notifyWindowBytes);
        if (!write.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed, plan, write.Status, barrier, registerWindowExecutionAttempted: true);
        }

        return VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success, plan, write.Status, barrier, registerWindowExecutionAttempted: true);
    }

    private static VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus MapPlanStatus(VirtioNetQueueNotificationRegisterWindowPlanStatus status)
    {
        return status == VirtioNetQueueNotificationRegisterWindowPlanStatus.NotificationPlanFailed ? VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.NotificationPlanFailed : status == VirtioNetQueueNotificationRegisterWindowPlanStatus.UnsupportedWriteWidth ? VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.UnsupportedWriteWidth : VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.RegisterWindowPlanFailed;
    }
}
