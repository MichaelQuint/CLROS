namespace OS.Drivers;
public static class PciBarSizingProbePlanner
{
    public const ushort FirstBarRegisterOffset = 0x10;
    public const ushort BarRegisterStride = sizeof(uint);
    public const ushort Memory32CommandCount = 3;
    public const ushort Memory64CommandCount = 6;
    public const int MaximumCommandCount = PciBarDecoder.BarCount * Memory32CommandCount;
    public const bool RequiresDecodedBarDescriptors = true;
    public const bool ProducesConfigSpaceCommandPlan = true;
    public const bool PerformsLiveConfigWrites = false;
    public const bool PerformsLiveConfigReads = false;
    public const bool RestoresBars = false;
    public const bool MapsBars = false;
    public const bool PerformsMmio = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsBlockIo = false;
    private const uint AllOnes = uint.MaxValue;
    public static PciBarSizingProbePlanResult Plan(PciBarDescriptorSet bars, Span<PciBarSizingProbeCommand> destination)
    {
        PciBarSizingProbePlanStatus preflightStatus = Preflight(bars, out ushort requiredCommandCount, out byte memoryBarCount, out byte skippedBarCount, out byte failingBarIndex, out PciBarKind failingBarKind);
        if (preflightStatus != PciBarSizingProbePlanStatus.Success)
        {
            return PciBarSizingProbePlanResult.Create(preflightStatus, commandCount: 0, requiredCommandCount, memoryBarCount, skippedBarCount, failingBarIndex, failingBarKind);
        }

        if (destination.Length < requiredCommandCount)
        {
            return PciBarSizingProbePlanResult.Create(PciBarSizingProbePlanStatus.CommandDestinationTooSmall, commandCount: 0, requiredCommandCount, memoryBarCount, skippedBarCount, PciBarDecoder.NoBarIndex, default);
        }

        ushort commandIndex = 0;
        WriteCommandsForBar(bars.Bar0, destination, ref commandIndex);
        WriteCommandsForBar(bars.Bar1, destination, ref commandIndex);
        WriteCommandsForBar(bars.Bar2, destination, ref commandIndex);
        WriteCommandsForBar(bars.Bar3, destination, ref commandIndex);
        WriteCommandsForBar(bars.Bar4, destination, ref commandIndex);
        WriteCommandsForBar(bars.Bar5, destination, ref commandIndex);
        return PciBarSizingProbePlanResult.Create(PciBarSizingProbePlanStatus.Success, commandIndex, requiredCommandCount, memoryBarCount, skippedBarCount, PciBarDecoder.NoBarIndex, default);
    }

    private static PciBarSizingProbePlanStatus Preflight(PciBarDescriptorSet bars, out ushort requiredCommandCount, out byte memoryBarCount, out byte skippedBarCount, out byte failingBarIndex, out PciBarKind failingBarKind)
    {
        requiredCommandCount = 0;
        memoryBarCount = 0;
        skippedBarCount = 0;
        failingBarIndex = PciBarDecoder.NoBarIndex;
        failingBarKind = default;
        PciBarSizingProbePlanStatus status = Accumulate(bars.Bar0, ref requiredCommandCount, ref memoryBarCount, ref skippedBarCount, out failingBarIndex, out failingBarKind);
        if (status != PciBarSizingProbePlanStatus.Success)
        {
            return status;
        }

        status = Accumulate(bars.Bar1, ref requiredCommandCount, ref memoryBarCount, ref skippedBarCount, out failingBarIndex, out failingBarKind);
        if (status != PciBarSizingProbePlanStatus.Success)
        {
            return status;
        }

        status = Accumulate(bars.Bar2, ref requiredCommandCount, ref memoryBarCount, ref skippedBarCount, out failingBarIndex, out failingBarKind);
        if (status != PciBarSizingProbePlanStatus.Success)
        {
            return status;
        }

        status = Accumulate(bars.Bar3, ref requiredCommandCount, ref memoryBarCount, ref skippedBarCount, out failingBarIndex, out failingBarKind);
        if (status != PciBarSizingProbePlanStatus.Success)
        {
            return status;
        }

        status = Accumulate(bars.Bar4, ref requiredCommandCount, ref memoryBarCount, ref skippedBarCount, out failingBarIndex, out failingBarKind);
        if (status != PciBarSizingProbePlanStatus.Success)
        {
            return status;
        }

        return Accumulate(bars.Bar5, ref requiredCommandCount, ref memoryBarCount, ref skippedBarCount, out failingBarIndex, out failingBarKind);
    }

