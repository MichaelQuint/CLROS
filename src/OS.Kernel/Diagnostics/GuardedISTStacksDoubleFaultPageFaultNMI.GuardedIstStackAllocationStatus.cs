namespace OS.Kernel.Diagnostics;
public enum GuardedIstStackAllocationStatus : byte
{
    Success = 0,
    InvalidAllocatorState = 1,
    AlreadyAllocated = 2,
    GenerationExhausted = 3
}
