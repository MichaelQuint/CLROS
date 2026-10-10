namespace OS.Std.Memory;

public enum MemoryOrderStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidCallback = 2,
    Failed = 3
}

/// <summary>
/// Invokes one adapter-supplied barrier. The adapter authenticates a real platform
/// barrier and the callback's stable lifetime and non-throwing contract. Success
/// reflects its return value; callback failure does not prove that no effects occurred.
/// </summary>
public static unsafe class MemoryOrder
{
    public static MemoryOrderStatus Fence(void* context, delegate* unmanaged<void*, byte> fence)
    {
        if (fence == null) return MemoryOrderStatus.InvalidCallback;
        return fence(context) == 1 ? MemoryOrderStatus.Success : MemoryOrderStatus.Failed;
    }
}
