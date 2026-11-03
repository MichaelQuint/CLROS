using OS.Std.Binary;

namespace OS.Drivers;

public static class VirtioNetQueueMmioSetupRegisterWindowExecutor
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool StagesExpectedDeviceReadValues = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public static VirtioNetQueueMmioSetupRegisterWindowExecutionResult Execute(VirtioNetQueueMmioSetupPlanResult setupPlan, ReadOnlySpan<VirtioNetQueueMmioSetupCommand> commands, VirtioPciBarRegisterWindowBinding commonConfiguration, Span<byte> commonWindowBytes)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.SetupPlanFailed, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.Success, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, default, commandIndex: -1, commandsExecuted: 0);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.CommandSourceTooSmall, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.Success, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, default, commandIndex: commands.Length, commandsExecuted: commands.Length);
        }

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioNetQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.UnsupportedCommandWidth, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth, VirtioPciBarRegisterWindowAccessExecutionStatus.UnsupportedAccessWidth, command, index, commandsExecuted: index);
            }

            VirtioPciBarRegisterWindowAccessPlanResult accessPlan = VirtioPciBarRegisterWindowPlanner.PlanAccess(commonConfiguration, command.RelativeOffset, width);
            if (!accessPlan.Succeeded)
            {
                return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.RegisterWindowPlanFailed, setupPlan.Status, accessPlan.Status, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, command, index, commandsExecuted: index);
            }

            if (command.ReadsRegister)
            {
                ulong stagedValue = GetExpectedReadValue(setupPlan.Plan, command);
                VirtioPciBarRegisterWindowAccessExecutionResult stage = VirtioPciBarRegisterWindowAccessExecutor.Write(accessPlan.Plan, stagedValue, commonWindowBytes);
                if (!stage.Succeeded)
                {
                    return AccessFailure(setupPlan, command, index, stage);
                }

                VirtioPciBarRegisterWindowAccessExecutionResult read = VirtioPciBarRegisterWindowAccessExecutor.Read(accessPlan.Plan, commonWindowBytes);
                if (!read.Succeeded)
                {
                    return AccessFailure(setupPlan, command, index, read);
                }

                if (read.Value != command.Value)
                {
                    return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.RegisterReadMismatch, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.Success, read.Status, command, index, commandsExecuted: index, read.Value);
                }

                continue;
            }

            VirtioPciBarRegisterWindowAccessExecutionResult write = VirtioPciBarRegisterWindowAccessExecutor.Write(accessPlan.Plan, command.Value, commonWindowBytes);
            if (!write.Succeeded)
            {
                return AccessFailure(setupPlan, command, index, write);
            }
        }

        return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Success(setupPlan, setupPlan.CommandCount);
    }

    private static VirtioNetQueueMmioSetupRegisterWindowExecutionResult AccessFailure(VirtioNetQueueMmioSetupPlanResult setupPlan, VirtioNetQueueMmioSetupCommand command, int commandIndex, VirtioPciBarRegisterWindowAccessExecutionResult access)
    {
        return VirtioNetQueueMmioSetupRegisterWindowExecutionResult.Fail(VirtioNetQueueMmioSetupRegisterWindowExecutionStatus.AccessExecutionFailed, setupPlan.Status, VirtioPciBarRegisterWindowPlanStatus.Success, access.Status, command, commandIndex, commandsExecuted: commandIndex, access.Value);
    }

    private static ulong GetExpectedReadValue(VirtioNetQueueMmioSetupPlan plan, VirtioNetQueueMmioSetupCommand command)
    {
        VirtioNetQueueMmioSetupQueuePlan queue = command.QueueIndex == plan.RxQueue.QueueIndex ? plan.RxQueue : plan.TxQueue;
        return command.Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueSize ? queue.DeviceMaximumQueueSize : command.Kind == VirtioNetQueueMmioSetupCommandKind.ReadQueueNotifyOffset ? queue.NotifyOffset : command.Value;
    }

    private static VirtioPciRegisterAccessWidth GetRegisterWidth(uint widthBytes)
    {
        return widthBytes == sizeof(byte) ? VirtioPciRegisterAccessWidth.Byte : widthBytes == sizeof(ushort) ? VirtioPciRegisterAccessWidth.Word : widthBytes == sizeof(uint) ? VirtioPciRegisterAccessWidth.DWord : widthBytes == sizeof(ulong) ? VirtioPciRegisterAccessWidth.QWord : default;
    }
}
