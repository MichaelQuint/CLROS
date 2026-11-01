namespace OS.Drivers;
/// <summary>Opaque allocation identity. Copying a token does not transfer ownership.</summary>

public readonly struct VirtqueueDescriptorToken
{
    internal readonly ulong Pool, Epoch, Generation;
    public readonly ushort Index;
    public bool IsInitialized => Pool != 0 && Epoch != 0 && Generation != 0;

    internal VirtqueueDescriptorToken(ulong pool, ulong epoch, ulong generation, ushort index)
    {
        Pool = pool;
        Epoch = epoch;
        Generation = generation;
        Index = index;
    }
}
