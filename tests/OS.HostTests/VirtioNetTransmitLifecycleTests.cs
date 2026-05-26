using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class VirtioNetTransmitLifecycleTests
{
    private const ushort RxQueueSize = 8;
    private const ushort TxQueueSize = 4;
    private const ulong HeaderPhysicalAddress = 0x0040_0000UL;
    private const ulong FramePhysicalAddress = 0x0050_0000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtionet.tx.lifecycle writes header descriptors", TestWritesHeaderDescriptors, ref failures);
        Run("drivers.virtionet.tx.lifecycle writes available ring head", TestWritesAvailableRingHead, ref failures);
        Run("drivers.virtionet.tx.lifecycle preserves unrelated memory", TestPreservesUnrelatedMemory, ref failures);
        Run("drivers.virtionet.tx.lifecycle rejects failed queue plan", TestRejectsFailedQueuePlan, ref failures);
        Run("drivers.virtionet.tx.lifecycle rejects header buffer", TestRejectsHeaderBuffer, ref failures);
        Run("drivers.virtionet.tx.lifecycle rejects frame bounds", TestRejectsFrameBounds, ref failures);
        Run("drivers.virtionet.tx.lifecycle rejects descriptor range", TestRejectsDescriptorRange, ref failures);
        Run("drivers.virtionet.tx.lifecycle rejects queue memory and overflow", TestRejectsQueueMemoryAndOverflow, ref failures);
        Run(
            "drivers.virtionet.tx.lifecycle default result is not success",
            TestDefaultResultIsNotSuccess,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio-net transmit lifecycle host tests failed: " + failures);
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

    private static bool TestWritesHeaderDescriptors()
    {
        VirtioNetQueuePlanResult queuePlan = CreateQueuePlan();
        if (!queuePlan.Succeeded)
        {
            return false;
        }

        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + 2];
        byte[] frame = CreateEthernetFrame(payloadBytes: 4);
        byte[] queueMemory = CreateQueueMemory(queuePlan);
        Array.Fill(header, (byte)0xcc);
        Array.Fill(queueMemory, (byte)0xcc);

        VirtioNetTransmitLifecycleResult result = Publish(
            queuePlan,
            firstDescriptorIndex: 1,
            availableRingIndex: 9,
            frame,
            header,
            queueMemory);

        if (!result.Succeeded ||
            result.Status != VirtioNetTransmitLifecycleStatus.Success ||
            result.HeadDescriptorIndex != 1 ||
            result.NextAvailableRingIndex != 10 ||
            result.DescriptorsWritten != VirtioNetTransmitLifecycle.RequiredDescriptorCount ||
            result.HeaderBytesWritten != VirtioNetHeader.HeaderLengthWithoutMergeableBuffers ||
            result.EthernetFrameBytes != frame.Length)
        {
            return false;
        }

        VirtioNetTransmitLifecyclePlan plan = result.Plan;
        return BytesAreZero(header.AsSpan(0, VirtioNetHeader.HeaderLengthWithoutMergeableBuffers))
            && header[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers] == 0xcc
            && header[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + 1] == 0xcc
            && DescriptorMatches(
                queueMemory,
                plan.HeaderDescriptor.DescriptorTableOffset,
                HeaderPhysicalAddress,
                VirtioNetTransmitLifecycle.TxHeaderBytes,
                flags: 1,
                next: 2)
            && DescriptorMatches(
                queueMemory,
                plan.FrameDescriptor.DescriptorTableOffset,
                FramePhysicalAddress,
                (uint)frame.Length,
                flags: 0,
                next: 0)
            && plan.HeaderDescriptor.DeviceReadable
            && !plan.HeaderDescriptor.DeviceWritable
            && plan.FrameDescriptor.DeviceReadable
            && !plan.FrameDescriptor.DeviceWritable
            && plan.HeaderDescriptor.MemoryRange.EndExclusive ==
                HeaderPhysicalAddress + VirtioNetTransmitLifecycle.TxHeaderBytes
            && plan.FrameDescriptor.MemoryRange.EndExclusive ==
                FramePhysicalAddress + (uint)frame.Length
            && !VirtioNetTransmitLifecycle.AllocatesDmaMemory
            && !VirtioNetTransmitLifecycle.PerformsMmio
            && !VirtioNetTransmitLifecycle.NotifiesDevice
            && !VirtioNetTransmitLifecycle.WaitsForUsedRingCompletion
            && !VirtioNetTransmitLifecycle.PerformsPacketTransmit;
    }

    private static bool TestWritesAvailableRingHead()
    {
        VirtioNetQueuePlanResult queuePlan = CreateQueuePlan();
        if (!queuePlan.Succeeded)
        {
            return false;
        }

        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] frame = CreateEthernetFrame(payloadBytes: 0);
        byte[] queueMemory = CreateQueueMemory(queuePlan);
        Array.Fill(queueMemory, (byte)0x5a);

        VirtioNetTransmitLifecycleResult result = Publish(
            queuePlan,
            firstDescriptorIndex: 0,
            availableRingIndex: ushort.MaxValue,
            frame,
            header,
            queueMemory);
        if (!result.Succeeded)
        {
            return false;
        }

        VirtqueueSplitRingPlan txQueue = queuePlan.Plan.TxQueue;
        int availableFlagsOffset = (int)txQueue.AvailableRing.Offset;
        int availableIndexOffset = availableFlagsOffset + 2;
        int availableEntryOffset = (int)result.Plan.AvailableRingEntryOffset;
        int previousAvailableEntryOffset = availableEntryOffset - 2;

        return ReadUInt16(queueMemory, availableIndexOffset) == 0
            && ReadUInt16(queueMemory, availableEntryOffset) == result.Plan.HeadDescriptorIndex
            && result.Plan.AvailableRingSlot == (TxQueueSize - 1)
            && queueMemory[availableFlagsOffset] == 0x5a
            && queueMemory[availableFlagsOffset + 1] == 0x5a
            && queueMemory[previousAvailableEntryOffset] == 0x5a
            && queueMemory[previousAvailableEntryOffset + 1] == 0x5a;
    }

    private static bool TestPreservesUnrelatedMemory()
    {
        VirtioNetQueuePlanResult queuePlan = CreateQueuePlan();
        if (!queuePlan.Succeeded)
        {
            return false;
        }

        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + 1];
        byte[] frame = CreateEthernetFrame(payloadBytes: 2);
        byte[] queueMemory = CreateQueueMemory(queuePlan);
        for (int i = 0; i < queueMemory.Length; i++)
        {
            queueMemory[i] = (byte)(i + 23);
        }

        byte[] originalQueue = new byte[queueMemory.Length];
        queueMemory.CopyTo(originalQueue, 0);
        header.AsSpan().Fill(0x7c);

        VirtioNetTransmitLifecycleResult result = Publish(
            queuePlan,
            firstDescriptorIndex: 1,
            availableRingIndex: 1,
            frame,
            header,
            queueMemory);

        return result.Succeeded
            && MemoryOutsideWriterRangesUnchanged(queueMemory, originalQueue, result.Plan)
            && header[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers] == 0x7c;
    }

    private static bool TestRejectsFailedQueuePlan()
    {
        VirtioNetQueuePlanResult failedPlan = VirtioNetQueuePlan.Create(
            availableQueueCount: 1,
            rxQueueSize: RxQueueSize,
            txQueueSize: TxQueueSize,
            rxBufferCount: 2,
            frameBufferBytes: VirtioNetQueuePlan.MaximumFrameBufferBytes);
        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] frame = CreateEthernetFrame(payloadBytes: 0);
        Span<byte> queueMemory = stackalloc byte[16];
        queueMemory.Fill(0x66);

        VirtioNetTransmitLifecycleResult result = Publish(
            failedPlan,
            firstDescriptorIndex: 0,
            availableRingIndex: 0,
            frame,
            header,
            queueMemory);

        return !failedPlan.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetTransmitLifecycleStatus.QueuePlanFailed
            && result.QueuePlanStatus == VirtioNetQueuePlanStatus.InsufficientQueueCount
            && AllBytesEqual(queueMemory, 0x66);
    }

    private static bool TestRejectsHeaderBuffer()
    {
        VirtioNetQueuePlanResult queuePlan = CreateQueuePlan();
        if (!queuePlan.Succeeded)
        {
            return false;
        }

        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1];
        byte[] frame = CreateEthernetFrame(payloadBytes: 0);
        byte[] queueMemory = CreateQueueMemory(queuePlan);
        Array.Fill(header, (byte)0x91);
        Array.Fill(queueMemory, (byte)0x91);

        VirtioNetTransmitLifecycleResult result = Publish(
            queuePlan,
            firstDescriptorIndex: 0,
            availableRingIndex: 0,
            frame,
            header,
            queueMemory);

        return !result.Succeeded
            && result.Status == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall
            && result.HeaderStatus == VirtioNetHeaderStatus.BufferTooSmall
            && AllBytesEqual(header, 0x91)
            && AllBytesEqual(queueMemory, 0x91);
    }

    private static bool TestRejectsFrameBounds()
    {
        VirtioNetQueuePlanResult queuePlan = CreateQueuePlan();
        if (!queuePlan.Succeeded)
        {
            return false;
        }

        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] tooSmallFrame = new byte[VirtioNetQueuePlan.EthernetHeaderLength - 1];
        byte[] tooLargeFrame = new byte[queuePlan.Plan.TxFrame.EthernetFrameBytes + 1];
        byte[] queueMemory = CreateQueueMemory(queuePlan);
        Array.Fill(header, (byte)0x44);
        Array.Fill(queueMemory, (byte)0x44);

        VirtioNetTransmitLifecycleResult tooSmall = Publish(
            queuePlan,
            firstDescriptorIndex: 0,
            availableRingIndex: 0,
            tooSmallFrame,
            header,
            queueMemory);
        VirtioNetTransmitLifecycleResult tooLarge = Publish(
            queuePlan,
            firstDescriptorIndex: 0,
            availableRingIndex: 0,
            tooLargeFrame,
            header,
            queueMemory);

        return !tooSmall.Succeeded
            && tooSmall.Status == VirtioNetTransmitLifecycleStatus.EthernetFrameTooSmall
            && !tooLarge.Succeeded
            && tooLarge.Status == VirtioNetTransmitLifecycleStatus.EthernetFrameTooLarge
            && AllBytesEqual(header, 0x44)
            && AllBytesEqual(queueMemory, 0x44);
    }

    private static bool TestRejectsDescriptorRange()
    {
        VirtioNetQueuePlanResult queuePlan = CreateQueuePlan();
        if (!queuePlan.Succeeded)
        {
            return false;
        }

        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] frame = CreateEthernetFrame(payloadBytes: 0);
        byte[] queueMemory = CreateQueueMemory(queuePlan);
        Array.Fill(queueMemory, (byte)0x21);

        VirtioNetTransmitLifecycleResult insufficientDescriptors =
            VirtioNetTransmitLifecycle.Publish(
            queuePlan,
            firstDescriptorIndex: 0,
            availableDescriptorCount: 1,
            availableRingIndex: 0,
            txHeaderPhysicalAddress: HeaderPhysicalAddress,
            ethernetFramePhysicalAddress: FramePhysicalAddress,
            ethernetFrame: frame,
            txHeader: header,
            queueMemory: queueMemory);
        VirtioNetTransmitLifecycleResult indexOutOfRange = Publish(
            queuePlan,
            firstDescriptorIndex: (ushort)(TxQueueSize - 1),
            availableRingIndex: 0,
            frame,
            header,
            queueMemory);

        return !insufficientDescriptors.Succeeded
            && insufficientDescriptors.Status ==
                VirtioNetTransmitLifecycleStatus.InsufficientDescriptors
            && !indexOutOfRange.Succeeded
            && indexOutOfRange.Status == VirtioNetTransmitLifecycleStatus.DescriptorIndexOutOfRange
            && AllBytesEqual(queueMemory, 0x21);
    }

    private static bool TestRejectsQueueMemoryAndOverflow()
    {
        VirtioNetQueuePlanResult queuePlan = CreateQueuePlan();
        if (!queuePlan.Succeeded)
        {
            return false;
        }

        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] frame = CreateEthernetFrame(payloadBytes: 0);
        byte[] shortQueueMemory = new byte[(int)queuePlan.Plan.TxQueue.EndOffset - 1];
        Array.Fill(shortQueueMemory, (byte)0x17);

        VirtioNetTransmitLifecycleResult shortQueue = Publish(
            queuePlan,
            firstDescriptorIndex: 0,
            availableRingIndex: 0,
            frame,
            header,
            shortQueueMemory);

        byte[] queueMemory = CreateQueueMemory(queuePlan);
        Array.Fill(queueMemory, (byte)0x18);
        VirtioNetTransmitLifecycleResult physicalOverflow =
            VirtioNetTransmitLifecycle.Publish(
            queuePlan,
            firstDescriptorIndex: 0,
            availableDescriptorCount: VirtioNetTransmitLifecycle.RequiredDescriptorCount,
            availableRingIndex: 0,
            txHeaderPhysicalAddress: ulong.MaxValue - 8,
            ethernetFramePhysicalAddress: FramePhysicalAddress,
            ethernetFrame: frame,
            txHeader: header,
            queueMemory: queueMemory);

        return !shortQueue.Succeeded
            && shortQueue.Status == VirtioNetTransmitLifecycleStatus.QueueMemoryTooSmall
            && AllBytesEqual(shortQueueMemory, 0x17)
            && !physicalOverflow.Succeeded
            && physicalOverflow.Status == VirtioNetTransmitLifecycleStatus.PhysicalRangeOverflow
            && AllBytesEqual(queueMemory, 0x18);
    }

    private static VirtioNetTransmitLifecycleResult Publish(
        VirtioNetQueuePlanResult queuePlan,
        ushort firstDescriptorIndex,
        ushort availableRingIndex,
        ReadOnlySpan<byte> ethernetFrame,
        Span<byte> txHeader,
        Span<byte> queueMemory)
    {
        return VirtioNetTransmitLifecycle.Publish(
            queuePlan,
            firstDescriptorIndex,
            VirtioNetTransmitLifecycle.RequiredDescriptorCount,
            availableRingIndex,
            HeaderPhysicalAddress,
            FramePhysicalAddress,
            ethernetFrame,
            txHeader,
            queueMemory);
    }

    private static VirtioNetQueuePlanResult CreateQueuePlan()
    {
        return VirtioNetQueuePlan.Create(
            availableQueueCount: VirtioNetQueuePlan.MinimumQueueCount,
            rxQueueSize: RxQueueSize,
            txQueueSize: TxQueueSize,
            rxBufferCount: 2,
            frameBufferBytes: VirtioNetQueuePlan.MaximumFrameBufferBytes);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetTransmitLifecycleResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioNetTransmitLifecycleStatus.Success
            && result.QueuePlanStatus == VirtioNetQueuePlanStatus.Success
            && result.HeaderStatus == VirtioNetHeaderStatus.Success
            && result.HeadDescriptorIndex == 0
            && result.NextAvailableRingIndex == 0
            && result.DescriptorsWritten == 0
            && result.HeaderBytesWritten == 0
            && result.EthernetFrameBytes == 0
            && result.Plan.AvailableDescriptorCount == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetTransmitLifecycleResult>();
    }

    private static byte[] CreateQueueMemory(VirtioNetQueuePlanResult queuePlan)
    {
        return new byte[(int)queuePlan.Plan.TxQueue.EndOffset];
    }

    private static byte[] CreateEthernetFrame(int payloadBytes)
    {
        byte[] frame = new byte[EthernetHeader.HeaderLength + payloadBytes];
        EthernetHeader ethernet = new EthernetHeader(
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x02),
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01),
            EthernetHeader.EtherTypeIpv4);

        PacketParseStatus status = EthernetHeader.TryWrite(frame, ethernet);
        if (status != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic Ethernet header.");
        }

        for (int index = 0; index < payloadBytes; index++)
        {
            frame[EthernetHeader.HeaderLength + index] = (byte)(0xb0 + index);
        }

        return frame;
    }

    private static bool DescriptorMatches(
        ReadOnlySpan<byte> queueMemory,
        uint descriptorOffset,
        ulong expectedAddress,
        uint expectedLength,
        ushort flags,
        ushort next)
    {
        ReadOnlySpan<byte> descriptor = queueMemory.Slice((int)descriptorOffset, 16);
        return EndianPrimitives.ReadUInt64LittleEndian(descriptor.Slice(0, 8)) == expectedAddress
            && EndianPrimitives.ReadUInt32LittleEndian(descriptor.Slice(8, 4)) == expectedLength
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(12, 2)) == flags
            && EndianPrimitives.ReadUInt16LittleEndian(descriptor.Slice(14, 2)) == next;
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static bool MemoryOutsideWriterRangesUnchanged(
        ReadOnlySpan<byte> actual,
        ReadOnlySpan<byte> original,
        VirtioNetTransmitLifecyclePlan plan)
    {
        for (int index = 0; index < actual.Length; index++)
        {
            if (IsInWrittenRange(index, plan))
            {
                continue;
            }

            if (actual[index] != original[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInWrittenRange(
        int index,
        VirtioNetTransmitLifecyclePlan plan)
    {
        return IsInRange(index, plan.HeaderDescriptor.DescriptorTableOffset, 16)
            || IsInRange(index, plan.FrameDescriptor.DescriptorTableOffset, 16)
            || IsInRange(index, plan.AvailableRingEntryOffset, sizeof(ushort))
            || IsInRange(index, plan.TxQueue.AvailableRing.Offset + 2, sizeof(ushort));
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

    private static bool BytesAreZero(ReadOnlySpan<byte> bytes)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }

        return true;
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
