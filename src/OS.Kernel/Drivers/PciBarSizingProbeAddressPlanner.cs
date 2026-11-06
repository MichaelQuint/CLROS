namespace OS.Drivers;

public static class PciBarSizingProbeAddressPlanner
{
    public const ushort NoCommandIndex = ushort.MaxValue;
    public const ushort NoRegisterOffset = ushort.MaxValue;
    public const bool RequiresDecodedBarDescriptors = true;
    public const bool RequiresCallerProvidedEcamSegment = true;
    public const bool RequiresCallerProvidedPciLocation = true;
    public const bool ProducesConfigSpaceCommandPlan = true;
    public const bool ProducesConfigSpaceAddressPlan = true;
    public const bool PerformsLiveConfigWrites = false;
    public const bool PerformsLiveConfigReads = false;
    public const bool RestoresBars = false;
    public const bool MapsEcam = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsBlockIo = false;
    public static PciBarSizingProbeAddressPlanResult Plan(PciEcamSegment segment, byte bus, byte device, byte function, PciBarDescriptorSet bars, Span<PciBarSizingProbeAddressCommand> destination)
    {
        Span<PciBarSizingProbeCommand> commands = stackalloc PciBarSizingProbeCommand[PciBarSizingProbePlanner.MaximumCommandCount];
        PciBarSizingProbePlanResult probePlan = PciBarSizingProbePlanner.Plan(bars, commands);
        if (!probePlan.Succeeded)
        {
            return PciBarSizingProbeAddressPlanResult.Create(PciBarSizingProbeAddressPlanStatus.ProbePlanFailed, probePlan, addressStatus: default, addressResult: default, commandCount: 0, requiredCommandCount: probePlan.RequiredCommandCount, failingCommandIndex: NoCommandIndex, failingBarIndex: probePlan.FailingBarIndex, failingRegisterOffset: NoRegisterOffset, failingBarKind: probePlan.FailingBarKind);
        }

        if (destination.Length < probePlan.CommandCount)
        {
            return PciBarSizingProbeAddressPlanResult.Create(PciBarSizingProbeAddressPlanStatus.DestinationTooSmall, probePlan, addressStatus: default, addressResult: default, commandCount: 0, requiredCommandCount: probePlan.CommandCount, failingCommandIndex: NoCommandIndex, failingBarIndex: PciBarDecoder.NoBarIndex, failingRegisterOffset: NoRegisterOffset, failingBarKind: default);
        }

        Span<PciEcamConfigurationAddressResult> addresses = stackalloc PciEcamConfigurationAddressResult[PciBarSizingProbePlanner.MaximumCommandCount];
        for (ushort index = 0; index < probePlan.CommandCount; index++)
        {
            PciBarSizingProbeCommand command = commands[index];
            PciEcamConfigurationAddressResult address = PciEcamConfigurationSpace.CalculateAddress(segment, bus, device, function, command.RegisterOffset, command.AccessWidthBytes);
            if (!address.Succeeded)
            {
                return PciBarSizingProbeAddressPlanResult.Create(PciBarSizingProbeAddressPlanStatus.AddressPlanningFailed, probePlan, address.Status, address, commandCount: 0, requiredCommandCount: probePlan.CommandCount, failingCommandIndex: index, failingBarIndex: command.BarIndex, failingRegisterOffset: command.RegisterOffset, failingBarKind: command.BarKind);
            }

            addresses[index] = address;
        }

        for (ushort index = 0; index < probePlan.CommandCount; index++)
        {
            destination[index] = new PciBarSizingProbeAddressCommand(commands[index], addresses[index]);
        }

        return PciBarSizingProbeAddressPlanResult.Create(PciBarSizingProbeAddressPlanStatus.Success, probePlan, PciEcamConfigurationSpaceStatus.Success, addressResult: default, commandCount: probePlan.CommandCount, requiredCommandCount: probePlan.CommandCount, failingCommandIndex: NoCommandIndex, failingBarIndex: PciBarDecoder.NoBarIndex, failingRegisterOffset: NoRegisterOffset, failingBarKind: default);
    }
}
