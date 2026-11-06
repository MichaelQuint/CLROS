using OS.Kernel.Platform;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Std.Hash;

#if M16_004_PROBE || M16_005_PROBE
using OS.Kernel.Compatibility.LinuxX64;
#endif
namespace OS.Boot;
/// <summary>
/// Prepare a bridge from firmware descriptors to owned descriptors without a
/// selector/IST mismatch window. The initial owned IDT uses the still-valid
/// firmware CS and IST=0; the owned GDT retains an equivalent code alias there.
/// After loading owned TR, the stub can install the final IDT's owned CS/ISTs.
/// All handlers are terminal, owned and mapped under both roots. During the
/// bridge their complete stack requirement must fit the current owned stack.
/// </summary>

public static unsafe class FirstExitDescriptors
{
    public const uint ContextBytes = 128;
    private const ulong LowEnd = 0x0000800000000000;
    // Inputs are a fresh M02-shaped GDT/TSS and final IDT, already materialized
    // in exclusively owned buffers. Prepare changes only the GDT tail, bridge
    // IDT and descriptor context, after validating every input. It allocates no
    // storage and cannot establish page mappings or authenticate handler code.
    public static bool Prepare(ref FirstExitDescriptorContext context, byte* gdt, ulong gdtBytes, byte* tss, ulong tssBytes, byte* finalIdt, ulong finalIdtBytes, byte* bridgeIdt, ulong bridgeIdtBytes, ushort firmwareCs)
    {
        if (context.Prepared != 0 || sizeof(FirstExitDescriptorContext) != ContextBytes || gdtBytes != 4096 || tssBytes != 104 || finalIdtBytes != 4096 || bridgeIdtBytes != 4096 || (firmwareCs & 7) != 0 || (firmwareCs != 8 && firmwareCs < 40) || firmwareCs > 4088 || !Storage(gdt, 4096, 16) || !Storage(tss, 104, 16) || !Storage(finalIdt, 4096, 4096) || !Storage(bridgeIdt, 4096, 4096))
            return false;
        fixed (FirstExitDescriptorContext* address = &context)
        {
            if (!Storage((byte*)address, ContextBytes, 8))
                return false;
            ulong* starts = stackalloc ulong[5];
            starts[0] = (ulong)address;
            starts[1] = (ulong)gdt;
            starts[2] = (ulong)tss;
            starts[3] = (ulong)finalIdt;
            starts[4] = (ulong)bridgeIdt;
            for (int i = 0; i < 5; i++)
                for (int j = 0; j < i; j++)
                    if (Overlaps(starts[i], Size(i), starts[j], Size(j)))
                        return false;
        }

        ulong tssBase = (ulong)tss;
        ulong tssDescriptor = 0x0000890000000067UL | ((tssBase & 0xffffff) << 16) | (((tssBase >> 24) & 0xff) << 56);
        if (*(ulong*)gdt != 0 || *(ulong*)(gdt + 8) != 0x00af9a000000ffffUL || *(ulong*)(gdt + 16) != 0x00cf92000000ffffUL || *(ulong*)(gdt + 24) != tssDescriptor || *(ulong*)(gdt + 32) != tssBase >> 32 || !StackTop(*(ulong*)(tss + 4)) || *(ushort*)(tss + 102) != 104)
            return false;
        ulong ist1 = *(ulong*)(tss + 36), ist2 = *(ulong*)(tss + 44), ist3 = *(ulong*)(tss + 52);
        if (!StackTop(ist1) || !StackTop(ist2) || !StackTop(ist3) || ist1 == ist2 || ist1 == ist3 || ist2 == ist3)
            return false;
        for (int i = 0; i < 104; i++)
            if (!(i >= 4 && i < 12) && !(i >= 36 && i < 60) && i < 102 && tss[i] != 0)
                return false;
        for (int i = 40; i < 4096; i++)
            if (gdt[i] != 0)
                return false;
        for (int i = 0; i < 4096; i++)
            if (bridgeIdt[i] != 0)
                return false;
        for (int vector = 0; vector < 256; vector++)
        {
            byte* gate = finalIdt + vector * 16;
            ulong handler = *(ushort*)gate | ((ulong)*(ushort*)(gate + 6) << 16) | ((ulong)*(uint*)(gate + 8) << 32);
            byte ist = vector == 2 ? (byte)3 : vector == 8 ? (byte)1 : vector == 14 ? (byte)2 : (byte)0;
            if (handler == 0 || handler >= LowEnd || *(ushort*)(gate + 2) != 8 || gate[4] != ist || gate[5] != 0x8e || *(uint*)(gate + 12) != 0)
                return false;
        }

        // No mutation above this point. The alias preserves the meaning of the
        // bridge's CS across LGDT; final gates use the canonical owned selector 8.
        if (firmwareCs >= 40)
            *(ulong*)(gdt + firmwareCs) = *(ulong*)(gdt + 8);
        for (int i = 0; i < 4096; i++)
            bridgeIdt[i] = finalIdt[i];
        for (int vector = 0; vector < 256; vector++)
        {
            *(ushort*)(bridgeIdt + vector * 16 + 2) = firmwareCs;
            bridgeIdt[vector * 16 + 4] = 0;
        }

        context = default;
        fixed (FirstExitDescriptorContext* address = &context)
        {
            *(ushort*)address->Gdtr = (ushort)(firmwareCs < 40 ? 39 : firmwareCs + 7);
            *(ulong*)(address->Gdtr + 2) = (ulong)gdt;
            *(ushort*)address->BridgeIdtr = 4095;
            *(ulong*)(address->BridgeIdtr + 2) = (ulong)bridgeIdt;
            *(ushort*)address->FinalIdtr = 4095;
            *(ulong*)(address->FinalIdtr + 2) = (ulong)finalIdt;
        }

        context.FirmwareCs = firmwareCs;
        context.Gdt = gdt;
        context.Tss = tss;
        context.BridgeIdt = bridgeIdt;
        context.FinalIdt = finalIdt;
        context.Prepared = 1;
        return true;
    }

    internal static bool Overlaps(ulong a, ulong bytes, ulong b, ulong otherBytes) => a < b + otherBytes && b < a + bytes;
    private static ulong Size(int i) => i == 0 ? ContextBytes : i == 2 ? 104UL : 4096UL;
    private static bool Storage(byte* p, ulong bytes, ulong alignment) => (ulong)p != 0 && ((ulong)p & (alignment - 1)) == 0 && (ulong)p < LowEnd - bytes;
    private static bool StackTop(ulong value) => value >= 4096 && value < LowEnd && (value & 15) == 0;
}
