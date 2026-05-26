using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;

internal static class EnaDmaAddressPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.ena.dma validates address width", TestValidatesAddressWidth, ref failures);
        Run("drivers.ena.dma rejects overflow", TestRejectsOverflow, ref failures);
        Run("drivers.ena.dma validates queue regions", TestValidatesQueueRegions, ref failures);
        Run("drivers.ena.dma rejects invalid regions", TestRejectsInvalidRegions, ref failures);
        Run("drivers.ena.dma rejects default results", TestRejectsDefaultResults, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ENA DMA address plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestValidatesAddressWidth()
    {
        EnaDmaRegionValidationResult valid32 = EnaDmaAddressPlan.ValidateRegion(
            CreateAttributes(32),
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.PacketBuffer,
                basePhysicalAddress: 0xFFFF_F000UL,
                lengthBytes: 0x1000,
                alignment: 4096));
        EnaDmaRegionValidationResult valid48 = EnaDmaAddressPlan.ValidateRegion(
            CreateAttributes(48),
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.RxRing,
                basePhysicalAddress: 0x0000_FFFF_FFFF_E000UL,
                lengthBytes: 0x1000,
                alignment: 4096));
        EnaDmaRegionValidationResult valid64 = EnaDmaAddressPlan.ValidateRegion(
            CreateAttributes(64),
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.TxRing,
                basePhysicalAddress: 0xFFFF_FFFF_FFFF_E000UL,
                lengthBytes: 0x1000,
                alignment: 4096));
        EnaDmaRegionValidationResult invalidLow = EnaDmaAddressPlan.ValidateRegion(
            CreateAttributes(31),
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.Generic,
                basePhysicalAddress: 0x1000,
                lengthBytes: 0x1000,
                alignment: 4096));
        EnaDmaRegionValidationResult invalidHigh = EnaDmaAddressPlan.ValidateRegion(
            CreateAttributes(65),
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.Generic,
                basePhysicalAddress: 0x1000,
                lengthBytes: 0x1000,
                alignment: 4096));
        EnaDmaRegionValidationResult outOfRange32 = EnaDmaAddressPlan.ValidateRegion(
            CreateAttributes(32),
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.PacketBuffer,
                basePhysicalAddress: 0x0000_0001_0000_0000UL,
                lengthBytes: 0x1000,
                alignment: 4096));

        return valid32.Succeeded
            && valid32.Status == EnaDmaAddressPlanStatus.Success
            && valid32.PhysicalAddressWidth == 32
            && valid32.MaximumAddress == 0xFFFF_FFFFUL
            && valid32.EndExclusive == 0x0000_0001_0000_0000UL
            && valid48.Succeeded
            && valid48.MaximumAddress == 0x0000_FFFF_FFFF_FFFFUL
            && valid48.EndExclusive == 0x0000_FFFF_FFFF_F000UL
            && valid64.Succeeded
            && valid64.MaximumAddress == ulong.MaxValue
            && valid64.EndExclusive == 0xFFFF_FFFF_FFFF_F000UL
            && !invalidLow.Succeeded
            && invalidLow.Status == EnaDmaAddressPlanStatus.InvalidAddressWidth
            && invalidLow.PhysicalAddressWidth == 31
            && !invalidHigh.Succeeded
            && invalidHigh.Status == EnaDmaAddressPlanStatus.InvalidAddressWidth
            && invalidHigh.PhysicalAddressWidth == 65
            && !outOfRange32.Succeeded
            && outOfRange32.Status == EnaDmaAddressPlanStatus.AddressOutOfRange
            && outOfRange32.MaximumAddress == 0xFFFF_FFFFUL;
    }

    private static bool TestRejectsDefaultResults()
    {
        EnaDmaRegionValidationResult region = default;
        EnaAdminQueueDmaMemoryPlanResult queue = default;

        return !region.IsInitialized
            && !region.Succeeded
            && region.Status == EnaDmaAddressPlanStatus.Success
            && !queue.IsInitialized
            && !queue.Succeeded
            && queue.Status == EnaDmaAddressPlanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                EnaDmaRegionValidationResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                EnaAdminQueueDmaMemoryPlanResult>();
    }

    private static bool TestRejectsOverflow()
    {
        EnaDmaRegionValidationResult addOverflow = EnaDmaAddressPlan.ValidateRegion(
            CreateAttributes(64),
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.PacketBuffer,
                basePhysicalAddress: ulong.MaxValue - 15UL,
                lengthBytes: 32,
                alignment: 1));
        EnaDmaRegionValidationResult rangeOverflow48 = EnaDmaAddressPlan.ValidateRegion(
            CreateAttributes(48),
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.PacketBuffer,
                basePhysicalAddress: 0x0000_FFFF_FFFF_F000UL,
                lengthBytes: 0x2000,
                alignment: 4096));

        return !addOverflow.Succeeded
            && addOverflow.Status == EnaDmaAddressPlanStatus.AddressOverflow
            && addOverflow.Region.BasePhysicalAddress == ulong.MaxValue - 15UL
            && addOverflow.Region.LengthBytes == 32
            && !rangeOverflow48.Succeeded
            && rangeOverflow48.Status == EnaDmaAddressPlanStatus.AddressOutOfRange
            && rangeOverflow48.MaximumAddress == 0x0000_FFFF_FFFF_FFFFUL;
    }

    private static bool TestValidatesQueueRegions()
    {
        EnaAdminQueueLayoutPlanResult layout = CreateLayout();
        if (!layout.Succeeded)
        {
            return false;
        }

        ulong baseAddress = 0x0000_1234_0000UL;
        EnaAdminQueueDmaMemoryPlanResult result =
            EnaDmaAddressPlan.CreateAdminQueueMemoryPlan(
                CreateAttributes(48),
                layout,
                baseAddress,
                layout.Plan.TotalRequiredBytes);
        if (!result.Succeeded)
        {
            return false;
        }

        EnaAdminQueueDmaMemoryPlan plan = result.Plan;
        return result.Status == EnaDmaAddressPlanStatus.Success
            && result.QueueLayoutStatus == EnaAdminQueueLayoutPlanStatus.Success
            && result.FailingRegionKind == EnaDmaRegionKind.None
            && result.PhysicalAddressWidth == 48
            && result.RequiredLengthBytes == layout.Plan.TotalRequiredBytes
            && result.ProvidedLengthBytes == layout.Plan.TotalRequiredBytes
            && result.MaximumAddress == 0x0000_FFFF_FFFF_FFFFUL
            && plan.ContiguousRegion.Kind == EnaDmaRegionKind.ContiguousAdminQueueMemory
            && plan.ContiguousRegion.BasePhysicalAddress == baseAddress
            && plan.ContiguousRegion.LengthBytes == layout.Plan.TotalRequiredBytes
            && plan.ContiguousRegion.Alignment == layout.Plan.BaseAlignment
            && plan.ContiguousRegion.IsBaseAligned
            && plan.AdminSubmissionQueue.Kind == EnaDmaRegionKind.AdminSubmissionQueue
            && plan.AdminSubmissionQueue.BasePhysicalAddress == baseAddress + layout.Plan.AdminQueue.Offset
            && plan.AdminSubmissionQueue.LengthBytes == layout.Plan.AdminQueue.Length
            && plan.AdminSubmissionQueue.Alignment == layout.Plan.AdminQueue.Alignment
            && plan.AdminSubmissionQueue.IsBaseAligned
            && plan.AdminCompletionQueue.Kind == EnaDmaRegionKind.AdminCompletionQueue
            && plan.AdminCompletionQueue.BasePhysicalAddress == baseAddress + layout.Plan.AdminCompletionQueue.Offset
            && plan.AdminCompletionQueue.LengthBytes == layout.Plan.AdminCompletionQueue.Length
            && plan.AdminCompletionQueue.Alignment == layout.Plan.AdminCompletionQueue.Alignment
            && plan.AdminCompletionQueue.IsBaseAligned
            && plan.QueueLayout.TotalRequiredBytes == 4224
            && !EnaDmaAddressPlan.AllocatesDmaMemory
            && !EnaDmaAddressPlan.MapsPages
            && !EnaDmaAddressPlan.ProgramsIommu
            && !EnaDmaAddressPlan.WritesMmioRegisters
            && !EnaDmaAddressPlan.RingsDoorbells
            && !EnaDmaAddressPlan.ConfiguresInterrupts
            && !EnaDmaAddressPlan.PerformsPacketIo;
    }

    private static bool TestRejectsInvalidRegions()
    {
        EnaDeviceAttributes attributes = CreateAttributes(48);
        EnaAdminQueueLayoutPlanResult layout = CreateLayout();
        if (!layout.Succeeded)
        {
            return false;
        }

        EnaDmaRegionValidationResult zeroLength = EnaDmaAddressPlan.ValidateRegion(
            attributes,
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.PacketBuffer,
                basePhysicalAddress: 0x2000,
                lengthBytes: 0,
                alignment: 4096));
        EnaDmaRegionValidationResult invalidAlignment = EnaDmaAddressPlan.ValidateRegion(
            attributes,
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.PacketBuffer,
                basePhysicalAddress: 0x2000,
                lengthBytes: 128,
                alignment: 24));
        EnaDmaRegionValidationResult unaligned = EnaDmaAddressPlan.ValidateRegion(
            attributes,
            new EnaDmaPhysicalRegion(
                EnaDmaRegionKind.PacketBuffer,
                basePhysicalAddress: 0x2001,
                lengthBytes: 128,
                alignment: 4096));
        EnaAdminQueueDmaMemoryPlanResult queueTooSmall =
            EnaDmaAddressPlan.CreateAdminQueueMemoryPlan(
                attributes,
                layout,
                basePhysicalAddress: 0x0000_1234_0000UL,
                lengthBytes: layout.Plan.TotalRequiredBytes - 1UL);
        EnaAdminQueueLayoutPlanResult rejectedLayout =
            EnaAdminQueueLayoutPlan.Create(
                adminQueueDepth: 0,
                adminQueueEntrySize: 64,
                adminCompletionQueueDepth: 8,
                adminCompletionQueueEntrySize: 16,
                baseAlignment: EnaAdminQueueLayoutPlan.DefaultBaseAlignment,
                memoryBudgetBytes: 8192,
                requiresContiguousMemory: true);
        EnaAdminQueueDmaMemoryPlanResult rejectedQueue =
            EnaDmaAddressPlan.CreateAdminQueueMemoryPlan(
                attributes,
                rejectedLayout,
                basePhysicalAddress: 0x0000_1234_0000UL,
                lengthBytes: 8192);
        EnaAdminQueueDmaMemoryPlanResult unalignedQueue =
            EnaDmaAddressPlan.CreateAdminQueueMemoryPlan(
                attributes,
                layout,
                basePhysicalAddress: 0x0000_1234_0001UL,
                lengthBytes: layout.Plan.TotalRequiredBytes);

        return !zeroLength.Succeeded
            && zeroLength.Status == EnaDmaAddressPlanStatus.ZeroLength
            && zeroLength.Region.Kind == EnaDmaRegionKind.PacketBuffer
            && !invalidAlignment.Succeeded
            && invalidAlignment.Status == EnaDmaAddressPlanStatus.InvalidAlignment
            && !unaligned.Succeeded
            && unaligned.Status == EnaDmaAddressPlanStatus.UnalignedBaseAddress
            && !queueTooSmall.Succeeded
            && queueTooSmall.Status == EnaDmaAddressPlanStatus.QueueMemoryTooSmall
            && queueTooSmall.RequiredLengthBytes == layout.Plan.TotalRequiredBytes
            && queueTooSmall.ProvidedLengthBytes == layout.Plan.TotalRequiredBytes - 1UL
            && queueTooSmall.FailingRegionKind == EnaDmaRegionKind.ContiguousAdminQueueMemory
            && !rejectedQueue.Succeeded
            && rejectedQueue.Status == EnaDmaAddressPlanStatus.QueueLayoutRejected
            && rejectedQueue.QueueLayoutStatus == EnaAdminQueueLayoutPlanStatus.InvalidDepth
            && !unalignedQueue.Succeeded
            && unalignedQueue.Status == EnaDmaAddressPlanStatus.UnalignedBaseAddress
            && unalignedQueue.FailingRegionKind == EnaDmaRegionKind.ContiguousAdminQueueMemory;
    }

    private static EnaAdminQueueLayoutPlanResult CreateLayout()
    {
        return EnaAdminQueueLayoutPlan.Create(
            adminQueueDepth: 8,
            adminQueueEntrySize: 64,
            adminCompletionQueueDepth: 8,
            adminCompletionQueueEntrySize: 16,
            baseAlignment: EnaAdminQueueLayoutPlan.DefaultBaseAlignment,
            memoryBudgetBytes: 8192,
            requiresContiguousMemory: true);
    }

    private static EnaDeviceAttributes CreateAttributes(uint physicalAddressWidth)
    {
        return new EnaDeviceAttributes(
            commandId: 1,
            phase: true,
            completionStatus: EnaAdminCompletionStatus.Success,
            extendedStatus: 0,
            submissionQueueHeadIndex: 0,
            featureId: EnaAdminFeatureId.DeviceAttributes,
            implementationId: 1,
            deviceVersion: 1,
            supportedFeatures: 0,
            capabilities: 0,
            physicalAddressWidth: physicalAddressWidth,
            virtualAddressWidth: 64,
            macAddress: new MacAddress(0x02, 0x10, 0x20, 0x30, 0x40, 0x50),
            maxMtu: 1500);
    }
}
