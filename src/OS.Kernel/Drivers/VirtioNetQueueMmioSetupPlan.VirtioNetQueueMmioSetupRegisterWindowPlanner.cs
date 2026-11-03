using OS.Std.Binary;

namespace OS.Drivers;

public static class VirtioNetQueueMmioSetupRegisterWindowPlanner
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool WritesAccessPlansToCallerStorage = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool ReadsDeviceRegisters = false;
    public const bool WritesDeviceRegisters = false;
    public const bool ConfiguresQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public static VirtioNetQueueMmioSetupRegisterWindowPlanResult PlanAccesses(VirtioNetQueueMmioSetupPlanResult setupPlan, ReadOnlySpan<VirtioNetQueueMmioSetupCommand> commands, VirtioPciBarRegisterWindowBinding commonConfiguration, Span<VirtioPciBarRegisterWindowAccessPlan> accessPlans)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(VirtioNetQueueMmioSetupRegisterWindowPlanStatus.SetupPlanFailed, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.Success, default, commandIndex: -1, accessPlansWritten: 0, requiredAccessPlanCount: setupPlan.CommandCount, availableAccessPlanCount: accessPlans.Length);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(VirtioNetQueueMmioSetupRegisterWindowPlanStatus.CommandSourceTooSmall, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.Success, default, commandIndex: commands.Length, accessPlansWritten: 0, requiredAccessPlanCount: setupPlan.CommandCount, availableAccessPlanCount: accessPlans.Length);
        }

        if (accessPlans.Length < setupPlan.CommandCount)
        {
            return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(VirtioNetQueueMmioSetupRegisterWindowPlanStatus.AccessPlanDestinationTooSmall, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.Success, default, commandIndex: accessPlans.Length, accessPlansWritten: 0, requiredAccessPlanCount: setupPlan.CommandCount, availableAccessPlanCount: accessPlans.Length);
        }

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioNetQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(VirtioNetQueueMmioSetupRegisterWindowPlanStatus.UnsupportedCommandWidth, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth, command, index, accessPlansWritten: index, requiredAccessPlanCount: setupPlan.CommandCount, availableAccessPlanCount: accessPlans.Length);
            }

            VirtioPciBarRegisterWindowAccessPlanResult accessPlan = VirtioPciBarRegisterWindowPlanner.PlanAccess(commonConfiguration, command.RelativeOffset, width);
            if (!accessPlan.Succeeded)
            {
                return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Fail(VirtioNetQueueMmioSetupRegisterWindowPlanStatus.RegisterWindowPlanFailed, setupPlan.Status, accessPlan.Status, command, index, accessPlansWritten: index, requiredAccessPlanCount: setupPlan.CommandCount, availableAccessPlanCount: accessPlans.Length);
            }

            accessPlans[index] = accessPlan.Plan;
        }

        return VirtioNetQueueMmioSetupRegisterWindowPlanResult.Success(setupPlan, setupPlan.CommandCount, accessPlans.Length);
    }

    private static VirtioPciRegisterAccessWidth GetRegisterWidth(uint widthBytes)
    {
        return widthBytes == sizeof(byte) ? VirtioPciRegisterAccessWidth.Byte : widthBytes == sizeof(ushort) ? VirtioPciRegisterAccessWidth.Word : widthBytes == sizeof(uint) ? VirtioPciRegisterAccessWidth.DWord : widthBytes == sizeof(ulong) ? VirtioPciRegisterAccessWidth.QWord : default;
    }
}
