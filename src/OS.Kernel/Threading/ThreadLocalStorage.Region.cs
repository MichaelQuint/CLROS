using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct ThreadLocalStorage
{
    /// <summary>Caller-admitted mapped span and an eight-byte-aligned base within
    /// it. All-zero means no TLS for that segment. A declaration grants no mapping
    /// authority. Extents include any negative-offset data needed by the consumer.</summary>

    public readonly struct Region
    {
        public readonly ulong Start, Bytes, Base;
        public Region(ulong start, ulong bytes, ulong @base)
        {
            Start = start;
            Bytes = bytes;
            Base = @base;
        }
    }
}
