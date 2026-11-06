using OS.Drivers;

namespace OS.Storage;

public struct VirtioBlockQueueCursorState
{
    public VirtioBlockQueueCursorState(
        ushort availableRingIndex,
        ushort usedRingIndex)
    {
        AvailableRingIndex = availableRingIndex;
        UsedRingIndex = usedRingIndex;
    }

    public ushort AvailableRingIndex
    {
        get; set;
    }

    public ushort UsedRingIndex
    {
        get; set;
    }
}
