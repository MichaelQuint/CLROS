// Narrow pointer-backed Span surface. Host mirror exercises the same storage
// operations without replacing the platform BCL. See the compatibility ledger.
#if NOSTDLIB_EFI
namespace System;
#else
namespace OS.Std.NoRuntime;
#endif

public readonly unsafe ref struct Span<T> where T : unmanaged
{
    private readonly ref T _reference;
    private readonly int _length;

    public Span(void* pointer, int length)
    {
        if (length < 0)
        {
#if NOSTDLIB_EFI
            while (true) System.RuntimeFailFast.Halt();
#else
            throw new System.ArgumentOutOfRangeException(nameof(length));
#endif
        }
        _reference = ref *(T*)pointer;
        _length = length;
    }

    public int Length => _length;

    public ref T GetPinnableReference() => ref (_length == 0 ? ref *(T*)null : ref _reference);
}
