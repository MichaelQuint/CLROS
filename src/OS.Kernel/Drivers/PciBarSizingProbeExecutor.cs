namespace OS.Drivers;

public static class PciBarSizingProbeExecutor
{
    public const int RequiredReadbackCount = PciBarDecoder.BarCount;
    public const int NoCommandIndex = -1;
    public const ushort NoRegisterOffset = ushort.MaxValue;
    public const bool RequiresAddressPlan = true;
    public const bool RequiresInjectedConfigAccessor = true;
    public const bool UsesCallerOwnedReadbackBuffer = true;
    public const bool InvokesInjectedConfigAccessor = true;
    public const bool ExecutesRestoreCommandsAgainstInjectedAccessor = true;
    public const bool PerformsLiveConfigWrites = false;
    public const bool PerformsLiveConfigReads = false;
    public const bool RestoresHardwareBars = false;
    public const bool MapsEcam = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsBlockIo = false;
    public static PciBarSizingProbeExecutionResult Execute(PciBarSizingProbeAddressPlanResult addressPlan, ReadOnlySpan<PciBarSizingProbeAddressCommand> commands, IPciConfigDwordAccessor? accessor, Span<uint> sizingReadbacks)
    {
        if (!addressPlan.Succeeded)
        {
            return CreateFailure(PciBarSizingProbeExecutionStatus.AddressPlanFailed, addressPlan, PciConfigDwordAccessStatus.Success, commands.Length, executedCommandCount: 0, sizingReadbacks.Length, readbackCount: 0, command: default, NoCommandIndex, value: 0);
        }

        if (commands.Length < addressPlan.CommandCount)
        {
            return CreateFailure(PciBarSizingProbeExecutionStatus.CommandSourceTooShort, addressPlan, PciConfigDwordAccessStatus.Success, commands.Length, executedCommandCount: 0, sizingReadbacks.Length, readbackCount: 0, command: default, NoCommandIndex, value: 0);
        }

        if (sizingReadbacks.Length < RequiredReadbackCount)
        {
            return CreateFailure(PciBarSizingProbeExecutionStatus.ReadbackDestinationTooSmall, addressPlan, PciConfigDwordAccessStatus.Success, commands.Length, executedCommandCount: 0, sizingReadbacks.Length, readbackCount: 0, command: default, NoCommandIndex, value: 0);
        }

        if (accessor is null)
        {
            return CreateFailure(PciBarSizingProbeExecutionStatus.MissingAccessor, addressPlan, PciConfigDwordAccessStatus.NotReady, commands.Length, executedCommandCount: 0, sizingReadbacks.Length, readbackCount: 0, command: default, NoCommandIndex, value: 0);
        }

        int executedCommandCount = 0;
        int readbackCount = 0;
        for (int index = 0; index < addressPlan.CommandCount; index++)
        {
            PciBarSizingProbeAddressCommand command = commands[index];
            if (!IsCommandValid(command, index))
            {
                return CreateFailure(PciBarSizingProbeExecutionStatus.CommandMalformed, addressPlan, PciConfigDwordAccessStatus.Success, commands.Length, executedCommandCount, sizingReadbacks.Length, readbackCount, command, index, value: 0);
            }

            if (command.Kind == PciBarSizingProbeCommandKind.ReadSizingReadback)
            {
                PciConfigDwordReadResult read = accessor.Read32(command.AddressResult);
                if (!read.Succeeded)
                {
                    return CreateFailure(PciBarSizingProbeExecutionStatus.ReadFailed, addressPlan, read.Status, commands.Length, executedCommandCount, sizingReadbacks.Length, readbackCount, command, index, read.Value);
                }

                sizingReadbacks[command.BarIndex] = read.Value;
                readbackCount++;
                executedCommandCount++;
                continue;
            }

            if (command.Kind == PciBarSizingProbeCommandKind.WriteAllOnes || command.Kind == PciBarSizingProbeCommandKind.RestoreOriginal)
            {
                PciConfigDwordAccessStatus writeStatus = accessor.Write32(command.AddressResult, command.Value);
                if (writeStatus != PciConfigDwordAccessStatus.Success)
                {
                    return CreateFailure(PciBarSizingProbeExecutionStatus.WriteFailed, addressPlan, writeStatus, commands.Length, executedCommandCount, sizingReadbacks.Length, readbackCount, command, index, command.Value);
                }

                executedCommandCount++;
                continue;
            }

            return CreateFailure(PciBarSizingProbeExecutionStatus.CommandMalformed, addressPlan, PciConfigDwordAccessStatus.Success, commands.Length, executedCommandCount, sizingReadbacks.Length, readbackCount, command, index, value: 0);
        }

        return PciBarSizingProbeExecutionResult.Create(PciBarSizingProbeExecutionStatus.Success, addressPlan, PciConfigDwordAccessStatus.Success, requiredCommandCount: addressPlan.CommandCount, availableCommandCount: commands.Length, executedCommandCount, RequiredReadbackCount, availableReadbackCount: sizingReadbacks.Length, readbackCount, NoCommandIndex, PciBarDecoder.NoBarIndex, NoRegisterOffset, failingCommandKind: default, failingPhysicalAddress: 0, value: 0);
    }

    private static bool IsCommandValid(PciBarSizingProbeAddressCommand command, int expectedSequenceIndex)
    {
        return command.AddressResult.Succeeded && command.SequenceIndex == expectedSequenceIndex && command.BarIndex < PciBarDecoder.BarCount && command.AccessWidthBytes == sizeof(uint) && command.AddressResult.AccessWidth == sizeof(uint) && command.AddressResult.RegisterOffset == command.RegisterOffset;
    }

    private static PciBarSizingProbeExecutionResult CreateFailure(PciBarSizingProbeExecutionStatus status, PciBarSizingProbeAddressPlanResult addressPlan, PciConfigDwordAccessStatus accessStatus, int availableCommandCount, int executedCommandCount, int availableReadbackCount, int readbackCount, PciBarSizingProbeAddressCommand command, int failingCommandIndex, uint value)
    {
        return PciBarSizingProbeExecutionResult.Create(status, addressPlan, accessStatus, requiredCommandCount: addressPlan.CommandCount, availableCommandCount, executedCommandCount, RequiredReadbackCount, availableReadbackCount, readbackCount, failingCommandIndex, failingCommandIndex == NoCommandIndex ? PciBarDecoder.NoBarIndex : command.BarIndex, failingCommandIndex == NoCommandIndex ? NoRegisterOffset : command.RegisterOffset, failingCommandIndex == NoCommandIndex ? default : command.Kind, failingCommandIndex == NoCommandIndex ? 0 : command.PhysicalAddress, value);
    }
}
