using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockUsedRingCompletionReaderStatus
{
    Success = 0,
    InvalidQueueSize = 1,
    UsedRingTooSmall = 2,
    StatusBufferTooSmall = 3,
    NoCompletion = 4,
    UnexpectedUsedIndex = 5,
    UnexpectedDescriptorHead = 6,
    UnexpectedByteCount = 7,
    DeviceIoError = 8,
    DeviceUnsupported = 9,
    UnknownStatusByte = 10,
    ArithmeticOverflow = 11
}

public readonly struct VirtioBlockUsedRingCompletionReaderResult
{
    private VirtioBlockUsedRingCompletionReaderResult(
        VirtioBlockUsedRingCompletionReaderStatus status,
        VirtqueueSplitRingPlanStatus queueStatus,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        ushort nextUsedIndex,
        ushort usedRingSlot,
        uint usedRingElementOffset,
        uint descriptorHead,
        uint usedByteCount,
        byte statusByte)
    {
        IsInitialized = true;
        Status = status;
        QueueStatus = queueStatus;
        PreviousUsedIndex = previousUsedIndex;
        CurrentUsedIndex = currentUsedIndex;
        NextUsedIndex = nextUsedIndex;
        UsedRingSlot = usedRingSlot;
        UsedRingElementOffset = usedRingElementOffset;
        DescriptorHead = descriptorHead;
        UsedByteCount = usedByteCount;
        StatusByte = statusByte;
    }

    public bool IsInitialized { get; }

    public VirtioBlockUsedRingCompletionReaderStatus Status { get; }

    public VirtqueueSplitRingPlanStatus QueueStatus { get; }

    public ushort PreviousUsedIndex { get; }

    public ushort CurrentUsedIndex { get; }

    public ushort NextUsedIndex { get; }

    public ushort UsedRingSlot { get; }

    public uint UsedRingElementOffset { get; }

    public uint DescriptorHead { get; }

    public uint UsedByteCount { get; }

    public byte StatusByte { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockUsedRingCompletionReaderStatus.Success;

    public static VirtioBlockUsedRingCompletionReaderResult Success(
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        ushort usedRingSlot,
        uint usedRingElementOffset,
        uint descriptorHead,
        uint usedByteCount,
        byte statusByte)
    {
        return new VirtioBlockUsedRingCompletionReaderResult(
            VirtioBlockUsedRingCompletionReaderStatus.Success,
            VirtqueueSplitRingPlanStatus.Success,
            previousUsedIndex,
            currentUsedIndex,
            currentUsedIndex,
            usedRingSlot,
            usedRingElementOffset,
            descriptorHead,
            usedByteCount,
            statusByte);
    }

    public static VirtioBlockUsedRingCompletionReaderResult Fail(
        VirtioBlockUsedRingCompletionReaderStatus status,
        VirtqueueSplitRingPlanStatus queueStatus,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        ushort usedRingSlot,
        uint usedRingElementOffset,
        uint descriptorHead,
        uint usedByteCount,
        byte statusByte)
    {
        return new VirtioBlockUsedRingCompletionReaderResult(
            status,
            queueStatus,
            previousUsedIndex,
            currentUsedIndex,
            currentUsedIndex,
            usedRingSlot,
            usedRingElementOffset,
            descriptorHead,
            usedByteCount,
            statusByte);
    }

    public static VirtioBlockUsedRingCompletionReaderResult Fail(
        VirtioBlockUsedRingCompletionReaderStatus status,
        VirtqueueSplitRingPlanStatus queueStatus,
        ushort previousUsedIndex)
    {
        return new VirtioBlockUsedRingCompletionReaderResult(
            status,
            queueStatus,
            previousUsedIndex,
            previousUsedIndex,
            previousUsedIndex,
            usedRingSlot: 0,
            usedRingElementOffset: 0,
            descriptorHead: 0,
            usedByteCount: 0,
            statusByte: 0);
    }
}

public static class VirtioBlockUsedRingCompletionReader
{
    public const bool PerformsMmio = false;
    public const bool PerformsInterruptPolling = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool NotifiesDevice = false;
    public const bool PerformsDiskRead = false;

    private const uint UsedRingIndexOffset = 2;
    private const uint UsedRingElementIdOffset = 0;
    private const uint UsedRingElementLengthOffset = 4;

    public static VirtioBlockUsedRingCompletionReaderResult Read(
        ushort queueSize,
        ushort previousUsedIndex,
        ushort expectedDescriptorHead,
        uint expectedDataByteCount,
        ReadOnlySpan<byte> statusBuffer,
        ReadOnlySpan<byte> usedRing)
    {
        VirtqueueSplitRingPlanStatus queueStatus =
            VirtqueueSplitRingPlan.ValidateQueueSize(queueSize);
        if (queueStatus != VirtqueueSplitRingPlanStatus.Success)
        {
            return VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.InvalidQueueSize,
                queueStatus,
                previousUsedIndex);
        }

        if ((uint)statusBuffer.Length < VirtioBlockRequestPlan.StatusLength)
        {
            return VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.StatusBufferTooSmall,
                queueStatus,
                previousUsedIndex);
        }

        if (!TryGetRequiredUsedRingLength(queueSize, out uint requiredUsedRingLength))
        {
            return VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.ArithmeticOverflow,
                queueStatus,
                previousUsedIndex);
        }

        if ((uint)usedRing.Length < requiredUsedRingLength)
        {
            return VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.UsedRingTooSmall,
                queueStatus,
                previousUsedIndex);
        }

        ushort currentUsedIndex = EndianPrimitives.ReadUInt16LittleEndian(
            usedRing.Slice((int)UsedRingIndexOffset, sizeof(ushort)));
        ushort expectedUsedIndex = unchecked((ushort)(previousUsedIndex + 1));
        if (currentUsedIndex == previousUsedIndex)
        {
            return VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.NoCompletion,
                queueStatus,
                previousUsedIndex,
                currentUsedIndex,
                usedRingSlot: 0,
                usedRingElementOffset: 0,
                descriptorHead: 0,
                usedByteCount: 0,
                statusByte: statusBuffer[0]);
        }

        if (currentUsedIndex != expectedUsedIndex)
        {
            return VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.UnexpectedUsedIndex,
                queueStatus,
                previousUsedIndex,
                currentUsedIndex,
                usedRingSlot: 0,
                usedRingElementOffset: 0,
                descriptorHead: 0,
                usedByteCount: 0,
                statusByte: statusBuffer[0]);
        }

        ushort usedRingSlot = (ushort)(previousUsedIndex & (queueSize - 1));
        if (!TryGetUsedRingElementOffset(usedRingSlot, out uint usedRingElementOffset) ||
            !ValidateRange(usedRingElementOffset, VirtqueueSplitRingPlan.UsedRingElementLength, usedRing.Length))
        {
            return VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.ArithmeticOverflow,
                queueStatus,
                previousUsedIndex,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset,
                descriptorHead: 0,
                usedByteCount: 0,
                statusByte: statusBuffer[0]);
        }

        ReadOnlySpan<byte> usedElement = usedRing.Slice(
            (int)usedRingElementOffset,
            (int)VirtqueueSplitRingPlan.UsedRingElementLength);
        uint descriptorHead = EndianPrimitives.ReadUInt32LittleEndian(
            usedElement.Slice((int)UsedRingElementIdOffset, sizeof(uint)));
        uint usedByteCount = EndianPrimitives.ReadUInt32LittleEndian(
            usedElement.Slice((int)UsedRingElementLengthOffset, sizeof(uint)));
        byte statusByte = statusBuffer[0];

        if (descriptorHead != expectedDescriptorHead)
        {
            return VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.UnexpectedDescriptorHead,
                queueStatus,
                previousUsedIndex,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset,
                descriptorHead,
                usedByteCount,
                statusByte);
        }

        if (usedByteCount != expectedDataByteCount)
        {
            return VirtioBlockUsedRingCompletionReaderResult.Fail(
                VirtioBlockUsedRingCompletionReaderStatus.UnexpectedByteCount,
                queueStatus,
                previousUsedIndex,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset,
                descriptorHead,
                usedByteCount,
                statusByte);
        }

        return statusByte switch
        {
            (byte)VirtioBlockDeviceStatus.Ok =>
                VirtioBlockUsedRingCompletionReaderResult.Success(
                    previousUsedIndex,
                    currentUsedIndex,
                    usedRingSlot,
                    usedRingElementOffset,
                    descriptorHead,
                    usedByteCount,
                    statusByte),
            (byte)VirtioBlockDeviceStatus.IoError =>
                VirtioBlockUsedRingCompletionReaderResult.Fail(
                    VirtioBlockUsedRingCompletionReaderStatus.DeviceIoError,
                    queueStatus,
                    previousUsedIndex,
                    currentUsedIndex,
                    usedRingSlot,
                    usedRingElementOffset,
                    descriptorHead,
                    usedByteCount,
                    statusByte),
            (byte)VirtioBlockDeviceStatus.Unsupported =>
                VirtioBlockUsedRingCompletionReaderResult.Fail(
                    VirtioBlockUsedRingCompletionReaderStatus.DeviceUnsupported,
                    queueStatus,
                    previousUsedIndex,
                    currentUsedIndex,
                    usedRingSlot,
                    usedRingElementOffset,
                    descriptorHead,
                    usedByteCount,
                    statusByte),
            _ =>
                VirtioBlockUsedRingCompletionReaderResult.Fail(
                    VirtioBlockUsedRingCompletionReaderStatus.UnknownStatusByte,
                    queueStatus,
                    previousUsedIndex,
                    currentUsedIndex,
                    usedRingSlot,
                    usedRingElementOffset,
                    descriptorHead,
                    usedByteCount,
                    statusByte)
        };
    }

    private static bool TryGetRequiredUsedRingLength(
        ushort queueSize,
        out uint length)
    {
        ulong value = (ulong)VirtqueueSplitRingPlan.UsedRingHeaderLength +
            ((ulong)queueSize * VirtqueueSplitRingPlan.UsedRingElementLength);
        if (value > uint.MaxValue)
        {
            length = 0;
            return false;
        }

        length = (uint)value;
        return true;
    }

    private static bool TryGetUsedRingElementOffset(
        ushort slot,
        out uint offset)
    {
        ulong value = (ulong)VirtqueueSplitRingPlan.UsedRingHeaderLength +
            ((ulong)slot * VirtqueueSplitRingPlan.UsedRingElementLength);
        if (value > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)value;
        return true;
    }

    private static bool ValidateRange(
        uint offset,
        uint length,
        int sourceLength)
    {
        ulong end = (ulong)offset + length;
        return end <= int.MaxValue && end <= (ulong)sourceLength;
    }
}
