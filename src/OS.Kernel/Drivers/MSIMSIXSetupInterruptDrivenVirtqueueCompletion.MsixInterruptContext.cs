using OS.Std.Binary;

namespace OS.Drivers;
/// <summary>Owned single-BSP wait storage. The native ABI fixes every field offset.
/// IDTR buffers contain the architectural 10 bytes followed by six zero bytes.</summary>

public unsafe struct MsixInterruptContext
{
    public ulong EoiAddress, Count, InterruptedRip, InterruptedFlags, EntryFlags, ExitFlags, Iterations;
    public fixed byte OriginalIdtr[16];
    public fixed byte TemporaryIdtr[16];
    public fixed byte RestoredIdtr[16];
    public ulong SpuriousCount, BeforeEoiIsr, AfterEoiIsr;
}
