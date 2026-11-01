namespace OS.Drivers;
public static class PciBarSizingPlanner
{
    public const byte RequiredReadbackCount = PciBarDecoder.BarCount;
    public const bool RequiresCallerOwnedOriginalBarSnapshot = true;
    public const bool RequiresCallerOwnedSizingReadbacks = true;
    public const bool ProducesVirtioApertureValidatorInput = true;
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
    public static PciBarSizingPlanResult Plan(PciBarDescriptorSet bars, ReadOnlySpan<uint> sizingReadbacks)
    {
        if (sizingReadbacks.Length < RequiredReadbackCount)
        {
            PciBarSizeDescriptorSet shortResult = CreateSet(CreateFailureDescriptor(0, default, PciBarSizeDescriptorStatus.SourceTooShort, 0, 0), default, default, default, default, default);
            return PciBarSizingPlanResult.Create(PciBarSizingPlanStatus.SourceTooShort, failingBarIndex: 0, PciBarSizeDescriptorStatus.SourceTooShort, shortResult);
        }

        PciBarSizeDescriptor bar0 = PlanOne(bars.Bar0, sizingReadbacks, out PciBarSizingPlanStatus status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar0);
        }

        PciBarSizeDescriptor bar1 = PlanOne(bars.Bar1, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar1, bar0);
        }

        PciBarSizeDescriptor bar2 = PlanOne(bars.Bar2, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar2, bar0, bar1);
        }

        PciBarSizeDescriptor bar3 = PlanOne(bars.Bar3, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar3, bar0, bar1, bar2);
        }

        PciBarSizeDescriptor bar4 = PlanOne(bars.Bar4, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar4, bar0, bar1, bar2, bar3);
        }

        PciBarSizeDescriptor bar5 = PlanOne(bars.Bar5, sizingReadbacks, out status);
        if (status != PciBarSizingPlanStatus.Success)
        {
            return CreateFailure(status, bar5, bar0, bar1, bar2, bar3, bar4);
        }

        return PciBarSizingPlanResult.Create(PciBarSizingPlanStatus.Success, PciBarDecoder.NoBarIndex, PciBarSizeDescriptorStatus.Success, CreateSet(bar0, bar1, bar2, bar3, bar4, bar5));
    }

    private static PciBarSizeDescriptor PlanOne(PciBarDescriptor descriptor, ReadOnlySpan<uint> sizingReadbacks, out PciBarSizingPlanStatus status)
    {
        status = PciBarSizingPlanStatus.Success;
        uint rawReadback = sizingReadbacks[descriptor.Index];
        switch (descriptor.Kind)
        {
            case PciBarKind.Absent:
                return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.Absent, rawReadback, rawUpperReadback: 0, sizeBytes: 0);
            case PciBarKind.Io:
                return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.IoBarNotSized, rawReadback, rawUpperReadback: 0, sizeBytes: 0);
            case PciBarKind.ConsumedByPrevious64BitBar:
                return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.ConsumedByPrevious64BitBar, rawReadback, rawUpperReadback: 0, sizeBytes: 0);
            case PciBarKind.Memory32:
                return PlanMemory32(descriptor, rawReadback, out status);
            case PciBarKind.Memory64:
                return PlanMemory64(descriptor, sizingReadbacks, rawReadback, out status);
            default:
                status = PciBarSizingPlanStatus.DescriptorMalformed;
                return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.DescriptorMalformed, rawReadback, rawUpperReadback: 0, sizeBytes: 0);
        }
    }

    private static PciBarSizeDescriptor PlanMemory32(PciBarDescriptor descriptor, uint rawReadback, out PciBarSizingPlanStatus status)
    {
        uint readbackFlags = rawReadback & ~PciBarDecoder.MemoryBaseAddressMask;
        uint descriptorFlags = descriptor.RawValue & ~PciBarDecoder.MemoryBaseAddressMask;
        uint addressMask = rawReadback & PciBarDecoder.MemoryBaseAddressMask;
        ulong size = unchecked((uint)(~addressMask + 1U));
        if (readbackFlags != descriptorFlags || addressMask == 0 || !IsPowerOfTwo(size))
        {
            status = PciBarSizingPlanStatus.MalformedReadback;
            return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.MalformedReadback, rawReadback, rawUpperReadback: 0, sizeBytes: 0);
        }

        if (!IsAligned(descriptor.BaseAddressBits, size))
        {
            status = PciBarSizingPlanStatus.BaseAlignmentMismatch;
            return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.BaseAlignmentMismatch, rawReadback, rawUpperReadback: 0, sizeBytes: size);
        }

        status = PciBarSizingPlanStatus.Success;
        return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.Success, rawReadback, rawUpperReadback: 0, size);
    }

    private static PciBarSizeDescriptor PlanMemory64(PciBarDescriptor descriptor, ReadOnlySpan<uint> sizingReadbacks, uint rawReadback, out PciBarSizingPlanStatus status)
    {
        uint rawUpperReadback = sizingReadbacks[descriptor.PairBarIndex];
        uint readbackFlags = rawReadback & ~PciBarDecoder.MemoryBaseAddressMask;
        uint descriptorFlags = descriptor.RawValue & ~PciBarDecoder.MemoryBaseAddressMask;
        ulong addressMask = ((ulong)rawUpperReadback << 32) | (rawReadback & PciBarDecoder.MemoryBaseAddressMask);
        ulong size = unchecked(~addressMask + 1UL);
        if (readbackFlags != descriptorFlags || addressMask == 0 || size == 0 || !IsPowerOfTwo(size))
        {
            status = PciBarSizingPlanStatus.MalformedReadback;
            return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.MalformedReadback, rawReadback, rawUpperReadback, sizeBytes: 0);
        }

        if (!IsAligned(descriptor.BaseAddressBits, size))
        {
            status = PciBarSizingPlanStatus.BaseAlignmentMismatch;
            return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.BaseAlignmentMismatch, rawReadback, rawUpperReadback, size);
        }

        status = PciBarSizingPlanStatus.Success;
        return CreateDescriptor(descriptor, PciBarSizeDescriptorStatus.Success, rawReadback, rawUpperReadback, size);
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool IsAligned(ulong value, ulong alignment)
    {
        return alignment != 0 && (value & (alignment - 1)) == 0;
    }

    private static PciBarSizeDescriptor CreateDescriptor(PciBarDescriptor descriptor, PciBarSizeDescriptorStatus status, uint rawReadback, uint rawUpperReadback, ulong sizeBytes)
    {
        return new PciBarSizeDescriptor(descriptor.Index, descriptor.Kind, status, rawReadback, rawUpperReadback, descriptor.BaseAddressBits, sizeBytes, descriptor.IsPrefetchable, descriptor.ConsumesNextSlot, descriptor.PairBarIndex);
    }

    private static PciBarSizeDescriptor CreateFailureDescriptor(byte index, PciBarKind kind, PciBarSizeDescriptorStatus status, uint rawReadback, uint rawUpperReadback)
    {
        return new PciBarSizeDescriptor(index, kind, status, rawReadback, rawUpperReadback, baseAddressBits: 0, sizeBytes: 0, isPrefetchable: false, consumesNextSlot: false, PciBarDecoder.NoBarIndex);
    }

    private static PciBarSizingPlanResult CreateFailure(PciBarSizingPlanStatus status, PciBarSizeDescriptor failing, PciBarSizeDescriptor bar0 = default, PciBarSizeDescriptor bar1 = default, PciBarSizeDescriptor bar2 = default, PciBarSizeDescriptor bar3 = default, PciBarSizeDescriptor bar4 = default)
    {
        PciBarSizeDescriptorSet set = CreateSet(failing.Index == 0 ? failing : bar0, failing.Index == 1 ? failing : bar1, failing.Index == 2 ? failing : bar2, failing.Index == 3 ? failing : bar3, failing.Index == 4 ? failing : bar4, failing.Index == 5 ? failing : default);
        return PciBarSizingPlanResult.Create(status, failing.Index, failing.Status, set);
    }

    private static PciBarSizeDescriptorSet CreateSet(PciBarSizeDescriptor bar0, PciBarSizeDescriptor bar1, PciBarSizeDescriptor bar2, PciBarSizeDescriptor bar3, PciBarSizeDescriptor bar4, PciBarSizeDescriptor bar5)
    {
        return new PciBarSizeDescriptorSet(bar0, bar1, bar2, bar3, bar4, bar5);
    }
}
