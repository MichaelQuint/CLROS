namespace OS.Std.Memory;

public enum MemoryOrderStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidCallback = 2,
    Failed = 3
}
