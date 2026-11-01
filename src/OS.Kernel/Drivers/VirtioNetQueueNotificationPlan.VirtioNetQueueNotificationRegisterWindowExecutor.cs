namespace OS.Drivers;
public static class VirtioNetQueueNotificationRegisterWindowExecutor
{
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
    public static VirtioNetQueueNotificationRegisterWindowExecutionResult Execute(VirtioNetQueueNotificationPlanResult notificationPlan, VirtioPciBarRegisterWindowBinding notifyConfiguration, Span<byte> notifyWindowBytes)
    {
        VirtioNetQueueNotificationRegisterWindowPlanResult plan = VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess(notificationPlan, notifyConfiguration);
        if (!plan.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowExecutionResult.FromPlan(MapPlanStatus(plan.Status), plan, VirtioPciBarRegisterWindowAccessExecutionStatus.Success);
        }

        VirtioPciBarRegisterWindowAccessExecutionResult write = VirtioPciBarRegisterWindowAccessExecutor.Write(plan.AccessPlan, plan.WriteValue, notifyWindowBytes);
        if (!write.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowExecutionResult.FromPlan(VirtioNetQueueNotificationRegisterWindowExecutionStatus.AccessExecutionFailed, plan, write.Status);
        }

        return VirtioNetQueueNotificationRegisterWindowExecutionResult.FromPlan(VirtioNetQueueNotificationRegisterWindowExecutionStatus.Success, plan, write.Status);
    }

    private static VirtioNetQueueNotificationRegisterWindowExecutionStatus MapPlanStatus(VirtioNetQueueNotificationRegisterWindowPlanStatus status)
    {
        return status == VirtioNetQueueNotificationRegisterWindowPlanStatus.NotificationPlanFailed ? VirtioNetQueueNotificationRegisterWindowExecutionStatus.NotificationPlanFailed : status == VirtioNetQueueNotificationRegisterWindowPlanStatus.UnsupportedWriteWidth ? VirtioNetQueueNotificationRegisterWindowExecutionStatus.UnsupportedWriteWidth : VirtioNetQueueNotificationRegisterWindowExecutionStatus.RegisterWindowPlanFailed;
    }
}
