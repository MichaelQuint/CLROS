namespace OS.Drivers;

public unsafe struct VirtqueueDescriptorPoolState
{
    internal ulong Self, Epoch;
    internal VirtqueueDescriptorSlot* Slots;
    internal ushort Count, Allocated;
    public readonly bool IsInitialized => Self != 0;
    public readonly ushort Capacity => Count;
    public readonly ushort AllocatedCount => Allocated;
}
