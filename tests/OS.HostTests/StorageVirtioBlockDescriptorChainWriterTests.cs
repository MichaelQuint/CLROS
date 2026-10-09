using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockDescriptorChainWriterTests
{
    private const ushort QueueSize = 8;
    private const ulong HeaderPhysicalAddress = 0x0010_0000UL;
    private const ulong DataPhysicalAddress = 0x0020_0000UL;
    private const ulong StatusPhysicalAddress = 0x0030_0000UL;

    [HostTest("storage.virtioblk descriptor writer writes three descriptors", HostLane.Fast, 0)]
    private static bool TestWritesThreeDescriptors()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorChainPlanResult descriptorPlan = CreateDescriptorPlan(
            queue,
            firstDescriptorIndex: 1,
            availableRingIndex: 9);
        if (!descriptorPlan.Succeeded || !queue.Succeeded)
        {
            return HostCheck.Result(false);
        }

        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0xcc);

        VirtioBlockDescriptorChainWriterResult result =
            VirtioBlockDescriptorChainWriter.Write(descriptorPlan, queue, queueMemory);
        if (!result.Succeeded ||
            result.Status != VirtioBlockDescriptorChainWriterStatus.Success ||
            result.HeadDescriptorIndex != 1 ||
            result.NextAvailableRingIndex != 10 ||
            result.DescriptorsWritten != VirtioBlockDescriptorChainPlan.RequiredDescriptorCount)
        {
            return HostCheck.Result(false);
        }

        VirtioBlockDescriptorChainPlan plan = descriptorPlan.Plan;
        return HostCheck.Result(DescriptorMatches(
                queueMemory,
                plan.RequestHeaderDescriptor.DescriptorTableOffset,
                HeaderPhysicalAddress,
                VirtioBlockRequestPlan.RequestHeaderLength,
                VirtioBlockDescriptorMetadata.NextFlag,
                next: 2)
            && DescriptorMatches(
                queueMemory,
                plan.DataBufferDescriptor.DescriptorTableOffset,
                DataPhysicalAddress,
                1024,
                VirtioBlockDescriptorMetadata.NextFlag | VirtioBlockDescriptorMetadata.DeviceWriteFlag,
                next: 3)
            && DescriptorMatches(
                queueMemory,
                plan.StatusByteDescriptor.DescriptorTableOffset,
                StatusPhysicalAddress,
                VirtioBlockRequestPlan.StatusLength,
                VirtioBlockDescriptorMetadata.DeviceWriteFlag,
                next: 0)
            && !VirtioBlockDescriptorChainWriter.AllocatesDmaMemory
            && !VirtioBlockDescriptorChainWriter.PerformsMmio
            && !VirtioBlockDescriptorChainWriter.NotifiesDevice
            && !VirtioBlockDescriptorChainWriter.WaitsForUsedRingCompletion
            && !VirtioBlockDescriptorChainWriter.PerformsDiskRead);
    }

    [HostTest("storage.virtioblk descriptor writer writes available ring head", HostLane.Fast, 1)]
    private static bool TestWritesAvailableRingHead()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorChainPlanResult descriptorPlan = CreateDescriptorPlan(
            queue,
            firstDescriptorIndex: 1,
            availableRingIndex: 9);
        if (!descriptorPlan.Succeeded || !queue.Succeeded)
        {
            return HostCheck.Result(false);
        }

        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0x5a);

        VirtioBlockDescriptorChainWriterResult result =
            VirtioBlockDescriptorChainWriter.Write(descriptorPlan, queue, queueMemory);
        if (!result.Succeeded)
        {
            return HostCheck.Result(false);
        }

        VirtioBlockDescriptorChainPlan plan = descriptorPlan.Plan;
        int availableFlagsOffset = (int)queue.Plan.AvailableRing.Offset;
        int availableIndexOffset = availableFlagsOffset + 2;
        int availableEntryOffset = (int)plan.AvailableRingEntryOffset;
        int previousAvailableEntryOffset = availableEntryOffset - 2;

        return HostCheck.Result(ReadUInt16(queueMemory, availableIndexOffset) == 10
            && ReadUInt16(queueMemory, availableEntryOffset) == plan.HeadDescriptorIndex
            && queueMemory[availableFlagsOffset] == 0x5a
            && queueMemory[availableFlagsOffset + 1] == 0x5a
            && queueMemory[previousAvailableEntryOffset] == 0x5a
            && queueMemory[previousAvailableEntryOffset + 1] == 0x5a);
    }

    [HostTest("storage.virtioblk descriptor writer preserves unrelated memory", HostLane.Fast, 2)]
    private static bool TestPreservesUnrelatedMemory()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorChainPlanResult descriptorPlan = CreateDescriptorPlan(
            queue,
            firstDescriptorIndex: 1,
            availableRingIndex: 9);
        if (!descriptorPlan.Succeeded || !queue.Succeeded)
        {
            return HostCheck.Result(false);
        }

        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        for (int i = 0; i < queueMemory.Length; i++)
        {
            queueMemory[i] = (byte)(i + 17);
        }

        byte[] original = new byte[queueMemory.Length];
        queueMemory.CopyTo(original, 0);

        VirtioBlockDescriptorChainWriterResult result =
            VirtioBlockDescriptorChainWriter.Write(descriptorPlan, queue, queueMemory);

        return HostCheck.Result(result.Succeeded &&
            MemoryOutsideWriterRangesUnchanged(queueMemory, original, descriptorPlan.Plan));
    }

    [HostTest("storage.virtioblk descriptor writer rejects failed plan", HostLane.Fast, 3)]
    private static bool TestRejectsFailedPlan()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorChainPlanResult descriptorPlan = CreateFailedDescriptorPlan(queue);
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0x7e);

        VirtioBlockDescriptorChainWriterResult result =
            VirtioBlockDescriptorChainWriter.Write(descriptorPlan, queue, queueMemory);

        return HostCheck.Result(!descriptorPlan.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorChainWriterStatus.DescriptorPlanFailed
            && result.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.TransactionPlanFailed
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.DescriptorsWritten == 0
            && AllBytesEqual(queueMemory, 0x7e));
    }

    [HostTest("storage.virtioblk descriptor writer rejects undersized queue memory", HostLane.Fast, 4)]
    private static bool TestRejectsUndersizedQueueMemory()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtioBlockDescriptorChainPlanResult descriptorPlan = CreateDescriptorPlan(
            queue,
            firstDescriptorIndex: 1,
            availableRingIndex: 9);
        if (!descriptorPlan.Succeeded || !queue.Succeeded)
        {
            return HostCheck.Result(false);
        }

        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset - 1];
        Array.Fill(queueMemory, (byte)0xa5);

        VirtioBlockDescriptorChainWriterResult result =
            VirtioBlockDescriptorChainWriter.Write(descriptorPlan, queue, queueMemory);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorChainWriterStatus.QueueMemoryTooSmall
            && result.DescriptorsWritten == 0
            && AllBytesEqual(queueMemory, 0xa5));
    }

    [HostTest("storage.virtioblk descriptor writer rejects descriptor index range", HostLane.Fast, 5)]
    private static bool TestRejectsDescriptorIndexRange()
    {
        VirtqueueSplitRingPlanResult plannedQueue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtqueueSplitRingPlanResult smallerQueue = VirtqueueSplitRingPlan.Create(queueSize: 4);
        VirtioBlockDescriptorChainPlanResult descriptorPlan = CreateDescriptorPlan(
            plannedQueue,
            firstDescriptorIndex: 2,
            availableRingIndex: 0);
        if (!descriptorPlan.Succeeded || !smallerQueue.Succeeded)
        {
            return HostCheck.Result(false);
        }

        byte[] queueMemory = new byte[(int)smallerQueue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0x44);

        VirtioBlockDescriptorChainWriterResult result =
            VirtioBlockDescriptorChainWriter.Write(descriptorPlan, smallerQueue, queueMemory);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorChainWriterStatus.DescriptorIndexOutOfRange
            && AllBytesEqual(queueMemory, 0x44));
    }

    [HostTest("storage.virtioblk descriptor writer rejects invalid available slot", HostLane.Fast, 6)]
    private static bool TestRejectsInvalidAvailableSlot()
    {
        VirtqueueSplitRingPlanResult plannedQueue = VirtqueueSplitRingPlan.Create(QueueSize);
        VirtqueueSplitRingPlanResult smallerQueue = VirtqueueSplitRingPlan.Create(queueSize: 4);
        VirtioBlockDescriptorChainPlanResult descriptorPlan = CreateDescriptorPlan(
            plannedQueue,
            firstDescriptorIndex: 0,
            availableRingIndex: 7);
        if (!descriptorPlan.Succeeded || !smallerQueue.Succeeded)
        {
            return HostCheck.Result(false);
        }

        byte[] queueMemory = new byte[(int)smallerQueue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0x33);

        VirtioBlockDescriptorChainWriterResult result =
            VirtioBlockDescriptorChainWriter.Write(descriptorPlan, smallerQueue, queueMemory);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorChainWriterStatus.InvalidAvailableRingSlot
            && AllBytesEqual(queueMemory, 0x33));
    }

    [HostTest("storage.virtioblk descriptor writer rejects arithmetic overflow", HostLane.Fast, 7)]
    private static bool TestRejectsArithmeticOverflow()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(
            queueStartOffset: 0x8000_0000,
            queueSize: QueueSize);
        VirtioBlockDescriptorChainPlanResult descriptorPlan = CreateDescriptorPlan(
            queue,
            firstDescriptorIndex: 0,
            availableRingIndex: 0);
        if (!descriptorPlan.Succeeded || !queue.Succeeded)
        {
            return HostCheck.Result(false);
        }

        Span<byte> queueMemory = stackalloc byte[16];
        queueMemory.Fill(0x22);

        VirtioBlockDescriptorChainWriterResult result =
            VirtioBlockDescriptorChainWriter.Write(descriptorPlan, queue, queueMemory);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioBlockDescriptorChainWriterStatus.ArithmeticOverflow
            && AllBytesEqual(queueMemory, 0x22));
    }

    [HostTest("storage.virtioblk descriptor writer default result is not success", HostLane.Fast, 8)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioBlockDescriptorChainWriterResult result = default;
        VirtioBlockDescriptorChainWriterResult failed = VirtioBlockDescriptorChainWriterResult.Fail(
            VirtioBlockDescriptorChainWriterStatus.QueueMemoryTooSmall,
            VirtioBlockDescriptorChainPlanStatus.Success,
            VirtqueueSplitRingPlanStatus.Success);

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockDescriptorChainWriterStatus.Success
            && result.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.HeadDescriptorIndex == 0
            && result.NextAvailableRingIndex == 0
            && result.DescriptorsWritten == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockDescriptorChainWriterStatus.QueueMemoryTooSmall
            && failed.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.Success
            && failed.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && failed.HeadDescriptorIndex == 0
            && failed.NextAvailableRingIndex == 0
            && failed.DescriptorsWritten == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockDescriptorChainWriterResult>());
    }

    private static VirtioBlockDescriptorChainPlanResult CreateDescriptorPlan(
        VirtqueueSplitRingPlanResult queue,
        ushort firstDescriptorIndex,
        ushort availableRingIndex)
    {
        return VirtioBlockDescriptorChainPlan.Create(
            CreateSuccessfulTransaction(queue.Plan.QueueSize),
            queue,
            firstDescriptorIndex,
            availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            availableRingIndex,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress);
    }

    private static VirtioBlockDescriptorChainPlanResult CreateFailedDescriptorPlan(
        VirtqueueSplitRingPlanResult queue)
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
                queueSize: queue.Plan.QueueSize,
                requestHeader: requestHeader,
                destination: destination,
                status: status);

        return VirtioBlockDescriptorChainPlan.Create(
            failedTransaction,
            queue,
            firstDescriptorIndex: 0,
            availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            availableRingIndex: 0,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress);
    }

    private static VirtioBlockReadTransactionPlanResult CreateSuccessfulTransaction(
        ushort queueSize)
    {
        Span<byte> requestHeader = stackalloc byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[1024];
        Span<byte> status = stackalloc byte[(int)VirtioBlockRequestPlan.StatusLength];

        return VirtioBlockReadTransactionPlan.Create(
            sectorSizeBytes: VirtioBlockReadTransactionPlan.RequiredSectorSizeBytes,
            deviceSectorCount: 16,
            firstSector: 2,
            sectorCount: 2,
            queueSize,
            requestHeader,
            destination,
            status);
    }

    private static bool DescriptorMatches(
        ReadOnlySpan<byte> queueMemory,
        uint descriptorOffset,
        ulong expectedAddress,
        uint expectedLength,
        ushort expectedFlags,
        ushort next)
    {
        ReadOnlySpan<byte> descriptor = queueMemory.Slice((int)descriptorOffset, 16);
        return EndianPrimitives.ReadUInt64LittleEndian(descriptor.Slice(0, 8)) == expectedAddress
            && EndianPrimitives.ReadUInt32LittleEndian(descriptor.Slice(8, 4)) == expectedLength
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(12, 2)) == expectedFlags
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(14, 2)) == next;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static bool MemoryOutsideWriterRangesUnchanged(
        ReadOnlySpan<byte> actual,
        ReadOnlySpan<byte> original,
        VirtioBlockDescriptorChainPlan plan)
    {
        for (int i = 0; i < actual.Length; i++)
        {
            if (IsInWrittenRange(i, plan))
            {
                continue;
            }

            if (actual[i] != original[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInWrittenRange(
        int index,
        VirtioBlockDescriptorChainPlan plan)
    {
        return IsInRange(index, plan.RequestHeaderDescriptor.DescriptorTableOffset, 16)
            || IsInRange(index, plan.DataBufferDescriptor.DescriptorTableOffset, 16)
            || IsInRange(index, plan.StatusByteDescriptor.DescriptorTableOffset, 16)
            || IsInRange(index, plan.AvailableRingEntryOffset, sizeof(ushort))
            || IsInRange(index, plan.QueuePlan.AvailableRing.Offset + 2, sizeof(ushort));
    }

    private static bool IsInRange(
        int index,
        uint offset,
        int length)
    {
        uint current = (uint)index;
        uint end = offset + (uint)length;
        return current >= offset && current < end;
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
