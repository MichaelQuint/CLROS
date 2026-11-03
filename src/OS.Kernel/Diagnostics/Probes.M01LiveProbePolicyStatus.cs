namespace OS.Kernel.Diagnostics;
#if !NOSTDLIB_EFI
public enum M01LiveProbePolicyStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    LiveAllocatorRequiresLoadedImageStack = 2,
    OwnedPagingRequiresLiveAllocator = 3
}
#endif
