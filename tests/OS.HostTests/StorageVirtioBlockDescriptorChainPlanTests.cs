using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Storage;

internal static class StorageVirtioBlockDescriptorChainPlanTests
{
    private const ushort QueueSize = 8;
    private const ulong HeaderPhysicalAddress = 0x0010_0000UL;
    private const ulong DataPhysicalAddress = 0x0020_0000UL;
    private const ulong StatusPhysicalAddress = 0x0030_0000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage.virtioblk descriptor chain plans three descriptors", TestPlansThreeDescriptors, ref failures);
        Run("storage.virtioblk descriptor chain marks data writable", TestMarksDataWritable, ref failures);
        Run("storage.virtioblk descriptor chain rejects insufficient descriptors", TestRejectsInsufficientDescriptors, ref failures);
        Run("storage.virtioblk descriptor chain rejects failed transaction plan", TestRejectsFailedTransactionPlan, ref failures);
        Run("storage.virtioblk descriptor chain default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio block descriptor-chain host tests failed: " + failures);
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

    private static bool TestPlansThreeDescriptors()
    {
        VirtioBlockReadTransactionPlanResult transaction = CreateSuccessfulTransaction();
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);

        VirtioBlockDescriptorChainPlanResult result = VirtioBlockDescriptorChainPlan.Create(
            transaction,
            queue,
            firstDescriptorIndex: 1,
            availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            availableRingIndex: 9,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress);
        if (!result.Succeeded || result.Status != VirtioBlockDescriptorChainPlanStatus.Success)
        {
            return false;
        }