    private static PciBarSizingProbePlanStatus Accumulate(PciBarDescriptor descriptor, ref ushort requiredCommandCount, ref byte memoryBarCount, ref byte skippedBarCount, out byte failingBarIndex, out PciBarKind failingBarKind)
    {
        failingBarIndex = PciBarDecoder.NoBarIndex;
        failingBarKind = default;
        switch (descriptor.Kind)
        {
            case PciBarKind.Memory32:
                requiredCommandCount += Memory32CommandCount;
                memoryBarCount++;
                return PciBarSizingProbePlanStatus.Success;
            case PciBarKind.Memory64:
                requiredCommandCount += Memory64CommandCount;
                memoryBarCount++;
                return PciBarSizingProbePlanStatus.Success;
            case PciBarKind.Absent:
            case PciBarKind.Io:
            case PciBarKind.ConsumedByPrevious64BitBar:
                skippedBarCount++;
                return PciBarSizingProbePlanStatus.Success;
            default:
                failingBarIndex = descriptor.Index;
                failingBarKind = descriptor.Kind;
                return PciBarSizingProbePlanStatus.DescriptorMalformed;
        }
    }

    private static void WriteCommandsForBar(PciBarDescriptor descriptor, Span<PciBarSizingProbeCommand> destination, ref ushort commandIndex)
    {
        if (descriptor.Kind == PciBarKind.Memory32)
        {
            ushort registerOffset = GetBarRegisterOffset(descriptor.Index);
            Write(destination, ref commandIndex, PciBarSizingProbeCommandKind.WriteAllOnes, descriptor.Index, PciBarDecoder.NoBarIndex, registerOffset, AllOnes, isUpperPart: false, descriptor.Kind);
            Write(destination, ref commandIndex, PciBarSizingProbeCommandKind.ReadSizingReadback, descriptor.Index, PciBarDecoder.NoBarIndex, registerOffset, value: 0, isUpperPart: false, descriptor.Kind);
            Write(destination, ref commandIndex, PciBarSizingProbeCommandKind.RestoreOriginal, descriptor.Index, PciBarDecoder.NoBarIndex, registerOffset, descriptor.RawValue, isUpperPart: false, descriptor.Kind);
            return;
        }

        if (descriptor.Kind == PciBarKind.Memory64)
        {
            ushort lowRegisterOffset = GetBarRegisterOffset(descriptor.Index);
            ushort highRegisterOffset = GetBarRegisterOffset(descriptor.PairBarIndex);
            Write(destination, ref commandIndex, PciBarSizingProbeCommandKind.WriteAllOnes, descriptor.Index, descriptor.PairBarIndex, lowRegisterOffset, AllOnes, isUpperPart: false, descriptor.Kind);
            Write(destination, ref commandIndex, PciBarSizingProbeCommandKind.WriteAllOnes, descriptor.PairBarIndex, descriptor.Index, highRegisterOffset, AllOnes, isUpperPart: true, descriptor.Kind);
            Write(destination, ref commandIndex, PciBarSizingProbeCommandKind.ReadSizingReadback, descriptor.Index, descriptor.PairBarIndex, lowRegisterOffset, value: 0, isUpperPart: false, descriptor.Kind);
            Write(destination, ref commandIndex, PciBarSizingProbeCommandKind.ReadSizingReadback, descriptor.PairBarIndex, descriptor.Index, highRegisterOffset, value: 0, isUpperPart: true, descriptor.Kind);
            Write(destination, ref commandIndex, PciBarSizingProbeCommandKind.RestoreOriginal, descriptor.PairBarIndex, descriptor.Index, highRegisterOffset, descriptor.RawUpperValue, isUpperPart: true, descriptor.Kind);
            Write(destination, ref commandIndex, PciBarSizingProbeCommandKind.RestoreOriginal, descriptor.Index, descriptor.PairBarIndex, lowRegisterOffset, descriptor.RawValue, isUpperPart: false, descriptor.Kind);
        }
    }

    private static void Write(Span<PciBarSizingProbeCommand> destination, ref ushort commandIndex, PciBarSizingProbeCommandKind kind, byte barIndex, byte pairBarIndex, ushort registerOffset, uint value, bool isUpperPart, PciBarKind barKind)
    {
        destination[commandIndex] = new PciBarSizingProbeCommand(kind, commandIndex, barIndex, pairBarIndex, registerOffset, value, isUpperPart, barKind);
        commandIndex++;
    }

    private static ushort GetBarRegisterOffset(byte barIndex)
    {
        return (ushort)(FirstBarRegisterOffset + (barIndex * BarRegisterStride));
    }
}
