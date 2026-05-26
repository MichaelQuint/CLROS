using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class VirtioNetReceiveLifecycleTests
{
    private const ushort QueueSize = 8;
    private const ulong FirstBufferPhysicalAddress = 0x0040_0000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtionet.rx.lifecycle publishes receive buffers", TestPublishesReceiveBuffers, ref failures);
        Run("drivers.virtionet.rx.lifecycle wraps available ring slots", TestWrapsAvailableRingSlots, ref failures);
        Run("drivers.virtionet.rx.lifecycle completes and classifies buffer", TestCompletesAndClassifiesBuffer, ref failures);
        Run("drivers.virtionet.rx.lifecycle rejects short queue memory", TestRejectsShortQueueMemory, ref failures);
        Run("drivers.virtionet.rx.lifecycle rejects descriptor exhaustion", TestRejectsDescriptorExhaustion, ref failures);
        Run("drivers.virtionet.rx.lifecycle reports completion failures", TestReportsCompletionFailures, ref failures);
        Run("drivers.virtionet.rx.lifecycle publish default result is not success", TestPublishDefaultResultIsNotSuccess, ref failures);
        Run("drivers.virtionet.rx.lifecycle completion default result is not success", TestCompletionDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Virtio-net RX lifecycle host tests failed: " + failures);
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

    private static bool TestPublishesReceiveBuffers()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0xcc);

        VirtioNetReceiveLifecyclePublishResult result =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
                queueSize: QueueSize,
                firstDescriptorIndex: 2,
                availableDescriptorCount: 3,
                previousAvailableRingIndex: 5,
                previousUsedIndex: 9,
                firstReceiveBufferPhysicalAddress: FirstBufferPhysicalAddress,
                receiveBufferStrideBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                receiveBufferCount: 3,
                frameBufferBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                queueMemory);

        return result.Succeeded
            && result.Status == VirtioNetReceiveLifecyclePublishStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.QueueSize == QueueSize
            && result.FirstDescriptorIndex == 2
            && result.PublishedDescriptorCount == 3
            && result.PreviousAvailableRingIndex == 5
            && result.NextAvailableRingIndex == 8
            && result.PreviousUsedIndex == 9
            && result.ExpectedNextUsedIndex == 10
            && result.FirstAvailableRingSlot == 5
            && result.LastAvailableRingSlot == 7
            && result.FrameBufferBytes == VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes
            && ReadDescriptorAddress(queueMemory, queue.Plan, 2) == FirstBufferPhysicalAddress
            && ReadDescriptorAddress(queueMemory, queue.Plan, 3) ==
                FirstBufferPhysicalAddress + VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes
            && ReadDescriptorLength(queueMemory, queue.Plan, 4) ==
                VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes
            && ReadDescriptorFlags(queueMemory, queue.Plan, 2) == 0x0002
            && ReadDescriptorNext(queueMemory, queue.Plan, 2) == 0
            && ReadUInt16(queueMemory, (int)result.FirstAvailableRingEntryOffset) == 2
            && ReadUInt16(queueMemory, GetAvailableEntryOffset(queue.Plan, 6)) == 3
            && ReadUInt16(queueMemory, GetAvailableEntryOffset(queue.Plan, 7)) == 4
            && ReadUInt16(queueMemory, (int)queue.Plan.AvailableRing.Offset + 2) == 8
            && !VirtioNetReceiveLifecycle.AllocatesReceiveBuffers
            && !VirtioNetReceiveLifecycle.PerformsDmaMapping
            && !VirtioNetReceiveLifecycle.PerformsMmio
            && !VirtioNetReceiveLifecycle.NotifiesDevice
            && !VirtioNetReceiveLifecycle.PollsDevice
            && !VirtioNetReceiveLifecycle.HandlesInterrupts
            && !VirtioNetReceiveLifecycle.CopiesPayload
            && !VirtioNetReceiveLifecycle.PerformsPacketIo;
    }

    private static bool TestWrapsAvailableRingSlots()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];

        VirtioNetReceiveLifecyclePublishResult result =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
                queueSize: QueueSize,
                firstDescriptorIndex: 0,
                availableDescriptorCount: 2,
                previousAvailableRingIndex: ushort.MaxValue,
                previousUsedIndex: ushort.MaxValue,
                firstReceiveBufferPhysicalAddress: FirstBufferPhysicalAddress,
                receiveBufferStrideBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                receiveBufferCount: 2,
                frameBufferBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                queueMemory);

        return result.Succeeded
            && result.FirstAvailableRingSlot == 7
            && result.LastAvailableRingSlot == 0
            && result.NextAvailableRingIndex == 1
            && result.ExpectedNextUsedIndex == 0
            && ReadUInt16(queueMemory, GetAvailableEntryOffset(queue.Plan, 7)) == 0
            && ReadUInt16(queueMemory, GetAvailableEntryOffset(queue.Plan, 0)) == 1
            && ReadUInt16(queueMemory, (int)queue.Plan.AvailableRing.Offset + 2) == 1;
    }

    private static bool TestCompletesAndClassifiesBuffer()
    {
        VirtioNetReceiveLifecyclePublishResult publish = CreatePublish(
            previousAvailableRingIndex: 4,
            previousUsedIndex: 6,
            firstDescriptorIndex: 3,
            receiveBufferCount: 2,
            out byte[] queueMemory);
        if (!publish.Succeeded)
        {
            return false;
        }

        byte[] receiveBuffer = CreateReceiveBuffer(
            EthernetHeader.EtherTypeIpv4,
            payloadBytes: 20);
        WriteUsedCompletion(
            publish.QueuePlan,
            queueMemory,
            publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            descriptorHead: 4,
            usedByteCount: (uint)receiveBuffer.Length);

        VirtioNetReceiveLifecycleCompletionResult result =
            VirtioNetReceiveLifecycle.CompleteReceiveBuffer(
                publish,
                queueMemory,
                receiveBuffer);

        return result.Succeeded
            && result.Status == VirtioNetReceiveLifecycleCompletionStatus.Success
            && result.PublishStatus == VirtioNetReceiveLifecyclePublishStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.DescriptorHead == 4
            && result.BufferIndex == 1
            && result.PreviousUsedIndex == 6
            && result.CurrentUsedIndex == 7
            && result.NextUsedIndex == 7
            && result.UsedRingSlot == 6
            && result.UsedRingElementOffset == GetUsedEntryOffset(publish.QueuePlan, 6)
            && result.UsedByteCount == receiveBuffer.Length
            && result.FrameBufferBytes == VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes
            && result.Classification.PayloadKind == EthernetPayloadKind.Ipv4
            && result.Classification.PayloadOffset ==
                VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength
            && result.Classification.PayloadLength == 20;
    }

    private static bool TestRejectsShortQueueMemory()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset - 1];
        Array.Fill(queueMemory, (byte)0x5a);

        VirtioNetReceiveLifecyclePublishResult result =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
                queueSize: QueueSize,
                firstDescriptorIndex: 1,
                availableDescriptorCount: 1,
                previousAvailableRingIndex: 2,
                previousUsedIndex: 3,
                firstReceiveBufferPhysicalAddress: FirstBufferPhysicalAddress,
                receiveBufferStrideBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                receiveBufferCount: 1,
                frameBufferBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                queueMemory);

        return !result.Succeeded
            && result.Status == VirtioNetReceiveLifecyclePublishStatus.QueueMemoryTooSmall
            && result.NextAvailableRingIndex == result.PreviousAvailableRingIndex
            && result.ExpectedNextUsedIndex == result.PreviousUsedIndex
            && AllBytesEqual(queueMemory, 0x5a);
    }

    private static bool TestRejectsDescriptorExhaustion()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];

        VirtioNetReceiveLifecyclePublishResult insufficient =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
                queueSize: QueueSize,
                firstDescriptorIndex: 1,
                availableDescriptorCount: 1,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                firstReceiveBufferPhysicalAddress: FirstBufferPhysicalAddress,
                receiveBufferStrideBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                receiveBufferCount: 2,
                frameBufferBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                queueMemory);
        VirtioNetReceiveLifecyclePublishResult outOfRange =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
                queueSize: QueueSize,
                firstDescriptorIndex: 7,
                availableDescriptorCount: 2,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                firstReceiveBufferPhysicalAddress: FirstBufferPhysicalAddress,
                receiveBufferStrideBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                receiveBufferCount: 2,
                frameBufferBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                queueMemory);

        return !insufficient.Succeeded
            && insufficient.Status == VirtioNetReceiveLifecyclePublishStatus.InsufficientDescriptors
            && !outOfRange.Succeeded
            && outOfRange.Status == VirtioNetReceiveLifecyclePublishStatus.DescriptorRangeOutOfRange;
    }

    private static bool TestReportsCompletionFailures()
    {
        VirtioNetReceiveLifecyclePublishResult publish = CreatePublish(
            previousAvailableRingIndex: 2,
            previousUsedIndex: 4,
            firstDescriptorIndex: 1,
            receiveBufferCount: 2,
            out byte[] queueMemory);
        if (!publish.Succeeded)
        {
            return false;
        }

        byte[] receiveBuffer = CreateReceiveBuffer(
            EthernetHeader.EtherTypeArp,
            payloadBytes: 28);

        VirtioNetReceiveLifecycleCompletionResult noCompletion =
            VirtioNetReceiveLifecycle.CompleteReceiveBuffer(
                publish,
                queueMemory,
                receiveBuffer);

        WriteUsedCompletion(
            publish.QueuePlan,
            queueMemory,
            publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            descriptorHead: 7,
            usedByteCount: (uint)receiveBuffer.Length);
        VirtioNetReceiveLifecycleCompletionResult wrongHead =
            VirtioNetReceiveLifecycle.CompleteReceiveBuffer(
                publish,
                queueMemory,
                receiveBuffer);

        WriteUsedCompletion(
            publish.QueuePlan,
            queueMemory,
            publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            descriptorHead: 1,
            usedByteCount: publish.FrameBufferBytes + 1u);
        VirtioNetReceiveLifecycleCompletionResult wrongLength =
            VirtioNetReceiveLifecycle.CompleteReceiveBuffer(
                publish,
                queueMemory,
                receiveBuffer);

        WriteUsedCompletion(
            publish.QueuePlan,
            queueMemory,
            publish.PreviousUsedIndex,
            publish.ExpectedNextUsedIndex,
            descriptorHead: 1,
            usedByteCount: (uint)(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1));
        VirtioNetReceiveLifecycleCompletionResult classifierFailure =
            VirtioNetReceiveLifecycle.CompleteReceiveBuffer(
                publish,
                queueMemory,
                receiveBuffer);

        return !noCompletion.Succeeded
            && noCompletion.Status == VirtioNetReceiveLifecycleCompletionStatus.NoCompletion
            && !wrongHead.Succeeded
            && wrongHead.Status == VirtioNetReceiveLifecycleCompletionStatus.UnexpectedDescriptorHead
            && wrongHead.DescriptorHead == 7
            && !wrongLength.Succeeded
            && wrongLength.Status == VirtioNetReceiveLifecycleCompletionStatus.UnexpectedByteCount
            && wrongLength.UsedByteCount == publish.FrameBufferBytes + 1u
            && !classifierFailure.Succeeded
            && classifierFailure.Status == VirtioNetReceiveLifecycleCompletionStatus.FrameClassificationFailed
            && classifierFailure.ClassifierStatus == VirtioNetReceiveFrameClassifierStatus.BufferTooSmall;
    }

    private static bool TestPublishDefaultResultIsNotSuccess()
    {
        VirtioNetReceiveLifecyclePublishResult result = default;
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];

        VirtioNetReceiveLifecyclePublishResult failed =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
                queueSize: QueueSize,
                firstDescriptorIndex: 1,
                availableDescriptorCount: 1,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                firstReceiveBufferPhysicalAddress: FirstBufferPhysicalAddress,
                receiveBufferStrideBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                receiveBufferCount: 2,
                frameBufferBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                queueMemory);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioNetReceiveLifecyclePublishStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioNetReceiveLifecyclePublishStatus.InsufficientDescriptors
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetReceiveLifecyclePublishResult>();
    }

    private static bool TestCompletionDefaultResultIsNotSuccess()
    {
        VirtioNetReceiveLifecycleCompletionResult result = default;
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        byte[] receiveBuffer = CreateReceiveBuffer(EthernetHeader.EtherTypeArp, payloadBytes: 28);
        VirtioNetReceiveLifecyclePublishResult publish =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
                queueSize: QueueSize,
                firstDescriptorIndex: 1,
                availableDescriptorCount: 1,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                firstReceiveBufferPhysicalAddress: FirstBufferPhysicalAddress,
                receiveBufferStrideBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                receiveBufferCount: 2,
                frameBufferBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
                queueMemory);

        VirtioNetReceiveLifecycleCompletionResult failed =
            VirtioNetReceiveLifecycle.CompleteReceiveBuffer(
                publish,
                queueMemory,
                receiveBuffer);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioNetReceiveLifecycleCompletionStatus.Success
            && result.PublishStatus == VirtioNetReceiveLifecyclePublishStatus.Success
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioNetReceiveLifecycleCompletionStatus.PublishFailed
            && failed.PublishStatus == VirtioNetReceiveLifecyclePublishStatus.InsufficientDescriptors
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetReceiveLifecycleCompletionResult>();
    }

    private static VirtioNetReceiveLifecyclePublishResult CreatePublish(
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        ushort firstDescriptorIndex,
        ushort receiveBufferCount,
        out byte[] queueMemory)
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        queueMemory = new byte[(int)queue.Plan.EndOffset];

        VirtioNetReceiveLifecyclePublishResult publish =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
            queueSize: QueueSize,
            firstDescriptorIndex,
            availableDescriptorCount: receiveBufferCount,
            previousAvailableRingIndex,
            previousUsedIndex,
            firstReceiveBufferPhysicalAddress: FirstBufferPhysicalAddress,
            receiveBufferStrideBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
            receiveBufferCount,
            frameBufferBytes: VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes,
            queueMemory);

        if (publish.Succeeded)
        {
            EndianPrimitives.WriteUInt16LittleEndian(
                queueMemory.AsSpan((int)publish.QueuePlan.UsedRing.Offset + 2, sizeof(ushort)),
                previousUsedIndex);
        }

        return publish;
    }

    private static byte[] CreateReceiveBuffer(
        ushort etherType,
        int payloadBytes)
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            payloadBytes];

        EthernetHeader ethernetHeader = new EthernetHeader(
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x22),
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x11),
            etherType);

        PacketParseStatus status = EthernetHeader.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            ethernetHeader);
        if (status != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic Ethernet frame.");
        }

        int payloadOffset = VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength;
        for (int index = 0; index < payloadBytes; index++)
        {
            buffer[payloadOffset + index] = (byte)(0x80 + index);
        }

        return buffer;
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
        int elementOffset = GetUsedEntryOffset(queue, slot);
        EndianPrimitives.WriteUInt32LittleEndian(
            queueMemory.AsSpan(elementOffset, sizeof(uint)),
            descriptorHead);
        EndianPrimitives.WriteUInt32LittleEndian(
            queueMemory.AsSpan(elementOffset + sizeof(uint), sizeof(uint)),
            usedByteCount);
    }

    private static ulong ReadDescriptorAddress(
        byte[] queueMemory,
        VirtqueueSplitRingPlan queue,
        ushort descriptorIndex)
    {
        int offset = GetDescriptorOffset(queue, descriptorIndex);
        return EndianPrimitives.ReadUInt64LittleEndian(
            queueMemory.AsSpan(offset, sizeof(ulong)));
    }

    private static uint ReadDescriptorLength(
        byte[] queueMemory,
        VirtqueueSplitRingPlan queue,
        ushort descriptorIndex)
    {
        int offset = GetDescriptorOffset(queue, descriptorIndex) + 8;
        return EndianPrimitives.ReadUInt32LittleEndian(
            queueMemory.AsSpan(offset, sizeof(uint)));
    }

    private static ushort ReadDescriptorFlags(
        byte[] queueMemory,
        VirtqueueSplitRingPlan queue,
        ushort descriptorIndex)
    {
        int offset = GetDescriptorOffset(queue, descriptorIndex) + 12;
        return EndianPrimitives.ReadUInt16LittleEndian(
            queueMemory.AsSpan(offset, sizeof(ushort)));
    }

    private static ushort ReadDescriptorNext(
        byte[] queueMemory,
        VirtqueueSplitRingPlan queue,
        ushort descriptorIndex)
    {
        int offset = GetDescriptorOffset(queue, descriptorIndex) + 14;
        return EndianPrimitives.ReadUInt16LittleEndian(
            queueMemory.AsSpan(offset, sizeof(ushort)));
    }

    private static int GetDescriptorOffset(
        VirtqueueSplitRingPlan queue,
        ushort descriptorIndex)
    {
        return (int)queue.DescriptorTable.Offset +
            (descriptorIndex * (int)VirtqueueSplitRingPlan.DescriptorLength);
    }

    private static int GetAvailableEntryOffset(
        VirtqueueSplitRingPlan queue,
        int slot)
    {
        return (int)queue.AvailableRing.Offset +
            (int)VirtqueueSplitRingPlan.AvailableRingHeaderLength +
            (slot * (int)VirtqueueSplitRingPlan.AvailableRingEntryLength);
    }

    private static int GetUsedEntryOffset(
        VirtqueueSplitRingPlan queue,
        int slot)
    {
        return (int)queue.UsedRing.Offset +
            (int)VirtqueueSplitRingPlan.UsedRingHeaderLength +
            (slot * (int)VirtqueueSplitRingPlan.UsedRingElementLength);
    }

    private static ushort ReadUInt16(
        byte[] bytes,
        int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(
            bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static bool AllBytesEqual(
        ReadOnlySpan<byte> bytes,
        byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != value)
            {
                return false;
            }
        }

        return true;
    }
}
