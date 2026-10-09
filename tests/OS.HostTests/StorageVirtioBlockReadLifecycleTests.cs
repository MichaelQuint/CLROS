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

    [HostTest("storage.virtioblk lifecycle publishes read request", HostLane.Fast, 0)]
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

        return HostCheck.Result(result.Succeeded
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
            && !VirtioBlockReadLifecycle.PerformsDiskRead);
    }

    [HostTest("storage.virtioblk lifecycle completes read request", HostLane.Fast, 1)]
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
            return HostCheck.Result(false);
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

        return HostCheck.Result(result.Succeeded
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
            && result.StatusByte == (byte)VirtioBlockDeviceStatus.Ok);
    }

    [HostTest("storage.virtioblk lifecycle carries next ring indexes", HostLane.Fast, 2)]
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
            return HostCheck.Result(false);
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

        return HostCheck.Result(completion.Succeeded
            && publish.PreviousAvailableRingIndex == ushort.MaxValue
            && publish.NextAvailableRingIndex == 0
            && publish.PreviousUsedIndex == ushort.MaxValue
            && publish.ExpectedNextUsedIndex == 0
            && completion.PreviousUsedIndex == ushort.MaxValue
            && completion.CurrentUsedIndex == 0
            && completion.NextUsedIndex == 0);
    }

    [HostTest("storage.virtioblk lifecycle reports read plan failure", HostLane.Fast, 3)]
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

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed
            && result.ReadPlanStatus == VirtioBlockReadTransactionPlanStatus.DestinationTooSmall
            && result.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && result.NextAvailableRingIndex == result.PreviousAvailableRingIndex
            && result.ExpectedNextUsedIndex == result.PreviousUsedIndex
            && AllBytesEqual(queueMemory, 0x44));
    }

    [HostTest("storage.virtioblk lifecycle reports descriptor write failure", HostLane.Fast, 4)]
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

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockReadLifecyclePublishStatus.DescriptorWriteFailed
            && result.ReadPlanStatus == VirtioBlockReadTransactionPlanStatus.Success
            && result.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.Success
            && result.DescriptorWriterStatus == VirtioBlockDescriptorChainWriterStatus.QueueMemoryTooSmall
            && AllBytesEqual(queueMemory, 0x5a));
    }

    [HostTest("storage.virtioblk lifecycle reports completion failure", HostLane.Fast, 5)]
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
            return HostCheck.Result(false);
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

        return HostCheck.Result(!completion.Succeeded
            && completion.Status == VirtioBlockReadLifecycleCompletionStatus.CompletionFailed
            && completion.PublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && completion.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.UnexpectedDescriptorHead
            && completion.DescriptorHead == publish.DescriptorHead
            && completion.CompletedDescriptorHead == publish.DescriptorHead + 1u
            && completion.CompletedByteCount == publish.DataByteCount
            && completion.StatusByte == (byte)VirtioBlockDeviceStatus.Ok);
    }

    [HostTest("storage.virtioblk lifecycle publish default result is not success", HostLane.Fast, 6)]
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

        return HostCheck.Result(!result.IsInitialized
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
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadLifecyclePublishResult>());
    }

    [HostTest("storage.virtioblk lifecycle completion default result is not success", HostLane.Fast, 7)]
    private static bool TestDefaultCompletionResultIsNotSuccess()
    {
        VirtioBlockReadLifecycleCompletionResult result = default;
        VirtioBlockReadLifecycleCompletionResult failed =
            VirtioBlockReadLifecycle.CompleteReadRequest(
                default,
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty);

        return HostCheck.Result(!result.IsInitialized
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
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadLifecycleCompletionResult>());
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
