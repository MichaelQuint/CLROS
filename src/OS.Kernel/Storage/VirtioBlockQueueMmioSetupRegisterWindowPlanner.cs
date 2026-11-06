using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockQueueMmioSetupRegisterWindowPlanner
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool WritesAccessPlansToCallerStorage = true;
    public const bool PlansCommonConfigurationAccesses = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool ReadsDeviceRegisters = false;
    public const bool WritesDeviceRegisters = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool PerformsDiskWrite = false;

    public static VirtioBlockQueueMmioSetupRegisterWindowPlanResult PlanAccesses(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        ReadOnlySpan<VirtioBlockQueueMmioSetupCommand> commands,
        VirtioPciBarRegisterWindowBinding commonConfiguration,
        Span<VirtioPciBarRegisterWindowAccessPlan> accessPlans)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.SetupPlanFailed,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                default,
                commandIndex: -1,
                accessPlansWritten: 0,
                requiredAccessPlanCount: setupPlan.CommandCount,
                availableAccessPlanCount: accessPlans.Length);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.CommandSourceTooSmall,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                default,
                commandIndex: commands.Length,
                accessPlansWritten: 0,
                requiredAccessPlanCount: setupPlan.CommandCount,
                availableAccessPlanCount: accessPlans.Length);
        }

        if (accessPlans.Length < setupPlan.CommandCount)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.AccessPlanDestinationTooSmall,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                default,
                commandIndex: accessPlans.Length,
                accessPlansWritten: 0,
                requiredAccessPlanCount: setupPlan.CommandCount,
                availableAccessPlanCount: accessPlans.Length);
        }

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioBlockQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.UnsupportedCommandWidth,
                    setupPlan.Status,
                    VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                    command,
                    index,
                    accessPlansWritten: index,
                    requiredAccessPlanCount: setupPlan.CommandCount,
                    availableAccessPlanCount: accessPlans.Length);
            }

            VirtioPciBarRegisterWindowAccessPlanResult accessPlan =
                VirtioPciBarRegisterWindowPlanner.PlanAccess(
                    commonConfiguration,
                    command.RelativeOffset,
                    width);
            if (!accessPlan.Succeeded)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowPlanStatus.RegisterWindowPlanFailed,
                    setupPlan.Status,
                    accessPlan.Status,
                    command,
                    index,
                    accessPlansWritten: index,
                    requiredAccessPlanCount: setupPlan.CommandCount,
                    availableAccessPlanCount: accessPlans.Length);
            }

            accessPlans[index] = accessPlan.Plan;
        }

        return VirtioBlockQueueMmioSetupRegisterWindowPlanResult.Success(
            setupPlan,
            commands[setupPlan.CommandCount - 1],
            setupPlan.CommandCount,
            accessPlans.Length);
    }

    private static VirtioPciRegisterAccessWidth GetRegisterWidth(uint widthBytes)
    {
        return widthBytes == sizeof(byte)
            ? VirtioPciRegisterAccessWidth.Byte
            : widthBytes == sizeof(ushort)
                ? VirtioPciRegisterAccessWidth.Word
                : widthBytes == sizeof(uint)
                    ? VirtioPciRegisterAccessWidth.DWord
                    : widthBytes == sizeof(ulong)
                        ? VirtioPciRegisterAccessWidth.QWord
                        : default;
    }
}
