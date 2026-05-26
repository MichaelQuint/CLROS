using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockUsedRingCompletionReaderTests
{
    private const ushort QueueSize = 8;
    private const ushort ExpectedDescriptorHead = 3;
    private const uint ExpectedDataByteCount = 1024;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage.virtioblk completion parses successful used entry", TestParsesSuccessfulUsedEntry, ref failures);
        Run("storage.virtioblk completion advances wrapped used index", TestAdvancesWrappedUsedIndex, ref failures);
        Run("storage.virtioblk completion rejects unexpected descriptor head", TestRejectsUnexpectedDescriptorHead, ref failures);
        Run("storage.virtioblk completion rejects unexpected byte count", TestRejectsUnexpectedByteCount, ref failures);
        Run("storage.virtioblk completion reports device io error", TestReportsDeviceIoError, ref failures);
        Run("storage.virtioblk completion rejects undersized used ring", TestRejectsUndersizedUsedRing, ref failures);
        Run("storage.virtioblk completion rejects unknown status byte", TestRejectsUnknownStatusByte, ref failures);
        Run("storage.virtioblk completion default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio block used-ring completion reader host tests failed: " + failures);
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

    private static bool TestParsesSuccessfulUsedEntry()
    {
        const ushort previousUsedIndex = 9;
        byte[] usedRing = CreateUsedRing(
            QueueSize,
            previousUsedIndex,
            unchecked((ushort)(previousUsedIndex + 1)),
            ExpectedDescriptorHead,
            ExpectedDataByteCount);
        byte[] status = { (byte)VirtioBlockDeviceStatus.Ok };

        VirtioBlockUsedRingCompletionReaderResult result =
            VirtioBlockUsedRingCompletionReader.Read(
                QueueSize,
                previousUsedIndex,
                ExpectedDescriptorHead,
                ExpectedDataByteCount,
                status,
                usedRing);

        return result.Succeeded
            && result.Status == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.QueueStatus == VirtqueueSplitRingPlanStatus.Success
            && result.PreviousUsedIndex == previousUsedIndex
            && result.CurrentUsedIndex == previousUsedIndex + 1
            && result.NextUsedIndex == previousUsedIndex + 1
            && result.UsedRingSlot == 1
            && result.UsedRingElementOffset == 12
            && result.DescriptorHead == ExpectedDescriptorHead
            && result.UsedByteCount == ExpectedDataByteCount
            && result.StatusByte == (byte)VirtioBlockDeviceStatus.Ok
            && !VirtioBlockUsedRingCompletionReader.PerformsMmio
            && !VirtioBlockUsedRingCompletionReader.PerformsInterruptPolling
            && !VirtioBlockUsedRingCompletionReader.HandlesInterrupts
            && !VirtioBlockUsedRingCompletionReader.PerformsDmaMapping
            && !VirtioBlockUsedRingCompletionReader.NotifiesDevice
            && !VirtioBlockUsedRingCompletionReader.PerformsDiskRead;
    }

    private static bool TestAdvancesWrappedUsedIndex()
    {
        const ushort previousUsedIndex = ushort.MaxValue;
        byte[] usedRing = CreateUsedRing(
            QueueSize,
            previousUsedIndex,
            currentUsedIndex: 0,
            ExpectedDescriptorHead,
            ExpectedDataByteCount);
        byte[] status = { (byte)VirtioBlockDeviceStatus.Ok };

        VirtioBlockUsedRingCompletionReaderResult result =
            VirtioBlockUsedRingCompletionReader.Read(
                QueueSize,
                previousUsedIndex,
                ExpectedDescriptorHead,
                ExpectedDataByteCount,
                status,
                usedRing);

        return result.Succeeded
            && result.CurrentUsedIndex == 0
            && result.NextUsedIndex == 0
            && result.UsedRingSlot == 7
            && result.UsedRingElementOffset == 60
            && result.DescriptorHead == ExpectedDescriptorHead;
    }

    private static bool TestRejectsUnexpectedDescriptorHead()
    {
        const ushort previousUsedIndex = 4;
        byte[] usedRing = CreateUsedRing(
            QueueSize,
            previousUsedIndex,
            unchecked((ushort)(previousUsedIndex + 1)),
            descriptorHead: ExpectedDescriptorHead + 1u,
            usedByteCount: ExpectedDataByteCount);
        byte[] status = { (byte)VirtioBlockDeviceStatus.Ok };

        VirtioBlockUsedRingCompletionReaderResult result =
            VirtioBlockUsedRingCompletionReader.Read(
                QueueSize,
                previousUsedIndex,
                ExpectedDescriptorHead,
                ExpectedDataByteCount,
                status,
                usedRing);

        return !result.Succeeded
            && result.Status == VirtioBlockUsedRingCompletionReaderStatus.UnexpectedDescriptorHead
            && result.DescriptorHead == ExpectedDescriptorHead + 1u
            && result.UsedByteCount == ExpectedDataByteCount
            && result.StatusByte == (byte)VirtioBlockDeviceStatus.Ok;
    }

    private static bool TestRejectsUnexpectedByteCount()
    {
        const ushort previousUsedIndex = 6;
        byte[] usedRing = CreateUsedRing(
            QueueSize,
            previousUsedIndex,
            unchecked((ushort)(previousUsedIndex + 1)),
            ExpectedDescriptorHead,
            usedByteCount: ExpectedDataByteCount - 1);
        byte[] status = { (byte)VirtioBlockDeviceStatus.Ok };

        VirtioBlockUsedRingCompletionReaderResult result =
            VirtioBlockUsedRingCompletionReader.Read(
                QueueSize,
                previousUsedIndex,
                ExpectedDescriptorHead,
                ExpectedDataByteCount,
                status,
                usedRing);

        return !result.Succeeded
            && result.Status == VirtioBlockUsedRingCompletionReaderStatus.UnexpectedByteCount
            && result.DescriptorHead == ExpectedDescriptorHead
            && result.UsedByteCount == ExpectedDataByteCount - 1
            && result.StatusByte == (byte)VirtioBlockDeviceStatus.Ok;
    }

    private static bool TestReportsDeviceIoError()
    {
        const ushort previousUsedIndex = 11;
        byte[] usedRing = CreateUsedRing(
            QueueSize,
            previousUsedIndex,
            unchecked((ushort)(previousUsedIndex + 1)),
            ExpectedDescriptorHead,
            ExpectedDataByteCount);
        byte[] status = { (byte)VirtioBlockDeviceStatus.IoError };

        VirtioBlockUsedRingCompletionReaderResult result =
            VirtioBlockUsedRingCompletionReader.Read(
                QueueSize,
                previousUsedIndex,
                ExpectedDescriptorHead,
                ExpectedDataByteCount,
                status,
                usedRing);

        return !result.Succeeded
            && result.Status == VirtioBlockUsedRingCompletionReaderStatus.DeviceIoError
            && result.DescriptorHead == ExpectedDescriptorHead
            && result.UsedByteCount == ExpectedDataByteCount
            && result.StatusByte == (byte)VirtioBlockDeviceStatus.IoError;
    }

    private static bool TestRejectsUndersizedUsedRing()
    {
        const ushort previousUsedIndex = 1;
        byte[] usedRing = new byte[RequiredUsedRingLength(QueueSize) - 1];
        byte[] status = { (byte)VirtioBlockDeviceStatus.Ok };

        VirtioBlockUsedRingCompletionReaderResult result =
            VirtioBlockUsedRingCompletionReader.Read(
                QueueSize,
                previousUsedIndex,
                ExpectedDescriptorHead,
                ExpectedDataByteCount,
                status,
                usedRing);

        return !result.Succeeded
            && result.Status == VirtioBlockUsedRingCompletionReaderStatus.UsedRingTooSmall
            && result.QueueStatus == VirtqueueSplitRingPlanStatus.Success
            && result.PreviousUsedIndex == previousUsedIndex;
    }

    private static bool TestRejectsUnknownStatusByte()
    {
        const ushort previousUsedIndex = 2;
        byte[] usedRing = CreateUsedRing(
            QueueSize,
            previousUsedIndex,
            unchecked((ushort)(previousUsedIndex + 1)),
            ExpectedDescriptorHead,
            ExpectedDataByteCount);
        byte[] status = { 0xff };

        VirtioBlockUsedRingCompletionReaderResult result =
            VirtioBlockUsedRingCompletionReader.Read(
                QueueSize,
                previousUsedIndex,
                ExpectedDescriptorHead,
                ExpectedDataByteCount,
                status,
                usedRing);

        return !result.Succeeded
            && result.Status == VirtioBlockUsedRingCompletionReaderStatus.UnknownStatusByte
            && result.StatusByte == 0xff
            && result.DescriptorHead == ExpectedDescriptorHead
            && result.UsedByteCount == ExpectedDataByteCount;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioBlockUsedRingCompletionReaderResult result = default;
        VirtioBlockUsedRingCompletionReaderResult failed =
            VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.InvalidQueueSize,
                VirtqueueSplitRingPlanStatus.InvalidQueueSize,
                previousUsedIndex: 7);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.QueueStatus == VirtqueueSplitRingPlanStatus.Success
            && result.PreviousUsedIndex == 0
            && result.CurrentUsedIndex == 0
            && result.NextUsedIndex == 0
            && result.UsedRingSlot == 0
            && result.UsedRingElementOffset == 0
            && result.DescriptorHead == 0
            && result.UsedByteCount == 0
            && result.StatusByte == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockUsedRingCompletionReaderStatus.InvalidQueueSize
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockUsedRingCompletionReaderResult>();
    }

    private static byte[] CreateUsedRing(
        ushort queueSize,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        uint descriptorHead,
        uint usedByteCount)
    {
        byte[] usedRing = new byte[RequiredUsedRingLength(queueSize)];
        EndianPrimitives.WriteUInt16LittleEndian(
            usedRing.AsSpan(2, sizeof(ushort)),
            currentUsedIndex);

        ushort slot = (ushort)(previousUsedIndex & (queueSize - 1));
        int elementOffset = 4 + (slot * 8);
        EndianPrimitives.WriteUInt32LittleEndian(
            usedRing.AsSpan(elementOffset, sizeof(uint)),
            descriptorHead);
        EndianPrimitives.WriteUInt32LittleEndian(
            usedRing.AsSpan(elementOffset + 4, sizeof(uint)),
            usedByteCount);

        return usedRing;
    }

    private static int RequiredUsedRingLength(ushort queueSize)
    {
        return (int)(VirtqueueSplitRingPlan.UsedRingHeaderLength +
            ((uint)queueSize * VirtqueueSplitRingPlan.UsedRingElementLength));
    }
}
