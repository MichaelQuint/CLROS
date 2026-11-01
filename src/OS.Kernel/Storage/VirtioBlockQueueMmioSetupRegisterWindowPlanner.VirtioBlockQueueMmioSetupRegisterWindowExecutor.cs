using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockQueueMmioSetupRegisterWindowExecutor
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool StagesExpectedDeviceReadValues = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
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
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool PerformsDiskWrite = false;

    public static VirtioBlockQueueMmioSetupRegisterWindowExecutionResult Execute(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        ReadOnlySpan<VirtioBlockQueueMmioSetupCommand> commands,
        VirtioPciBarRegisterWindowBinding commonConfiguration,
        Span<byte> commonWindowBytes)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.SetupPlanFailed,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: -1,
                commandsExecuted: 0);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.CommandSourceTooSmall,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: commands.Length,
                commandsExecuted: commands.Length);
        }

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioBlockQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.UnsupportedCommandWidth,
                    setupPlan.Status,
                    VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                    VirtioPciBarRegisterWindowAccessExecutionStatus.UnsupportedAccessWidth,
                    command,
                    index,
                    commandsExecuted: index);
            }

            VirtioPciBarRegisterWindowAccessPlanResult accessPlan =
                VirtioPciBarRegisterWindowPlanner.PlanAccess(
                    commonConfiguration,
                    command.RelativeOffset,
                    width);
            if (!accessPlan.Succeeded)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.RegisterWindowPlanFailed,
                    setupPlan.Status,
                    accessPlan.Status,
                    VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                    command,
                    index,
                    commandsExecuted: index);
            }

            if (command.ReadsRegister)
            {
                ulong stagedValue = GetExpectedReadValue(setupPlan.Plan, command);
                VirtioPciBarRegisterWindowAccessExecutionResult stage =
                    VirtioPciBarRegisterWindowAccessExecutor.Write(
                        accessPlan.Plan,
                        stagedValue,
                        commonWindowBytes);
                if (!stage.Succeeded)
                {
                    return AccessFailure(setupPlan, command, index, stage);
                }

                VirtioPciBarRegisterWindowAccessExecutionResult read =
                    VirtioPciBarRegisterWindowAccessExecutor.Read(
                        accessPlan.Plan,
                        commonWindowBytes);
                if (!read.Succeeded)
                {
                    return AccessFailure(setupPlan, command, index, read);
                }

                if (read.Value != command.Value)
                {
                    return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
                        VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.RegisterReadMismatch,
                        setupPlan.Status,
                        VirtioPciBarRegisterWindowPlanStatus.Success,
                        read.Status,
                        command,
                        index,
                        commandsExecuted: index,
                        read.Value);
                }

                continue;
            }

            VirtioPciBarRegisterWindowAccessExecutionResult write =
                VirtioPciBarRegisterWindowAccessExecutor.Write(
                    accessPlan.Plan,
                    command.Value,
                    commonWindowBytes);
            if (!write.Succeeded)
            {
                return AccessFailure(setupPlan, command, index, write);
            }
        }

        return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Success(
            setupPlan,
            commands[setupPlan.CommandCount - 1],
            setupPlan.CommandCount);
    }

    private static VirtioBlockQueueMmioSetupRegisterWindowExecutionResult AccessFailure(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        VirtioBlockQueueMmioSetupCommand command,
        int commandIndex,
        VirtioPciBarRegisterWindowAccessExecutionResult access)
    {
        return VirtioBlockQueueMmioSetupRegisterWindowExecutionResult.Fail(
            VirtioBlockQueueMmioSetupRegisterWindowExecutionStatus.AccessExecutionFailed,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            access.Status,
            command,
            commandIndex,
            commandsExecuted: commandIndex,
            access.Value);
    }

    private static ulong GetExpectedReadValue(
        VirtioBlockQueueMmioSetupPlan plan,
        VirtioBlockQueueMmioSetupCommand command)
    {
        return command.Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueSize
            ? plan.DeviceMaximumQueueSize
            : command.Kind == VirtioBlockQueueMmioSetupCommandKind.ReadQueueNotifyOffset
                ? plan.NotifyOffset
                : command.Value;
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
