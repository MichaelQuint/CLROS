using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

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

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockUsedRingCompletionReaderStatus Status
    {
        get;
    }

    public VirtqueueSplitRingPlanStatus QueueStatus
    {
        get;
    }

    public ushort PreviousUsedIndex
    {
        get;
    }

    public ushort CurrentUsedIndex
    {
        get;
    }

    public ushort NextUsedIndex
    {
        get;
    }

    public ushort UsedRingSlot
    {
        get;
    }

    public uint UsedRingElementOffset
    {
        get;
    }

    public uint DescriptorHead
    {
        get;
    }

    public uint UsedByteCount
    {
        get;
    }

    public byte StatusByte
    {
        get;
    }

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
