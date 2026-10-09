using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class PciBarSizingProbeAddressPlanTests
{

    [HostTest("drivers.pci.bar.sizeprobe.address binds 32bit command addresses", HostLane.Fast, 0)]
    private static bool TestBinds32BitCommandAddresses()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000008);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        PciEcamSegment segment = new PciEcamSegment(
            basePhysicalAddress: 0xE0000000,
            segmentGroup: 2,
            startBus: 0x20,
            endBus: 0x2F);
        Span<PciBarSizingProbeAddressCommand> commands =
            stackalloc PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];

        PciBarSizingProbeAddressPlanResult result =
            PciBarSizingProbeAddressPlanner.Plan(
                segment,
                bus: 0x24,
                device: 3,
                function: 5,
                bars,
                commands);

        ulong expectedPhysical = ExpectedAddress(segment, bus: 0x24, device: 3, function: 5, registerOffset: 0x10);
        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbeAddressPlanStatus.Success
            && result.ProbePlanStatus == PciBarSizingProbePlanStatus.Success
            && result.CommandCount == 3
            && result.RequiredCommandCount == 3
            && result.FailingCommandIndex == PciBarSizingProbeAddressPlanner.NoCommandIndex
            && IsAddressCommand(
                commands[0],
                PciBarSizingProbeCommandKind.WriteAllOnes,
                sequence: 0,
                barIndex: 0,
                pairBarIndex: PciBarDecoder.NoBarIndex,
                registerOffset: 0x10,
                value: uint.MaxValue,
                expectedPhysical,
                isUpperPart: false,
                PciBarKind.Memory32)
            && IsAddressCommand(
                commands[1],
                PciBarSizingProbeCommandKind.ReadSizingReadback,
                sequence: 1,
                barIndex: 0,
                pairBarIndex: PciBarDecoder.NoBarIndex,
                registerOffset: 0x10,
                value: 0,
                expectedPhysical,
                isUpperPart: false,
                PciBarKind.Memory32)
            && IsAddressCommand(
                commands[2],
                PciBarSizingProbeCommandKind.RestoreOriginal,
                sequence: 2,
                barIndex: 0,
                pairBarIndex: PciBarDecoder.NoBarIndex,
                registerOffset: 0x10,
                value: 0xFE000008,
                expectedPhysical,
                isUpperPart: false,
                PciBarKind.Memory32));
    }

    [HostTest("drivers.pci.bar.sizeprobe.address binds 64bit command addresses", HostLane.Fast, 1)]
    private static bool TestBinds64BitCommandAddresses()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x18, 0x8000000C);
        WriteUInt32(headerBytes, 0x1C, 0x00000001);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        PciEcamSegment segment = new PciEcamSegment(
            basePhysicalAddress: 0xD0000000,
            segmentGroup: 1,
            startBus: 8,
            endBus: 15);
        Span<PciBarSizingProbeAddressCommand> commands =
            stackalloc PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];

        PciBarSizingProbeAddressPlanResult result =
            PciBarSizingProbeAddressPlanner.Plan(
                segment,
                bus: 11,
                device: 2,
                function: 6,
                bars,
                commands);

        ulong lowPhysical = ExpectedAddress(segment, bus: 11, device: 2, function: 6, registerOffset: 0x18);
        ulong highPhysical = ExpectedAddress(segment, bus: 11, device: 2, function: 6, registerOffset: 0x1C);
        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.CommandCount == 6
            && result.RequiredCommandCount == 6
            && IsAddressCommand(
                commands[0],
                PciBarSizingProbeCommandKind.WriteAllOnes,
                sequence: 0,
                barIndex: 2,
                pairBarIndex: 3,
                registerOffset: 0x18,
                value: uint.MaxValue,
                lowPhysical,
                isUpperPart: false,
                PciBarKind.Memory64)
            && IsAddressCommand(
                commands[1],
                PciBarSizingProbeCommandKind.WriteAllOnes,
                sequence: 1,
                barIndex: 3,
                pairBarIndex: 2,
                registerOffset: 0x1C,
                value: uint.MaxValue,
                highPhysical,
                isUpperPart: true,
                PciBarKind.Memory64)
            && IsAddressCommand(
                commands[2],
                PciBarSizingProbeCommandKind.ReadSizingReadback,
                sequence: 2,
                barIndex: 2,
                pairBarIndex: 3,
                registerOffset: 0x18,
                value: 0,
                lowPhysical,
                isUpperPart: false,
                PciBarKind.Memory64)
            && IsAddressCommand(
                commands[3],
                PciBarSizingProbeCommandKind.ReadSizingReadback,
                sequence: 3,
                barIndex: 3,
                pairBarIndex: 2,
                registerOffset: 0x1C,
                value: 0,
                highPhysical,
                isUpperPart: true,
                PciBarKind.Memory64)
            && IsAddressCommand(
                commands[4],
                PciBarSizingProbeCommandKind.RestoreOriginal,
                sequence: 4,
                barIndex: 3,
                pairBarIndex: 2,
                registerOffset: 0x1C,
                value: 0x00000001,
                highPhysical,
                isUpperPart: true,
                PciBarKind.Memory64)
            && IsAddressCommand(
                commands[5],
                PciBarSizingProbeCommandKind.RestoreOriginal,
                sequence: 5,
                barIndex: 2,
                pairBarIndex: 3,
                registerOffset: 0x18,
                value: 0x8000000C,
                lowPhysical,
                isUpperPart: false,
                PciBarKind.Memory64));
    }

    [HostTest("drivers.pci.bar.sizeprobe.address propagates probe plan failure", HostLane.Fast, 2)]
    private static bool TestPropagatesProbePlanFailure()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x24, 0xFE000004);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeAddressCommand> commands =
            stackalloc PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];

        PciBarSizingProbeAddressPlanResult result =
            PciBarSizingProbeAddressPlanner.Plan(
                new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 0, endBus: 0),
                bus: 0,
                device: 0,
                function: 0,
                bars,
                commands);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbeAddressPlanStatus.ProbePlanFailed
            && result.ProbePlanStatus == PciBarSizingProbePlanStatus.DescriptorMalformed
            && result.CommandCount == 0
            && result.RequiredCommandCount == 0
            && result.FailingCommandIndex == PciBarSizingProbeAddressPlanner.NoCommandIndex
            && result.FailingBarIndex == 5
            && result.FailingBarKind == PciBarKind.Malformed
            && commands[0].Kind == default);
    }

    [HostTest("drivers.pci.bar.sizeprobe.address propagates ecam address failure", HostLane.Fast, 3)]
    private static bool TestPropagatesEcamAddressFailure()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000000);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        PciEcamSegment segment = new PciEcamSegment(
            basePhysicalAddress: 0xE0000000,
            segmentGroup: 0,
            startBus: 8,
            endBus: 12);
        Span<PciBarSizingProbeAddressCommand> commands =
            stackalloc PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];

        PciBarSizingProbeAddressPlanResult result =
            PciBarSizingProbeAddressPlanner.Plan(
                segment,
                bus: 7,
                device: 0,
                function: 0,
                bars,
                commands);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbeAddressPlanStatus.AddressPlanningFailed
            && result.ProbePlanStatus == PciBarSizingProbePlanStatus.Success
            && result.AddressStatus == PciEcamConfigurationSpaceStatus.BusOutsideSegment
            && result.AddressResult.Status == PciEcamConfigurationSpaceStatus.BusOutsideSegment
            && result.AddressResult.RegisterOffset == 0x10
            && result.RequiredCommandCount == 3
            && result.CommandCount == 0
            && result.FailingCommandIndex == 0
            && result.FailingBarIndex == 0
            && result.FailingRegisterOffset == 0x10
            && result.FailingBarKind == PciBarKind.Memory32
            && commands[0].Kind == default);
    }

    [HostTest("drivers.pci.bar.sizeprobe.address rejects short destination", HostLane.Fast, 4)]
    private static bool TestRejectsShortDestination()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000000);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeAddressCommand> commands =
            stackalloc PciBarSizingProbeAddressCommand[2];

        PciBarSizingProbeAddressPlanResult result =
            PciBarSizingProbeAddressPlanner.Plan(
                new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 0, endBus: 0),
                bus: 0,
                device: 0,
                function: 0,
                bars,
                commands);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbeAddressPlanStatus.DestinationTooSmall
            && result.ProbePlanStatus == PciBarSizingProbePlanStatus.Success
            && result.CommandCount == 0
            && result.RequiredCommandCount == 3
            && result.FailingCommandIndex == PciBarSizingProbeAddressPlanner.NoCommandIndex
            && result.FailingBarIndex == PciBarDecoder.NoBarIndex
            && commands[0].Kind == default
            && commands[1].Kind == default);
    }

    [HostTest("drivers.pci.bar.sizeprobe.address default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PciBarSizingProbeAddressPlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == PciBarSizingProbeAddressPlanStatus.Success
            && result.ProbePlanStatus == PciBarSizingProbePlanStatus.Success
            && !result.ProbePlan.IsInitialized
            && !result.ProbePlan.Succeeded
            && result.AddressStatus == PciEcamConfigurationSpaceStatus.Success
            && !result.AddressResult.IsInitialized
            && !result.AddressResult.Succeeded
            && result.CommandCount == 0
            && result.RequiredCommandCount == 0
            && result.FailingCommandIndex == 0
            && result.FailingBarIndex == 0
            && result.FailingRegisterOffset == 0
            && result.FailingBarKind == PciBarKind.Absent);
    }

    [HostTest("drivers.pci.bar.sizeprobe.address preserves no-live-config-access boundary", HostLane.Fast, 6)]
    private static bool TestPreservesNoLiveConfigAccessBoundary()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000000);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeAddressCommand> commands =
            stackalloc PciBarSizingProbeAddressCommand[PciBarSizingProbePlanner.MaximumCommandCount];

        PciBarSizingProbeAddressPlanResult result =
            PciBarSizingProbeAddressPlanner.Plan(
                new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 0, endBus: 0),
                bus: 0,
                device: 0,
                function: 0,
                bars,
                commands);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && PciBarSizingProbeAddressPlanner.RequiresDecodedBarDescriptors
            && PciBarSizingProbeAddressPlanner.RequiresCallerProvidedEcamSegment
            && PciBarSizingProbeAddressPlanner.RequiresCallerProvidedPciLocation
            && PciBarSizingProbeAddressPlanner.ProducesConfigSpaceCommandPlan
            && PciBarSizingProbeAddressPlanner.ProducesConfigSpaceAddressPlan
            && !PciBarSizingProbeAddressPlanner.PerformsLiveConfigWrites
            && !PciBarSizingProbeAddressPlanner.PerformsLiveConfigReads
            && !PciBarSizingProbeAddressPlanner.RestoresBars
            && !PciBarSizingProbeAddressPlanner.MapsEcam
            && !PciBarSizingProbeAddressPlanner.MapsBars
            && !PciBarSizingProbeAddressPlanner.PerformsMmio
            && !PciBarSizingProbeAddressPlanner.PerformsDmaMapping
            && !PciBarSizingProbeAddressPlanner.AllocatesDmaMemory
            && !PciBarSizingProbeAddressPlanner.HandlesInterrupts
            && !PciBarSizingProbeAddressPlanner.PerformsPacketIo
            && !PciBarSizingProbeAddressPlanner.PerformsBlockIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PciBarSizingProbeAddressPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PciBarSizingProbeAddressCommand>());
    }

    private static bool IsAddressCommand(
        PciBarSizingProbeAddressCommand command,
        PciBarSizingProbeCommandKind kind,
        ushort sequence,
        byte barIndex,
        byte pairBarIndex,
        ushort registerOffset,
        uint value,
        ulong physicalAddress,
        bool isUpperPart,
        PciBarKind barKind)
    {
        return command.Kind == kind
            && command.SequenceIndex == sequence
            && command.BarIndex == barIndex
            && command.PairBarIndex == pairBarIndex
            && command.RegisterOffset == registerOffset
            && command.AccessWidthBytes == sizeof(uint)
            && command.Value == value
            && command.IsUpperPart == isUpperPart
            && command.BarKind == barKind
            && command.AddressStatus == PciEcamConfigurationSpaceStatus.Success
            && command.AddressResult.RegisterOffset == registerOffset
            && command.AddressResult.AccessWidth == sizeof(uint)
            && command.PhysicalAddress == physicalAddress
            && command.EcamOffset == physicalAddress - command.AddressResult.Segment.BasePhysicalAddress;
    }

    private static ulong ExpectedAddress(
        PciEcamSegment segment,
        byte bus,
        byte device,
        byte function,
        ushort registerOffset)
    {
        return segment.BasePhysicalAddress +
            ((ulong)(bus - segment.StartBus) * PciEcamConfigurationSpace.BytesPerBus) +
            ((ulong)device * PciEcamConfigurationSpace.BytesPerDevice) +
            ((ulong)function * PciEcamConfigurationSpace.BytesPerFunction) +
            registerOffset;
    }

    private static PciBarDescriptorSet DecodeBars(byte[] headerBytes)
    {
        PciConfigurationHeaderResult header = PciConfigurationHeader.ParseType0(headerBytes);
        if (!header.Succeeded)
        {
            throw new InvalidOperationException("test header parse failed: " + header.Status);
        }

        return PciBarDecoder.Decode(header.Header).Bars;
    }

    private static byte[] CreateBaseHeader()
    {
        byte[] headerBytes = new byte[PciConfigurationHeader.Type0HeaderLength];
        WriteUInt16(headerBytes, 0x00, VirtioPciDeviceDiscovery.VirtioVendorId);
        WriteUInt16(headerBytes, 0x02, 0x1041);
        headerBytes[0x0E] = PciConfigurationHeader.Type0HeaderType;
        return headerBytes;
    }

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }
}
