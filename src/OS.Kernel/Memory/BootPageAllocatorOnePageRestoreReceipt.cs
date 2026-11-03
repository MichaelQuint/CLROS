namespace OS.Kernel.Memory;

public readonly struct BootPageAllocatorOnePageRestoreReceipt
{
    private readonly byte _isInitialized;
    private BootPageAllocatorOnePageRestoreReceipt(BootPageFreeListNode sourceNodeBefore, int previousNodeIndexBefore, BootPageFreeListNode previousNodeBefore)
    {
        _isInitialized = 1;
        SourceNodeBefore = sourceNodeBefore;
        PreviousNodeIndexBefore = previousNodeIndexBefore;
        PreviousNodeBefore = previousNodeBefore;
    }

    public bool IsInitialized => _isInitialized == 1;
    public BootPageFreeListNode SourceNodeBefore
    {
        get;
    }
    public int PreviousNodeIndexBefore
    {
        get;
    }
    public BootPageFreeListNode PreviousNodeBefore
    {
        get;
    }

    internal static BootPageAllocatorOnePageRestoreReceipt Create(BootPageFreeListNode sourceNodeBefore, int previousNodeIndexBefore, BootPageFreeListNode previousNodeBefore)
    {
        return new BootPageAllocatorOnePageRestoreReceipt(sourceNodeBefore, previousNodeIndexBefore, previousNodeBefore);
    }
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
