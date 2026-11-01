using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockQueueNotificationRegisterWindowPlanner
{
    public const bool ConsumesNotifyOffsetPlan = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool PlansMappedRegisterAddresses = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
    public const bool PerformsMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool ReadsDeviceRegisters = false;
    public const bool WritesDeviceRegisters = false;
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

    public static VirtioBlockQueueNotificationRegisterWindowPlanResult PlanAccess(
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioPciBarRegisterWindowBinding notifyConfiguration)
    {
        if (!notifyOffset.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowPlanResult.Create(
                VirtioBlockQueueNotificationRegisterWindowPlanStatus.NotifyOffsetPlanFailed,
                notifyOffset,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                default);
        }

        if (notifyOffset.WriteWidthBytes !=
            VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes)
        {
            return VirtioBlockQueueNotificationRegisterWindowPlanResult.Create(
                VirtioBlockQueueNotificationRegisterWindowPlanStatus.UnsupportedWriteWidth,
                notifyOffset,
                VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                default);
        }

        VirtioPciBarRegisterWindowAccessPlanResult accessPlan =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                notifyConfiguration,
                notifyOffset.NotifyRelativeOffset,
                VirtioPciRegisterAccessWidth.Word);
        if (!accessPlan.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowPlanResult.Create(
                VirtioBlockQueueNotificationRegisterWindowPlanStatus.RegisterWindowPlanFailed,
                notifyOffset,
                accessPlan.Status,
                default);
        }

        return VirtioBlockQueueNotificationRegisterWindowPlanResult.Create(
            VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success,
            notifyOffset,
            accessPlan.Status,
            accessPlan.Plan);
    }
}
