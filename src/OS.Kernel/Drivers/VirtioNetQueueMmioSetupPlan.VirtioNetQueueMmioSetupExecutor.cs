using OS.Std.Binary;

namespace OS.Drivers;
public static class VirtioNetQueueMmioSetupExecutor
{
    public const bool RequiresInjectedAccessor = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool PublishesDescriptors = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool ReadsOrWritesPacketBuffers = false;
    public static VirtioNetQueueMmioSetupExecutionResult Execute(VirtioNetQueueMmioSetupPlanResult setupPlan, ReadOnlySpan<VirtioNetQueueMmioSetupCommand> commands, VirtioPciBarAperture commonAperture, IVirtioPciRegisterAccessor? accessor)
    {
        if (!setupPlan.Succeeded)
        {
            return VirtioNetQueueMmioSetupExecutionResult.Fail(VirtioNetQueueMmioSetupExecutionStatus.SetupPlanFailed, setupPlan.Status, VirtioPciRegisterReadStatus.NotReady, VirtioPciRegisterWriteStatus.NotReady, default, commandIndex: -1, commandsExecuted: 0);
        }

        if (commands.Length < setupPlan.CommandCount)
        {
            return VirtioNetQueueMmioSetupExecutionResult.Fail(VirtioNetQueueMmioSetupExecutionStatus.CommandSourceTooSmall, setupPlan.Status, VirtioPciRegisterReadStatus.NotReady, VirtioPciRegisterWriteStatus.NotReady, default, commandIndex: commands.Length, commandsExecuted: commands.Length);
        }

        if (accessor is null)
        {
            return VirtioNetQueueMmioSetupExecutionResult.Fail(VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady, setupPlan.Status, VirtioPciRegisterReadStatus.NotReady, VirtioPciRegisterWriteStatus.NotReady, commands[0], commandIndex: 0, commandsExecuted: 0);
        }

        for (int index = 0; index < setupPlan.CommandCount; index++)
        {
            VirtioNetQueueMmioSetupCommand command = commands[index];
            if (command.ReadsRegister)
            {
                if (command.WidthBytes != sizeof(ushort))
                {
                    return VirtioNetQueueMmioSetupExecutionResult.Fail(VirtioNetQueueMmioSetupExecutionStatus.UnsupportedCommandWidth, setupPlan.Status, VirtioPciRegisterReadStatus.UnsupportedWidth, VirtioPciRegisterWriteStatus.Success, command, index, commandsExecuted: index);
                }

                VirtioPciRegisterRead16Result read = accessor.Read16(commonAperture, command.RelativeOffset);
                if (!read.Succeeded)
                {
                    VirtioNetQueueMmioSetupExecutionStatus status = read.Status == VirtioPciRegisterReadStatus.NotReady ? VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady : VirtioNetQueueMmioSetupExecutionStatus.RegisterReadFailed;
                    return VirtioNetQueueMmioSetupExecutionResult.Fail(status, setupPlan.Status, read.Status, VirtioPciRegisterWriteStatus.Success, command, index, commandsExecuted: index, read.Value);
                }

                if (read.Value != command.Value)
                {
                    return VirtioNetQueueMmioSetupExecutionResult.Fail(VirtioNetQueueMmioSetupExecutionStatus.RegisterReadMismatch, setupPlan.Status, read.Status, VirtioPciRegisterWriteStatus.Success, command, index, commandsExecuted: index, read.Value);
                }

                continue;
            }

            VirtioPciRegisterWriteStatus writeStatus;
            if (command.WidthBytes == sizeof(ushort))
            {
                writeStatus = accessor.Write16(commonAperture, command.RelativeOffset, (ushort)command.Value);
            }
            else if (command.WidthBytes == sizeof(ulong))
            {
                writeStatus = accessor.Write64(commonAperture, command.RelativeOffset, command.Value);
            }
            else
            {
                return VirtioNetQueueMmioSetupExecutionResult.Fail(VirtioNetQueueMmioSetupExecutionStatus.UnsupportedCommandWidth, setupPlan.Status, VirtioPciRegisterReadStatus.Success, VirtioPciRegisterWriteStatus.UnsupportedWidth, command, index, commandsExecuted: index);
            }

            if (writeStatus != VirtioPciRegisterWriteStatus.Success)
            {
                VirtioNetQueueMmioSetupExecutionStatus status = writeStatus == VirtioPciRegisterWriteStatus.NotReady ? VirtioNetQueueMmioSetupExecutionStatus.AccessorNotReady : VirtioNetQueueMmioSetupExecutionStatus.RegisterWriteFailed;
                return VirtioNetQueueMmioSetupExecutionResult.Fail(status, setupPlan.Status, VirtioPciRegisterReadStatus.Success, writeStatus, command, index, commandsExecuted: index);
            }
        }

        return VirtioNetQueueMmioSetupExecutionResult.Success(setupPlan, setupPlan.CommandCount);
    }
}
