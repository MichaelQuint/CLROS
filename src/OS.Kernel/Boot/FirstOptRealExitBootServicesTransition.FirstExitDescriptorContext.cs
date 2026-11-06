using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;

public unsafe struct FirstExitDescriptorContext
{
    public fixed byte Gdtr[16], BridgeIdtr[16], FinalIdtr[16];
    public fixed byte ObservedGdtr[16], ObservedIdtr[16];
    public ushort ObservedTr, FirmwareCs, ObservedCs, ObservedDs, ObservedEs, ObservedSs;
    public uint Prepared;
    public byte* Gdt;
    public byte* Tss;
    public byte* BridgeIdt;
    public byte* FinalIdt;
}
