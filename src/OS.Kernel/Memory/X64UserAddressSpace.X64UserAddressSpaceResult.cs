using OS.Boot;

namespace OS.Kernel.Memory;

public struct X64UserAddressSpaceResult
{
    public X64UserAddressSpaceStatus Status;
    public ulong Root, LeafPages;
    public uint UsedTables;
    public readonly bool Succeeded => Status == X64UserAddressSpaceStatus.Ready;
}
#if M10_004_PROBE

#elif M16_025_PROBE

#else
#endif
