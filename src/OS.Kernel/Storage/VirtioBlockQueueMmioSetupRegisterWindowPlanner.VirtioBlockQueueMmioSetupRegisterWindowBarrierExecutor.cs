using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool StagesExpectedDeviceReadValues = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool GatesQueueEnableOnBarrier = true;
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

    public static VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult Execute(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        ReadOnlySpan<VirtioBlockQueueMmioSetupCommand> commands,
        VirtioPciBarRegisterWindowBinding commonConfiguration,
        Span<byte> commonWindowBytes,
        VirtioBlockQueueMmioSetupBarrierReadiness barrier)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.SetupPlanFailed,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: -1,
                commandsExecuted: 0,
                barrier,
                queueEnableAttempted: false);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.CommandSourceTooSmall,
                setupPlan.Status,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                default,
                commandIndex: commands.Length,
                commandsExecuted: 0,
                barrier,
                queueEnableAttempted: false);
        }

        bool queueEnableAttempted = false;

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioBlockQueueMmioSetupCommand command = commands[index];
            VirtioPciRegisterAccessWidth width = GetRegisterWidth(command.WidthBytes);
            if (width == default)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.UnsupportedCommandWidth,
                    setupPlan.Status,
                    VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                    VirtioPciBarRegisterWindowAccessExecutionStatus.UnsupportedAccessWidth,
                    command,
                    index,
                    commandsExecuted: index,
                    barrier,
                    queueEnableAttempted);
            }

            VirtioPciBarRegisterWindowAccessPlanResult accessPlan =
                VirtioPciBarRegisterWindowPlanner.PlanAccess(
                    commonConfiguration,
                    command.RelativeOffset,
                    width);
            if (!accessPlan.Succeeded)
            {
                return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                    VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.RegisterWindowPlanFailed,
                    setupPlan.Status,
                    accessPlan.Status,
                    VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                    command,
                    index,
                    commandsExecuted: index,
                    barrier,
                    queueEnableAttempted);
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
                    return AccessFailure(setupPlan, command, index, stage, barrier, queueEnableAttempted);
                }

                VirtioPciBarRegisterWindowAccessExecutionResult read =
                    VirtioPciBarRegisterWindowAccessExecutor.Read(
                        accessPlan.Plan,
                        commonWindowBytes);
                if (!read.Succeeded)
                {
                    return AccessFailure(setupPlan, command, index, read, barrier, queueEnableAttempted);
                }

                if (read.Value != command.Value)
                {
                    return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.RegisterReadMismatch,
                        setupPlan.Status,
                        VirtioPciBarRegisterWindowPlanStatus.Success,
                        read.Status,
                        command,
                        index,
                        commandsExecuted: index,
                        barrier,
                        queueEnableAttempted,
                        read.Value);
                }

                continue;
            }

            if (command.Kind == VirtioBlockQueueMmioSetupCommandKind.WriteQueueEnable)
            {
                if (!barrier.Succeeded)
                {
                    return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
                        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady,
                        setupPlan.Status,
                        VirtioPciBarRegisterWindowPlanStatus.Success,
                        VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                        command,
                        index,
                        commandsExecuted: index,
                        barrier,
                        queueEnableAttempted: false);
                }

                queueEnableAttempted = true;
            }

            VirtioPciBarRegisterWindowAccessExecutionResult write =
                VirtioPciBarRegisterWindowAccessExecutor.Write(
                    accessPlan.Plan,
                    command.Value,
                    commonWindowBytes);
            if (!write.Succeeded)
            {
                return AccessFailure(setupPlan, command, index, write, barrier, queueEnableAttempted);
            }
        }

        return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Success(
            setupPlan,
            commands[setupPlan.CommandCount - 1],
            setupPlan.CommandCount,
            barrier,
            queueEnableAttempted);
    }

    private static VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult AccessFailure(
        VirtioBlockQueueMmioSetupPlanResult setupPlan,
        VirtioBlockQueueMmioSetupCommand command,
        int commandIndex,
        VirtioPciBarRegisterWindowAccessExecutionResult access,
        VirtioBlockQueueMmioSetupBarrierReadiness barrier,
        bool queueEnableAttempted)
    {
        return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult.Fail(
            VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.AccessExecutionFailed,
            setupPlan.Status,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            access.Status,
            command,
            commandIndex,
            commandsExecuted: commandIndex,
            barrier,
            queueEnableAttempted,
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