        VirtioBlockDescriptorChainPlan plan = result.Plan;
        return plan.QueuePlan.QueueSize == QueueSize
            && plan.TransactionPlan.DataByteCount == 1024
            && plan.RequestPlan.RequestType == VirtioBlockRequestType.Read
            && plan.HeadDescriptorIndex == 1
            && plan.RequestHeaderDescriptor.Role == VirtioBlockDescriptorChainRole.RequestHeader
            && plan.RequestHeaderDescriptor.DescriptorIndex == 1
            && plan.RequestHeaderDescriptor.PhysicalAddress == HeaderPhysicalAddress
            && plan.RequestHeaderDescriptor.Length == VirtioBlockRequestPlan.RequestHeaderLength
            && plan.RequestHeaderDescriptor.HasNext
            && plan.RequestHeaderDescriptor.NextDescriptorIndex == 2
            && plan.RequestHeaderDescriptor.Flags == VirtioBlockDescriptorMetadata.NextFlag
            && plan.RequestHeaderDescriptor.DescriptorTableOffset == 16
            && plan.RequestHeaderDescriptor.MemoryRange.EndExclusive == HeaderPhysicalAddress + VirtioBlockRequestPlan.RequestHeaderLength
            && plan.DataBufferDescriptor.Role == VirtioBlockDescriptorChainRole.DataBuffer
            && plan.DataBufferDescriptor.DescriptorIndex == 2
            && plan.DataBufferDescriptor.PhysicalAddress == DataPhysicalAddress
            && plan.DataBufferDescriptor.Length == 1024
            && plan.DataBufferDescriptor.HasNext
            && plan.DataBufferDescriptor.NextDescriptorIndex == 3
            && plan.DataBufferDescriptor.DescriptorTableOffset == 32
            && plan.DataBufferDescriptor.MemoryRange.EndExclusive == DataPhysicalAddress + 1024
            && plan.StatusByteDescriptor.Role == VirtioBlockDescriptorChainRole.StatusByte
            && plan.StatusByteDescriptor.DescriptorIndex == 3
            && plan.StatusByteDescriptor.PhysicalAddress == StatusPhysicalAddress
            && plan.StatusByteDescriptor.Length == VirtioBlockRequestPlan.StatusLength
            && !plan.StatusByteDescriptor.HasNext
            && plan.StatusByteDescriptor.NextDescriptorIndex == 0
            && plan.StatusByteDescriptor.DescriptorTableOffset == 48
            && plan.StatusByteDescriptor.MemoryRange.EndExclusive == StatusPhysicalAddress + VirtioBlockRequestPlan.StatusLength
            && plan.AvailableRingIndex == 9
            && plan.AvailableRingSlot == 1
            && plan.AvailableRingEntryOffset == 134
            && !VirtioBlockDescriptorChainPlan.PerformsDmaMapping
            && !VirtioBlockDescriptorChainPlan.WritesQueueMemory
            && !VirtioBlockDescriptorChainPlan.PerformsMmio
            && !VirtioBlockDescriptorChainPlan.NotifiesDevice
            && !VirtioBlockDescriptorChainPlan.HandlesInterrupts
            && !VirtioBlockDescriptorChainPlan.PerformsDiskRead;
    }

    private static bool TestMarksDataWritable()
    {
        VirtioBlockDescriptorChainPlanResult result = VirtioBlockDescriptorChainPlan.Create(
            CreateSuccessfulTransaction(),
            VirtqueueSplitRingPlan.Create(QueueSize),
            firstDescriptorIndex: 0,
            availableDescriptorCount: QueueSize,
            availableRingIndex: 0,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress);

        if (!result.Succeeded)
        {
            return false;
        }

        VirtioBlockDescriptorChainPlan plan = result.Plan;
        return !plan.RequestHeaderDescriptor.DeviceWritable
            && plan.RequestHeaderDescriptor.DeviceReadable
            && plan.DataBufferDescriptor.DeviceWritable
            && !plan.DataBufferDescriptor.DeviceReadable
            && plan.StatusByteDescriptor.DeviceWritable
            && plan.DataBufferDescriptor.Flags == (VirtioBlockDescriptorMetadata.NextFlag | VirtioBlockDescriptorMetadata.DeviceWriteFlag)
            && plan.StatusByteDescriptor.Flags == VirtioBlockDescriptorMetadata.DeviceWriteFlag;
    }

    private static bool TestRejectsInsufficientDescriptors()
    {
        VirtioBlockDescriptorChainPlanResult result = VirtioBlockDescriptorChainPlan.Create(
            CreateSuccessfulTransaction(),
            VirtqueueSplitRingPlan.Create(QueueSize),
            firstDescriptorIndex: 0,
            availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount - 1,
            availableRingIndex: 0,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress);

        return !result.Succeeded
            && result.Status == VirtioBlockDescriptorChainPlanStatus.InsufficientDescriptors
            && result.TransactionPlanStatus == VirtioBlockReadTransactionPlanStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success;
    }

    private static bool TestRejectsFailedTransactionPlan()
    {
        Span<byte> requestHeader = stackalloc byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[511];
        Span<byte> status = stackalloc byte[(int)VirtioBlockRequestPlan.StatusLength];

        VirtioBlockReadTransactionPlanResult failedTransaction =
            VirtioBlockReadTransactionPlan.Create(
                sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
                deviceSectorCount: 16,
                firstSector: 0,
                sectorCount: 1,
                queueSize: QueueSize,
                requestHeader: requestHeader,
                destination: destination,
                status: status);

        VirtioBlockDescriptorChainPlanResult result = VirtioBlockDescriptorChainPlan.Create(
            failedTransaction,
            VirtqueueSplitRingPlan.Create(QueueSize),
            firstDescriptorIndex: 0,
            availableDescriptorCount: QueueSize,
            availableRingIndex: 0,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress);

        return !failedTransaction.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorChainPlanStatus.TransactionPlanFailed
            && result.TransactionPlanStatus == VirtioBlockReadTransactionPlanStatus.DestinationTooSmall
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioBlockDescriptorChainPlanResult result = default;
        VirtioBlockDescriptorChainPlanResult failed =
            VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.InsufficientDescriptors,
                VirtioBlockReadTransactionPlanStatus.Success,
                VirtioBlockRequestPlanStatus.Success,
                VirtqueueSplitRingPlanStatus.Success);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorChainPlanStatus.Success
            && result.TransactionPlanStatus == VirtioBlockReadTransactionPlanStatus.Success
            && result.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.Plan.AvailableDescriptorCount == 0
            && result.Plan.AvailableRingIndex == 0
            && result.Plan.HeadDescriptorIndex == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockDescriptorChainPlanStatus.InsufficientDescriptors
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorChainPlanResult>();
    }

    private static VirtioBlockReadTransactionPlanResult CreateSuccessfulTransaction()
    {
        Span<byte> requestHeader = stackalloc byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[1024];
        Span<byte> status = stackalloc byte[(int)VirtioBlockRequestPlan.StatusLength];

        return VirtioBlockReadTransactionPlan.Create(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 16,
            firstSector: 2,
            sectorCount: 2,
            queueSize: QueueSize,
            requestHeader: requestHeader,
            destination: destination,
            status: status);
    }
}
