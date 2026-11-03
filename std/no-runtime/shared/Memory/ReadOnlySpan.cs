// Narrow pointer-backed Span surface. Host mirror exercises the same storage
// operations without replacing the platform BCL. See the compatibility ledger.
#if NOSTDLIB_EFI
namespace System;
#else
namespace OS.Std.NoRuntime;
#endif
/// <summary>Pointer-backed unmanaged read-only view; the adapter owns mapping
/// and lifetime. This subset supports length, indexing, slicing and pinning.</summary>

public readonly unsafe ref struct ReadOnlySpan<T>
    where T : unmanaged
{
    private readonly T* _pointer;
    private readonly int _length;
    /// <summary>Empty borrowed view with no backing storage or lifetime.</summary>

    public static ReadOnlySpan<T> Empty => default;

    public ReadOnlySpan(void* pointer, int length)
    {
        if (length < 0)
            Bounds();
        _pointer = (T*)pointer;
        _length = length;
    }

    public int Length => _length;

    public ref readonly T GetPinnableReference() => ref *(_length == 0 ? (T*)null : _pointer);
    public ref readonly T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_length)
                Bounds();
            return ref _pointer[index];
        }
    }

    public ReadOnlySpan<T> Slice(int start) => Slice(start, _length - start);
    public ReadOnlySpan<T> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
            Bounds();
        return new ReadOnlySpan<T>(_pointer + start, length);
    }

    private static void Bounds()
    {
#if NOSTDLIB_EFI
        while (true)
            System.RuntimeFailFast.Halt();
#else
        throw new System.ArgumentOutOfRangeException();
#endif
    }
}
