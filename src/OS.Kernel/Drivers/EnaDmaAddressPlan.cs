namespace OS.Drivers;
public static class EnaDmaAddressPlan
{
    public const bool AllocatesDmaMemory = false;
    public const bool MapsPages = false;
    public const bool ProgramsIommu = false;
    public const bool WritesMmioRegisters = false;
    public const bool RingsDoorbells = false;
    public const bool ConfiguresInterrupts = false;
    public const bool PerformsPacketIo = false;
    public static EnaDmaRegionValidationResult ValidateRegion(EnaDeviceAttributes deviceAttributes, EnaDmaPhysicalRegion region)
    {
        if (!TryGetMaximumAddress(deviceAttributes.PhysicalAddressWidth, out ulong maximumAddress))
        {
            return EnaDmaRegionValidationResult.Create(EnaDmaAddressPlanStatus.InvalidAddressWidth, deviceAttributes.PhysicalAddressWidth, maximumAddress, endExclusive: 0, region);
        }

        if (region.LengthBytes == 0)
        {
            return EnaDmaRegionValidationResult.Create(EnaDmaAddressPlanStatus.ZeroLength, deviceAttributes.PhysicalAddressWidth, maximumAddress, endExclusive: region.BasePhysicalAddress, region);
        }

        if (!IsPowerOfTwo(region.Alignment))
        {
            return EnaDmaRegionValidationResult.Create(EnaDmaAddressPlanStatus.InvalidAlignment, deviceAttributes.PhysicalAddressWidth, maximumAddress, endExclusive: 0, region);
        }

        if (!region.IsBaseAligned)
        {
            return EnaDmaRegionValidationResult.Create(EnaDmaAddressPlanStatus.UnalignedBaseAddress, deviceAttributes.PhysicalAddressWidth, maximumAddress, endExclusive: 0, region);
        }

        if (region.BasePhysicalAddress > ulong.MaxValue - region.LengthBytes)
        {
            return EnaDmaRegionValidationResult.Create(EnaDmaAddressPlanStatus.AddressOverflow, deviceAttributes.PhysicalAddressWidth, maximumAddress, endExclusive: 0, region);
        }

        ulong endExclusive = region.BasePhysicalAddress + region.LengthBytes;
        ulong lastAddress = endExclusive - 1UL;
        if (region.BasePhysicalAddress > maximumAddress || lastAddress > maximumAddress)
        {
            return EnaDmaRegionValidationResult.Create(EnaDmaAddressPlanStatus.AddressOutOfRange, deviceAttributes.PhysicalAddressWidth, maximumAddress, endExclusive, region);
        }

        return EnaDmaRegionValidationResult.Create(EnaDmaAddressPlanStatus.Success, deviceAttributes.PhysicalAddressWidth, maximumAddress, endExclusive, region);
    }

    public static EnaAdminQueueDmaMemoryPlanResult CreateAdminQueueMemoryPlan(EnaDeviceAttributes deviceAttributes, EnaAdminQueueLayoutPlanResult queueLayoutResult, ulong basePhysicalAddress, ulong lengthBytes)
    {
        if (!TryGetMaximumAddress(deviceAttributes.PhysicalAddressWidth, out ulong maximumAddress))
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(EnaDmaAddressPlanStatus.InvalidAddressWidth, queueLayoutResult.Status, EnaDmaRegionKind.ContiguousAdminQueueMemory, deviceAttributes.PhysicalAddressWidth, queueLayoutResult.TotalRequiredBytes, lengthBytes, maximumAddress);
        }

