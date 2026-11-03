namespace OS.Kernel.Memory;

public enum BootPageAllocatorOnePageCoalescingKind : byte
{
    Uninitialized = 0,
    None = 1,
    Previous = 2,
    Next = 3,
    PreviousAndNext = 4
}
