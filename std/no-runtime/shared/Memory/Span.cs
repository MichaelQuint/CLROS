// Narrow pointer-backed Span surface. Host mirror exercises the same storage
// operations without replacing the platform BCL. See the compatibility ledger.
#if NOSTDLIB_EFI
namespace System;
#else
namespace OS.Std.NoRuntime;
#endif
public readonly unsafe ref struct Span<T>
    where T : unmanaged
{
    private readonly ref T _reference;
    private readonly int _length;
    /// <summary>Empty borrowed view with no backing storage or lifetime.</summary>

    public static Span<T> Empty => default;

    public Span(void* pointer, int length)
    {
        if (length < 0)
        {
#if NOSTDLIB_EFI
            while (true)
                System.RuntimeFailFast.Halt();
#else
            throw new System.ArgumentOutOfRangeException(nameof(length));
#endif
        }

        _reference = ref *(T*)pointer;
        _length = length;
    }

    public int Length => _length;

    public ref T GetPinnableReference() => ref (_length == 0 ? ref *(T*)null : ref _reference);
    public ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_length)
                Bounds();
            fixed (T* pointer = &_reference)
                return ref pointer[index];
        }
    }

    public Span<T> Slice(int start) => Slice(start, _length - start);
    public Span<T> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
            Bounds();
        fixed (T* pointer = &_reference)
            return new Span<T>(pointer + start, length);
    }

    public void Clear()
    {
        for (int index = 0; index < _length; index++)
            this[index] = default;
    }

    public static implicit operator ReadOnlySpan<T>(Span<T> source)
    {
        fixed (T* pointer = source)
            return new ReadOnlySpan<T>(pointer, source.Length);
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
#if NOSTDLIB_EFI

#else
#endif
