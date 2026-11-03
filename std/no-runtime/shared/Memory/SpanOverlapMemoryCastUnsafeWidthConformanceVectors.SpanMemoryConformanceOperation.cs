namespace OS.Std.Memory;

public enum SpanMemoryConformanceOperation : byte
{
    Uninitialized = 0,
    Slice = 1,
    Overlap = 2,
    MemoryCast = 3,
    UnalignedWidth = 4
}
#if !NOSTDLIB_EFI
#else

#endif
