using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class PciBarSizingProbePlanTests
{

    [HostTest("drivers.pci.bar.sizeprobe plans 32bit mmio sequence", HostLane.Fast, 0)]
    private static bool TestPlans32BitMmioSequence()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000008);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeCommand> commands = stackalloc PciBarSizingProbeCommand[3];

        PciBarSizingProbePlanResult result =
            PciBarSizingProbePlanner.Plan(bars, commands);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.CommandCount == 3
            && result.RequiredCommandCount == 3
            && result.MemoryBarCount == 1
            && result.SkippedBarCount == 5
            && commands[0].Kind == PciBarSizingProbeCommandKind.WriteAllOnes
            && commands[0].SequenceIndex == 0
            && commands[0].BarIndex == 0
            && commands[0].PairBarIndex == PciBarDecoder.NoBarIndex
            && commands[0].RegisterOffset == 0x10
            && commands[0].AccessWidthBytes == sizeof(uint)
            && commands[0].Value == uint.MaxValue
            && !commands[0].IsUpperPart
            && commands[0].BarKind == PciBarKind.Memory32
            && commands[1].Kind == PciBarSizingProbeCommandKind.ReadSizingReadback
            && commands[1].BarIndex == 0
            && commands[1].RegisterOffset == 0x10
            && commands[1].Value == 0
            && commands[2].Kind == PciBarSizingProbeCommandKind.RestoreOriginal
            && commands[2].BarIndex == 0
            && commands[2].RegisterOffset == 0x10
            && commands[2].Value == 0xFE000008);
    }

    [HostTest("drivers.pci.bar.sizeprobe plans 64bit mmio sequence", HostLane.Fast, 1)]
    private static bool TestPlans64BitMmioSequence()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x18, 0x8000000C);
        WriteUInt32(headerBytes, 0x1C, 0x00000001);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeCommand> commands = stackalloc PciBarSizingProbeCommand[6];

        PciBarSizingProbePlanResult result =
            PciBarSizingProbePlanner.Plan(bars, commands);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.CommandCount == 6
            && result.RequiredCommandCount == 6
            && result.MemoryBarCount == 1
            && result.SkippedBarCount == 5
            && IsCommand(
                commands[0],
                PciBarSizingProbeCommandKind.WriteAllOnes,
                sequence: 0,
                barIndex: 2,
                pairBarIndex: 3,
                registerOffset: 0x18,
                value: uint.MaxValue,
                isUpperPart: false)
            && IsCommand(
                commands[1],
                PciBarSizingProbeCommandKind.WriteAllOnes,
                sequence: 1,
                barIndex: 3,
                pairBarIndex: 2,
                registerOffset: 0x1C,
                value: uint.MaxValue,
                isUpperPart: true)
            && IsCommand(
                commands[2],
                PciBarSizingProbeCommandKind.ReadSizingReadback,
                sequence: 2,
                barIndex: 2,
                pairBarIndex: 3,
                registerOffset: 0x18,
                value: 0,
                isUpperPart: false)
            && IsCommand(
                commands[3],
                PciBarSizingProbeCommandKind.ReadSizingReadback,
                sequence: 3,
                barIndex: 3,
                pairBarIndex: 2,
                registerOffset: 0x1C,
                value: 0,
                isUpperPart: true)
            && IsCommand(
                commands[4],
                PciBarSizingProbeCommandKind.RestoreOriginal,
                sequence: 4,
                barIndex: 3,
                pairBarIndex: 2,
                registerOffset: 0x1C,
                value: 0x00000001,
                isUpperPart: true)
            && IsCommand(
                commands[5],
                PciBarSizingProbeCommandKind.RestoreOriginal,
                sequence: 5,
                barIndex: 2,
                pairBarIndex: 3,
                registerOffset: 0x18,
                value: 0x8000000C,
                isUpperPart: false));
    }

    [HostTest("drivers.pci.bar.sizeprobe skips absent io consumed bars", HostLane.Fast, 2)]
    private static bool TestSkipsAbsentIoConsumedBars()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x14, 0x0000C001);
        WriteUInt32(headerBytes, 0x18, 0x90000004);
        WriteUInt32(headerBytes, 0x1C, 0);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeCommand> commands = stackalloc PciBarSizingProbeCommand[6];

        PciBarSizingProbePlanResult result =
            PciBarSizingProbePlanner.Plan(bars, commands);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.CommandCount == 6
            && result.RequiredCommandCount == 6
            && result.MemoryBarCount == 1
            && result.SkippedBarCount == 5
            && bars.Bar0.Kind == PciBarKind.Absent
            && bars.Bar1.Kind == PciBarKind.Io
            && bars.Bar3.Kind == PciBarKind.ConsumedByPrevious64BitBar
            && commands[0].BarIndex == 2
            && commands[1].BarIndex == 3
            && commands[2].BarIndex == 2
            && commands[3].BarIndex == 3
            && commands[4].BarIndex == 3
            && commands[5].BarIndex == 2);
    }

    [HostTest("drivers.pci.bar.sizeprobe rejects malformed descriptor", HostLane.Fast, 3)]
    private static bool TestRejectsMalformedDescriptor()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x24, 0xFE000004);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeCommand> commands = stackalloc PciBarSizingProbeCommand[6];

        PciBarSizingProbePlanResult result =
            PciBarSizingProbePlanner.Plan(bars, commands);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbePlanStatus.DescriptorMalformed
            && result.CommandCount == 0
            && result.FailingBarIndex == 5
            && result.FailingBarKind == PciBarKind.Malformed
            && result.RequiredCommandCount == 0
            && result.MemoryBarCount == 0
            && result.SkippedBarCount == 5);
    }

    [HostTest("drivers.pci.bar.sizeprobe rejects short command destination", HostLane.Fast, 4)]
    private static bool TestRejectsShortCommandDestination()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000000);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeCommand> commands = stackalloc PciBarSizingProbeCommand[2];

        PciBarSizingProbePlanResult result =
            PciBarSizingProbePlanner.Plan(bars, commands);

        return HostCheck.Result(!result.Succeeded
            && result.IsInitialized
            && result.Status == PciBarSizingProbePlanStatus.CommandDestinationTooSmall
            && result.CommandCount == 0
            && result.RequiredCommandCount == 3
            && result.MemoryBarCount == 1
            && result.SkippedBarCount == 5
            && result.FailingBarIndex == PciBarDecoder.NoBarIndex
            && commands[0].Kind == default
            && commands[1].Kind == default);
    }

    [HostTest("drivers.pci.bar.sizeprobe default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PciBarSizingProbePlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == PciBarSizingProbePlanStatus.Success
            && result.CommandCount == 0
            && result.RequiredCommandCount == 0
            && result.MemoryBarCount == 0
            && result.SkippedBarCount == 0
            && result.FailingBarIndex == 0
            && result.FailingBarKind == PciBarKind.Absent);
    }

    [HostTest("drivers.pci.bar.sizeprobe preserves no-live-config-access boundary", HostLane.Fast, 6)]
    private static bool TestPreservesNoLiveConfigAccessBoundary()
    {
        byte[] headerBytes = CreateBaseHeader();
        WriteUInt32(headerBytes, 0x10, 0xFE000000);
        PciBarDescriptorSet bars = DecodeBars(headerBytes);
        Span<PciBarSizingProbeCommand> commands = stackalloc PciBarSizingProbeCommand[3];

        PciBarSizingProbePlanResult result =
            PciBarSizingProbePlanner.Plan(bars, commands);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && PciBarSizingProbePlanner.RequiresDecodedBarDescriptors
            && PciBarSizingProbePlanner.ProducesConfigSpaceCommandPlan
            && !PciBarSizingProbePlanner.PerformsLiveConfigWrites
            && !PciBarSizingProbePlanner.PerformsLiveConfigReads
            && !PciBarSizingProbePlanner.RestoresBars
            && !PciBarSizingProbePlanner.MapsBars
            && !PciBarSizingProbePlanner.PerformsMmio
            && !PciBarSizingProbePlanner.PerformsDmaMapping
            && !PciBarSizingProbePlanner.AllocatesDmaMemory
            && !PciBarSizingProbePlanner.HandlesInterrupts
            && !PciBarSizingProbePlanner.PerformsPacketIo
            && !PciBarSizingProbePlanner.PerformsBlockIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PciBarSizingProbePlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PciBarSizingProbeCommand>());
    }

    private static bool IsCommand(
        PciBarSizingProbeCommand command,
        PciBarSizingProbeCommandKind kind,
        ushort sequence,
        byte barIndex,
        byte pairBarIndex,
        ushort registerOffset,
        uint value,
        bool isUpperPart)
    {
        return command.Kind == kind
            && command.SequenceIndex == sequence
            && command.BarIndex == barIndex
            && command.PairBarIndex == pairBarIndex
            && command.RegisterOffset == registerOffset
            && command.AccessWidthBytes == sizeof(uint)
            && command.Value == value
            && command.IsUpperPart == isUpperPart
            && command.BarKind == PciBarKind.Memory64;
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
