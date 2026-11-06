using OS.Kernel.Platform;

namespace OS.Drivers;
/// <summary>Opaque identity for one allocation; copying a token does not transfer ownership.</summary>

public readonly struct DmaBufferToken
{
    internal readonly ulong Pool, Epoch, Generation;
    internal readonly uint Slot;
    public bool IsInitialized => Pool != 0 && Epoch != 0 && Generation != 0;

    internal DmaBufferToken(ulong pool, ulong epoch, ulong generation, uint slot)
    {
        Pool = pool;
        Epoch = epoch;
        Generation = generation;
        Slot = slot;
    }
}
