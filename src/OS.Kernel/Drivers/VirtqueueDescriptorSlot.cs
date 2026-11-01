namespace OS.Drivers;
public struct VirtqueueDescriptorSlot
{
    internal ulong Pool, Generation;
    internal VirtqueueDescriptorOwnership Owner;
}
