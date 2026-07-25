using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockReadLifecycleTests
{
    private const ushort QueueSize = 8;
    private const ulong HeaderPhysicalAddress = 0x0010_0000UL;
    private const ulong DataPhysicalAddress = 0x0020_0000UL;
    private const ulong StatusPhysicalAddress = 0x0030_0000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage.virtioblk lifecycle publishes read request", TestPublishesReadRequest, ref failures);
        Run("storage.virtioblk lifecycle completes read request", TestCompletesReadRequest, ref failures);
        Run("storage.virtioblk lifecycle carries next ring indexes", TestCarriesNextRingIndexes, ref failures);
        Run("storage.virtioblk lifecycle reports read plan failure", TestReportsReadPlanFailure, ref failures);
        Run("storage.virtioblk lifecycle reports descriptor write failure", TestReportsDescriptorWriteFailure, ref failures);
        Run("storage.virtioblk lifecycle reports completion failure", TestReportsCompletionFailure, ref failures);
        Run("storage.virtioblk lifecycle publish default result is not success", TestDefaultPublishResultIsNotSuccess, ref failures);
        Run("storage.virtioblk lifecycle completion default result is not success", TestDefaultCompletionResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio block read lifecycle host tests failed: " + failures);
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

    private static bool TestPublishesReadRequest()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[1024];
        byte[] status = { 0x7e };
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0xcc);

        VirtioBlockReadLifecyclePublishResult result =
            VirtioBlockReadLifecycle.PublishReadRequest(
                sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
                deviceSectorCount: 64,
                firstSector: 4,
                sectorCount: 2,
                queueSize: QueueSize,
                firstDescriptorIndex: 1,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: 9,
                previousUsedIndex: 13,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return result.Succeeded
            && result.Status == VirtioBlockReadLifecyclePublishStatus.Success
            && result.ReadPlanStatus == VirtioBlockReadTransactionPlanStatus.Success
            && result.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.Success
            && result.DescriptorWriterStatus == VirtioBlockDescriptorChainWriterStatus.Success
            && result.FirstSector == 4
            && result.SectorCount == 2
            && result.LastSectorInclusive == 5
            && result.DataByteCount == 1024
            && result.QueueSize == QueueSize
            && result.DescriptorHead == 1
            && result.PreviousAvailableRingIndex == 9
            && result.NextAvailableRingIndex == 10
            && result.PreviousUsedIndex == 13
            && result.ExpectedNextUsedIndex == 14
            && result.AvailableRingSlot == 1
            && result.DescriptorsWritten == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && EndianPrimitives.ReadUInt32LittleEndian(requestHeader.AsSpan(0, sizeof(uint))) == (uint)VirtioBlockRequestType.Read
            && EndianPrimitives.ReadUInt64LittleEndian(requestHeader.AsSpan(8, sizeof(ulong))) == 4
            && ReadUInt16(queueMemory, (int)result.AvailableRingEntryOffset) == result.DescriptorHead
            && ReadUInt16(queueMemory, (int)queue.Plan.AvailableRing.Offset + 2) == result.NextAvailableRingIndex
            && !VirtioBlockReadLifecycle.PerformsDmaMapping
            && !VirtioBlockReadLifecycle.PerformsMmio
            && !VirtioBlockReadLifecycle.NotifiesDevice
            && !VirtioBlockReadLifecycle.PollsDevice
            && !VirtioBlockReadLifecycle.HandlesInterrupts
            && !VirtioBlockReadLifecycle.CopiesPayload
            && !VirtioBlockReadLifecycle.PerformsDiskRead;
    }

    private static bool TestCompletesReadRequest()
    {
        VirtioBlockReadLifecyclePublishResult publish = CreateSuccessfulPublish(
            previousAvailableRingIndex: 5,
            previousUsedIndex: 9,
            firstDescriptorIndex: 3,
            out byte[] queueMemory,
            out byte[] status);
        if (!publish.Succeeded)
        {
            return false;
        }

        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        WriteUsedCompletion(
            publish.QueuePlan,
            queueMemory,
            publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            publish.DescriptorHead,
            publish.DataByteCount);

        VirtioBlockReadLifecycleCompletionResult result =
            VirtioBlockReadLifecycle.CompleteReadRequest(
                publish,
                queueMemory,
                status);

        return result.Succeeded
            && result.Status == VirtioBlockReadLifecycleCompletionStatus.Success
            && result.PublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.FirstSector == 2
            && result.SectorCount == 2
            && result.LastSectorInclusive == 3
            && result.DataByteCount == 1024
            && result.DescriptorHead == 3
            && result.PreviousAvailableRingIndex == 5
            && result.NextAvailableRingIndex == 6
            && result.PreviousUsedIndex == 9
            && result.CurrentUsedIndex == 10
            && result.NextUsedIndex == 10
            && result.UsedRingSlot == 1
            && result.UsedRingElementOffset == 12
            && result.CompletedDescriptorHead == 3
            && result.CompletedByteCount == 1024
            && result.StatusByte == (byte)VirtioBlockDeviceStatus.Ok;
    }

    private static bool TestCarriesNextRingIndexes()
    {
        VirtioBlockReadLifecyclePublishResult publish = CreateSuccessfulPublish(
            previousAvailableRingIndex: ushort.MaxValue,
            previousUsedIndex: ushort.MaxValue,
            firstDescriptorIndex: 0,
            out byte[] queueMemory,
            out byte[] status);
        if (!publish.Succeeded)
        {
            return false;
        }

        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        WriteUsedCompletion(
            publish.QueuePlan,
            queueMemory,
            publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            publish.DescriptorHead,
            publish.DataByteCount);

        VirtioBlockReadLifecycleCompletionResult completion =
            VirtioBlockReadLifecycle.CompleteReadRequest(
                publish,
                queueMemory,
                status);

        return completion.Succeeded
            && publish.PreviousAvailableRingIndex == ushort.MaxValue
            && publish.NextAvailableRingIndex == 0
            && publish.PreviousUsedIndex == ushort.MaxValue
            && publish.ExpectedNextUsedIndex == 0
            && completion.PreviousUsedIndex == ushort.MaxValue
            && completion.CurrentUsedIndex == 0
            && completion.NextUsedIndex == 0;
    }

    private static bool TestReportsReadPlanFailure()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[511];
        byte[] status = { 0x7e };
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0x44);

        VirtioBlockReadLifecyclePublishResult result =
            VirtioBlockReadLifecycle.PublishReadRequest(
                sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
                deviceSectorCount: 64,
                firstSector: 4,
                sectorCount: 1,
                queueSize: QueueSize,
                firstDescriptorIndex: 1,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: 9,
                previousUsedIndex: 13,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return !result.Succeeded
            && result.Status == VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed
            && result.ReadPlanStatus == VirtioBlockReadTransactionPlanStatus.DestinationTooSmall
            && result.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && result.NextAvailableRingIndex == result.PreviousAvailableRingIndex
            && result.ExpectedNextUsedIndex == result.PreviousUsedIndex
            && AllBytesEqual(queueMemory, 0x44);
    }

    private static bool TestReportsDescriptorWriteFailure()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[512];
        byte[] status = { 0 };
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset - 1];
        Array.Fill(queueMemory, (byte)0x5a);

        VirtioBlockReadLifecyclePublishResult result =
            VirtioBlockReadLifecycle.PublishReadRequest(
                sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
                deviceSectorCount: 64,
                firstSector: 4,
                sectorCount: 1,
                queueSize: QueueSize,
                firstDescriptorIndex: 1,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: 9,
                previousUsedIndex: 13,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return !result.Succeeded
            && result.Status == VirtioBlockReadLifecyclePublishStatus.DescriptorWriteFailed
            && result.ReadPlanStatus == VirtioBlockReadTransactionPlanStatus.Success
            && result.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.Success
            && result.DescriptorWriterStatus == VirtioBlockDescriptorChainWriterStatus.QueueMemoryTooSmall
            && AllBytesEqual(queueMemory, 0x5a);
    }

    private static bool TestReportsCompletionFailure()
    {
        VirtioBlockReadLifecyclePublishResult publish = CreateSuccessfulPublish(
            previousAvailableRingIndex: 2,
            previousUsedIndex: 6,
            firstDescriptorIndex: 1,
            out byte[] queueMemory,
            out byte[] status);
        if (!publish.Succeeded)
        {
            return false;
        }

        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        WriteUsedCompletion(
            publish.QueuePlan,
            queueMemory,
            publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            descriptorHead: publish.DescriptorHead + 1u,
            publish.DataByteCount);

        VirtioBlockReadLifecycleCompletionResult completion =
            VirtioBlockReadLifecycle.CompleteReadRequest(
                publish,
                queueMemory,
                status);

        return !completion.Succeeded
            && completion.Status == VirtioBlockReadLifecycleCompletionStatus.CompletionFailed
            && completion.PublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && completion.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.UnexpectedDescriptorHead
            && completion.DescriptorHead == publish.DescriptorHead
            && completion.CompletedDescriptorHead == publish.DescriptorHead + 1u
            && completion.CompletedByteCount == publish.DataByteCount
            && completion.StatusByte == (byte)VirtioBlockDeviceStatus.Ok;
    }

    private static bool TestDefaultPublishResultIsNotSuccess()
    {
        VirtioBlockReadLifecyclePublishResult result = default;
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[511];
        byte[] status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        VirtioBlockReadLifecyclePublishResult failed =
            VirtioBlockReadLifecycle.PublishReadRequest(
                sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
                deviceSectorCount: 64,
                firstSector: 0,
                sectorCount: 1,
                queueSize: QueueSize,
                firstDescriptorIndex: 0,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadLifecyclePublishStatus.Success
            && result.ReadPlanStatus == VirtioBlockReadTransactionPlanStatus.Success
            && result.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.Success
            && result.DescriptorWriterStatus == VirtioBlockDescriptorChainWriterStatus.Success
            && result.QueueSize == 0
            && result.DescriptorHead == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadLifecyclePublishResult>();
    }

    private static bool TestDefaultCompletionResultIsNotSuccess()
    {
        VirtioBlockReadLifecycleCompletionResult result = default;
        VirtioBlockReadLifecycleCompletionResult failed =
            VirtioBlockReadLifecycle.CompleteReadRequest(
                default,
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadLifecycleCompletionStatus.Success
            && result.PublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && result.ReadPlanStatus == VirtioBlockReadTransactionPlanStatus.Success
            && result.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.Success
            && result.DescriptorWriterStatus == VirtioBlockDescriptorChainWriterStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.DescriptorHead == 0
            && result.CompletedByteCount == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockReadLifecycleCompletionStatus.PublishFailed
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadLifecycleCompletionResult>();
    }

    private static VirtioBlockReadLifecyclePublishResult CreateSuccessfulPublish(
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        ushort firstDescriptorIndex,
        out byte[] queueMemory,
        out byte[] status)
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[1024];
        status = new byte[1];
        queueMemory = new byte[(int)queue.Plan.EndOffset];

        return VirtioBlockReadLifecycle.PublishReadRequest(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 64,
            firstSector: 2,
            sectorCount: 2,
            queueSize: QueueSize,
            firstDescriptorIndex,
            availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            previousAvailableRingIndex,
            previousUsedIndex,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress,
            requestHeader,
            destination,
            status,
            queueMemory);
    }

    private static void WriteUsedCompletion(
        VirtqueueSplitRingPlan queue,
        byte[] queueMemory,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        uint descriptorHead,
        uint usedByteCount)
    {
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)queue.UsedRing.Offset + 2, sizeof(ushort)),
            currentUsedIndex);

        ushort slot = (ushort)(previousUsedIndex & (queue.QueueSize - 1));
        int elementOffset = (int)queue.UsedRing.Offset +
            (int)VirtqueueSplitRingPlan.UsedRingHeaderLength +
            (slot * (int)VirtqueueSplitRingPlan.UsedRingElementLength);
        EndianPrimitives.WriteUInt32LittleEndian(
            queueMemory.AsSpan(elementOffset, sizeof(uint)),
            descriptorHead);
        EndianPrimitives.WriteUInt32LittleEndian(
            queueMemory.AsSpan(elementOffset + 4, sizeof(uint)),
            usedByteCount);
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static bool AllBytesEqual(
        ReadOnlySpan<byte> bytes,
        byte value)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != value)
            {
                return false;
            }
        }

        return true;
    }
}