        if (!queueLayoutResult.Succeeded)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(EnaDmaAddressPlanStatus.QueueLayoutRejected, queueLayoutResult.Status, EnaDmaRegionKind.ContiguousAdminQueueMemory, deviceAttributes.PhysicalAddressWidth, queueLayoutResult.TotalRequiredBytes, lengthBytes, maximumAddress);
        }

        EnaAdminQueueLayoutPlan queueLayout = queueLayoutResult.Plan;
        if (lengthBytes < queueLayout.TotalRequiredBytes)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(EnaDmaAddressPlanStatus.QueueMemoryTooSmall, queueLayoutResult.Status, EnaDmaRegionKind.ContiguousAdminQueueMemory, deviceAttributes.PhysicalAddressWidth, queueLayout.TotalRequiredBytes, lengthBytes, maximumAddress);
        }

        EnaDmaPhysicalRegion contiguousRegion = new EnaDmaPhysicalRegion(EnaDmaRegionKind.ContiguousAdminQueueMemory, basePhysicalAddress, lengthBytes, queueLayout.BaseAlignment);
        EnaDmaRegionValidationResult contiguousValidation = ValidateRegion(deviceAttributes, contiguousRegion);
        if (!contiguousValidation.Succeeded)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(contiguousValidation.Status, queueLayoutResult.Status, EnaDmaRegionKind.ContiguousAdminQueueMemory, deviceAttributes.PhysicalAddressWidth, queueLayout.TotalRequiredBytes, lengthBytes, maximumAddress);
        }

        if (!TryAdd(basePhysicalAddress, queueLayout.AdminQueue.Offset, out ulong adminQueueBase))
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(EnaDmaAddressPlanStatus.AddressOverflow, queueLayoutResult.Status, EnaDmaRegionKind.AdminSubmissionQueue, deviceAttributes.PhysicalAddressWidth, queueLayout.AdminQueue.Length, queueLayout.AdminQueue.Length, maximumAddress);
        }

        EnaDmaPhysicalRegion adminQueueRegion = new EnaDmaPhysicalRegion(EnaDmaRegionKind.AdminSubmissionQueue, adminQueueBase, queueLayout.AdminQueue.Length, queueLayout.AdminQueue.Alignment);
        EnaDmaRegionValidationResult adminQueueValidation = ValidateRegion(deviceAttributes, adminQueueRegion);
        if (!adminQueueValidation.Succeeded)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(adminQueueValidation.Status, queueLayoutResult.Status, EnaDmaRegionKind.AdminSubmissionQueue, deviceAttributes.PhysicalAddressWidth, queueLayout.AdminQueue.Length, queueLayout.AdminQueue.Length, maximumAddress);
        }

        if (!TryAdd(basePhysicalAddress, queueLayout.AdminCompletionQueue.Offset, out ulong adminCompletionQueueBase))
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(EnaDmaAddressPlanStatus.AddressOverflow, queueLayoutResult.Status, EnaDmaRegionKind.AdminCompletionQueue, deviceAttributes.PhysicalAddressWidth, queueLayout.AdminCompletionQueue.Length, queueLayout.AdminCompletionQueue.Length, maximumAddress);
        }

        EnaDmaPhysicalRegion adminCompletionQueueRegion = new EnaDmaPhysicalRegion(EnaDmaRegionKind.AdminCompletionQueue, adminCompletionQueueBase, queueLayout.AdminCompletionQueue.Length, queueLayout.AdminCompletionQueue.Alignment);
        EnaDmaRegionValidationResult adminCompletionQueueValidation = ValidateRegion(deviceAttributes, adminCompletionQueueRegion);
        if (!adminCompletionQueueValidation.Succeeded)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(adminCompletionQueueValidation.Status, queueLayoutResult.Status, EnaDmaRegionKind.AdminCompletionQueue, deviceAttributes.PhysicalAddressWidth, queueLayout.AdminCompletionQueue.Length, queueLayout.AdminCompletionQueue.Length, maximumAddress);
        }

        EnaAdminQueueDmaMemoryPlan plan = new EnaAdminQueueDmaMemoryPlan(contiguousRegion, adminQueueRegion, adminCompletionQueueRegion, queueLayout);
        return EnaAdminQueueDmaMemoryPlanResult.Success(deviceAttributes, maximumAddress, plan);
    }

    private static bool TryGetMaximumAddress(uint physicalAddressWidth, out ulong maximumAddress)
    {
        if (physicalAddressWidth < EnaDeviceAttributesParser.MinimumAddressWidth || physicalAddressWidth > EnaDeviceAttributesParser.MaximumAddressWidth)
        {
            maximumAddress = 0;
            return false;
        }

        maximumAddress = physicalAddressWidth == 64 ? ulong.MaxValue : (1UL << (int)physicalAddressWidth) - 1UL;
        return true;
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool TryAdd(ulong left, uint right, out ulong result)
    {
        if (left > ulong.MaxValue - right)
        {
            result = 0;
            return false;
        }

        result = left + right;
        return true;
    }
}
