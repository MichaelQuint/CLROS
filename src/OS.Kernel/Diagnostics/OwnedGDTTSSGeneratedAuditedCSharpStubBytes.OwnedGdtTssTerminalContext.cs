namespace OS.Kernel.Diagnostics;
/// <summary>
/// Exact caller-owned RCX context consumed by the terminal M02-009 byte plan.
/// All fields are inline bytes. The first and second ten-byte fields are packed
/// x64 GDTR images; the processor writes the second image and the TR selector.
/// </summary>

public unsafe struct OwnedGdtTssTerminalContext
{
    public fixed byte LoadGdtr[10];
    public fixed byte ReadBackGdtr[10];
    public ushort ReadBackTrSelector;
    public ushort Reserved;
    public fixed byte Gdt[40];
    public fixed byte Tss[104];
}
#if !NOSTDLIB_EFI
#endif
