namespace OS.Kernel.Diagnostics;
/// <summary>
/// Exact caller-owned RCX context used by the M02-012 reversible IDTR stub.
/// The four ten-byte buffers are packed x64 IDTR images. Reserved bytes keep
/// every image on a 16-byte boundary and must remain zero.
/// </summary>

public unsafe struct IDTRContinuedExecutionContext
{
    public fixed byte RequestedIdtr[10];
    public fixed byte Reserved0[6];
    public fixed byte PreviousIdtr[10];
    public fixed byte Reserved1[6];
    public fixed byte LoadedIdtr[10];
    public fixed byte Reserved2[6];
    public fixed byte RestoredIdtr[10];
    public fixed byte Reserved3[6];
    public ulong RflagsBefore;
    public ulong RflagsAfter;
}
#if !NOSTDLIB_EFI
#endif
