using OS.Kernel.Platform;
using OS.Std.Hash;

namespace OS.Boot;

public enum FirstExitBindingStatus
{
    NotStarted, Ready, Consumed, AlreadyBound, NotReady, FirmwareUnavailable, InvalidStorage,
    InvalidMap, InvalidTable, WrongBootServicesTable, InvalidFunction, NotFirmwareCode,
    Changed
}

public unsafe struct FirstExitFirmwareBindingState
{
    internal uint Phase;
    internal byte* SystemTable;
    internal byte* BootServices;
    internal ulong SystemBytes, BootBytes, MapFunction, ExitFunction;
    internal uint SystemCrc, BootCrc;
    internal void* ImageHandle;
    public bool IsReady => Phase == 1;
    public bool IsConsumed => Phase == 2;
    public bool RetainedPointersCleared => SystemTable == null && BootServices == null &&
        MapFunction == 0 && ExitFunction == 0 && ImageHandle == null;
}

/// <summary>
/// Bind the two entries from the original x64 EFI tables, never caller-supplied
/// replacement callbacks. The adapter supplies the actual EfiMain table/handle,
/// valid readable extents and an owned firmware-map snapshot. Table CRCs detect
/// corruption, not malicious firmware or forged provenance. Memory-map code
/// classification is not a page-table execute-permission check. Actual firmware
/// CR3/mapping authority remains the adapter's obligation before the final call.
/// No firmware function is called here. Consume rechecks table identity/content
/// before the final window and clears this binding before exposing its entries.
/// Retain one original state exclusively; do not copy/reset it or alias outputs.
/// </summary>
public static unsafe class FirstExitFirmwareBinding
{
    private const ulong LowEnd = 0x0000800000000000;
    private const ulong SystemSignature = 0x5453595320494249;
    private const ulong BootSignature = 0x56524553544f4f42;

    public static FirstExitBindingStatus Bind(ref FirstExitFirmwareBindingState state,
        FirmwareServicesMode mode, void* imageHandle, byte* systemTable, ulong systemBytes,
        byte* bootServices, ulong bootBytes, FirmwareMemoryMapRecord ownedMap)
    {
        if (state.Phase != 0) return FirstExitBindingStatus.AlreadyBound;
        if (mode != FirmwareServicesMode.BootServicesAvailable) return FirstExitBindingStatus.FirmwareUnavailable;
        if (imageHandle == null || !Extent((ulong)systemTable, systemBytes) || systemBytes < 120 || systemBytes > 4096 ||
            !Extent((ulong)bootServices, bootBytes) || bootBytes < 240 || bootBytes > 4096 ||
            ((ulong)systemTable & 7) != 0 || ((ulong)bootServices & 7) != 0 ||
            FirstExitDescriptors.Overlaps((ulong)systemTable, systemBytes, (ulong)bootServices, bootBytes))
            return FirstExitBindingStatus.InvalidStorage;
        if (!ownedMap.HasOwnedCopy() || ownedMap.DescriptorVersion != 1 ||
            !UefiMemoryMapCopyPlanResult.Create(ownedMap, ownedMap.MapSize).Succeeded ||
            !Extent(ownedMap.BufferAddress, ownedMap.MapSize)) return FirstExitBindingStatus.InvalidMap;
        fixed (FirstExitFirmwareBindingState* address = &state)
        {
            ulong start = (ulong)address, bytes = (ulong)sizeof(FirstExitFirmwareBindingState);
            if (!Extent(start, bytes) || FirstExitDescriptors.Overlaps(start, bytes, (ulong)systemTable, systemBytes) ||
                FirstExitDescriptors.Overlaps(start, bytes, (ulong)bootServices, bootBytes) ||
                FirstExitDescriptors.Overlaps(start, bytes, ownedMap.BufferAddress, ownedMap.MapSize) ||
                FirstExitDescriptors.Overlaps(ownedMap.BufferAddress, ownedMap.MapSize, (ulong)systemTable, systemBytes) ||
                FirstExitDescriptors.Overlaps(ownedMap.BufferAddress, ownedMap.MapSize, (ulong)bootServices, bootBytes))
                return FirstExitBindingStatus.InvalidStorage;
        }
        if (!Header(systemTable, systemBytes, 120, SystemSignature) ||
            !Header(bootServices, bootBytes, 240, BootSignature)) return FirstExitBindingStatus.InvalidTable;
        if (*(ulong*)(systemTable + 96) != (ulong)bootServices) return FirstExitBindingStatus.WrongBootServicesTable;
        ulong map = *(ulong*)(bootServices + 56), exit = *(ulong*)(bootServices + 232);
        if (!Extent(map, 1) || !Extent(exit, 1) || map == exit) return FirstExitBindingStatus.InvalidFunction;

        uint mapMatches = 0, exitMatches = 0;
        bool mapCode = false, exitCode = false;
        for (ulong i = 0; i < ownedMap.DescriptorCount; i++)
        {
            byte* descriptor = (byte*)(ownedMap.BufferAddress + i * ownedMap.DescriptorSize);
            ulong start = *(ulong*)(descriptor + 8), pages = *(ulong*)(descriptor + 24);
            if ((start & 4095) != 0 || pages == 0 || pages > (UefiMemoryMapCopyPlan.MaximumUnsignedLong - start) / 4096)
                return FirstExitBindingStatus.InvalidMap;
            ulong end = start + pages * 4096;
            uint type = *(uint*)descriptor;
            bool code = type == 3 || type == 5; // EfiBootServicesCode / EfiRuntimeServicesCode
            if (start <= map && map < end) { mapMatches++; mapCode = code; }
            if (start <= exit && exit < end) { exitMatches++; exitCode = code; }
        }
        if (mapMatches != 1 || exitMatches != 1 || !mapCode || !exitCode) return FirstExitBindingStatus.NotFirmwareCode;
        state.SystemTable = systemTable; state.SystemBytes = systemBytes;
        state.BootServices = bootServices; state.BootBytes = bootBytes;
        state.SystemCrc = *(uint*)(systemTable + 16); state.BootCrc = *(uint*)(bootServices + 16);
        state.MapFunction = map; state.ExitFunction = exit; state.ImageHandle = imageHandle;
        state.Phase = 1;
        return FirstExitBindingStatus.Ready;
    }

    public static FirstExitBindingStatus Consume(ref FirstExitFirmwareBindingState state,
        FirmwareServicesMode mode,
        out delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap,
        out delegate* unmanaged<void*, ulong, ulong> exitBootServices, out void* imageHandle)
    {
        getMemoryMap = null; exitBootServices = null; imageHandle = null;
        if (!state.IsReady) return FirstExitBindingStatus.NotReady;
        // Do not dereference any firmware storage when services are unavailable.
        if (mode != FirmwareServicesMode.BootServicesAvailable)
        {
            state = default;
            state.Phase = 2;
            return FirstExitBindingStatus.FirmwareUnavailable;
        }
        bool unchanged = Header(state.SystemTable, state.SystemBytes, 120, SystemSignature) &&
            Header(state.BootServices, state.BootBytes, 240, BootSignature) &&
            *(uint*)(state.SystemTable + 16) == state.SystemCrc && *(uint*)(state.BootServices + 16) == state.BootCrc &&
            *(ulong*)(state.SystemTable + 96) == (ulong)state.BootServices &&
            *(ulong*)(state.BootServices + 56) == state.MapFunction && *(ulong*)(state.BootServices + 232) == state.ExitFunction;
        if (unchanged)
        {
            getMemoryMap = (delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong>)state.MapFunction;
            exitBootServices = (delegate* unmanaged<void*, ulong, ulong>)state.ExitFunction;
            imageHandle = state.ImageHandle;
        }
        state = default;
        state.Phase = 2;
        return unchanged ? FirstExitBindingStatus.Consumed : FirstExitBindingStatus.Changed;
    }

    private static bool Header(byte* table, ulong capacity, uint minimum, ulong signature)
    {
        uint size = *(uint*)(table + 12);
        if (*(ulong*)table != signature || *(uint*)(table + 8) < 0x20000 ||
            size < minimum || size > capacity || *(uint*)(table + 20) != 0) return false;
        uint crc = Crc32.InitialState;
        for (uint i = 0; i < size; i++) crc = Crc32.UpdateByte(crc, i >= 16 && i < 20 ? (byte)0 : table[i]);
        return Crc32.Finish(crc) == *(uint*)(table + 16);
    }

    private static bool Extent(ulong start, ulong bytes)
        => start != 0 && start < LowEnd && bytes != 0 && bytes < LowEnd - start;
}

public enum FirstExitPhase : uint
{
    Empty, InFlight, Failed, OwnedContinuation, UnexpectedContinuationReturn
}

public enum FirstExitAdoptionPhase : uint
{
    Empty, Prepared, ExitStateCheck, ProcessorCheck, StackCheck, CanaryBeforeCheck,
    Cr3ReadBackCheck, Cr4ReadBackCheck, CanaryAfterCheck, OwnedDiagnostic,
    UnexpectedDiagnosticReturn, DescriptorInstall, DescriptorReadBackCheck
}

// Fixed x64 layout consumed by FirstExitPermanentCr3Stub. Retain the original
// context exclusively; do not change inputs after preparation or reset Phase.
public unsafe struct FirstExitPermanentCr3Context
{
    public ulong FirmwareCr3, OwnedCr3, FirmwareCr4;
    public ulong StackBottom, StackTop, MinimumRsp;
    public ulong LowerCanary, UpperCanary;
    public delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void> Diagnostic;
    public void* DiagnosticContext;
    public ulong ObservedCr3, ObservedCr4;
    public FirstExitAdoptionPhase Phase;
    internal uint Reserved;
    public ulong ObservedRsp;
    public FirstExitDescriptorContext* Descriptors;
}

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
    public static bool Prepare(ref FirstExitDescriptorContext context, byte* gdt, ulong gdtBytes,
        byte* tss, ulong tssBytes, byte* finalIdt, ulong finalIdtBytes,
        byte* bridgeIdt, ulong bridgeIdtBytes, ushort firmwareCs)
    {
        if (context.Prepared != 0 || sizeof(FirstExitDescriptorContext) != ContextBytes ||
            gdtBytes != 4096 || tssBytes != 104 || finalIdtBytes != 4096 || bridgeIdtBytes != 4096 ||
            (firmwareCs & 7) != 0 || (firmwareCs != 8 && firmwareCs < 40) || firmwareCs > 4088 ||
            !Storage(gdt, 4096, 16) || !Storage(tss, 104, 16) ||
            !Storage(finalIdt, 4096, 4096) || !Storage(bridgeIdt, 4096, 4096)) return false;

        fixed (FirstExitDescriptorContext* address = &context)
        {
            if (!Storage((byte*)address, ContextBytes, 8)) return false;
            ulong* starts = stackalloc ulong[5];
            starts[0] = (ulong)address; starts[1] = (ulong)gdt; starts[2] = (ulong)tss;
            starts[3] = (ulong)finalIdt; starts[4] = (ulong)bridgeIdt;
            for (int i = 0; i < 5; i++) for (int j = 0; j < i; j++)
                if (Overlaps(starts[i], Size(i), starts[j], Size(j))) return false;
        }

        ulong tssBase = (ulong)tss;
        ulong tssDescriptor = 0x0000890000000067UL | ((tssBase & 0xffffff) << 16) |
            (((tssBase >> 24) & 0xff) << 56);
        if (*(ulong*)gdt != 0 || *(ulong*)(gdt + 8) != 0x00af9a000000ffffUL ||
            *(ulong*)(gdt + 16) != 0x00cf92000000ffffUL ||
            *(ulong*)(gdt + 24) != tssDescriptor || *(ulong*)(gdt + 32) != tssBase >> 32 ||
            !StackTop(*(ulong*)(tss + 4)) || *(ushort*)(tss + 102) != 104) return false;
        ulong ist1 = *(ulong*)(tss + 36), ist2 = *(ulong*)(tss + 44), ist3 = *(ulong*)(tss + 52);
        if (!StackTop(ist1) || !StackTop(ist2) || !StackTop(ist3) ||
            ist1 == ist2 || ist1 == ist3 || ist2 == ist3) return false;
        for (int i = 0; i < 104; i++)
            if (!(i >= 4 && i < 12) && !(i >= 36 && i < 60) && i < 102 && tss[i] != 0) return false;
        for (int i = 40; i < 4096; i++) if (gdt[i] != 0) return false;
        for (int i = 0; i < 4096; i++) if (bridgeIdt[i] != 0) return false;
        for (int vector = 0; vector < 256; vector++)
        {
            byte* gate = finalIdt + vector * 16;
            ulong handler = *(ushort*)gate | ((ulong)*(ushort*)(gate + 6) << 16) | ((ulong)*(uint*)(gate + 8) << 32);
            byte ist = vector == 2 ? (byte)3 : vector == 8 ? (byte)1 : vector == 14 ? (byte)2 : (byte)0;
            if (handler == 0 || handler >= LowEnd || *(ushort*)(gate + 2) != 8 ||
                gate[4] != ist || gate[5] != 0x8e || *(uint*)(gate + 12) != 0) return false;
        }

        // No mutation above this point. The alias preserves the meaning of the
        // bridge's CS across LGDT; final gates use the canonical owned selector 8.
        if (firmwareCs >= 40) *(ulong*)(gdt + firmwareCs) = *(ulong*)(gdt + 8);
        for (int i = 0; i < 4096; i++) bridgeIdt[i] = finalIdt[i];
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
        context.Gdt = gdt; context.Tss = tss; context.BridgeIdt = bridgeIdt; context.FinalIdt = finalIdt;
        context.Prepared = 1;
        return true;
    }

    internal static bool Overlaps(ulong a, ulong bytes, ulong b, ulong otherBytes)
        => a < b + otherBytes && b < a + bytes;
    private static ulong Size(int i) => i == 0 ? ContextBytes : i == 2 ? 104UL : 4096UL;
    private static bool Storage(byte* p, ulong bytes, ulong alignment)
        => (ulong)p != 0 && ((ulong)p & (alignment - 1)) == 0 && (ulong)p < LowEnd - bytes;
    private static bool StackTop(ulong value) => value >= 4096 && value < LowEnd && (value & 15) == 0;
}

/// <summary>
/// Emits the terminal Win64 continuation passed to AttemptAndTransfer. It uses
/// the current, already-owned stack, checks successful exit and the exact CPU
/// state, clears PGE, loads the owned root, checks readback and both canaries,
/// then calls the owned diagnostic. Every explicit refusal and unexpected return
/// halts; there is no near RET, firmware callback, CR3 restoration or output here.
///
/// Preparation accepts only low identity-mapped addresses and roots below 4 GiB,
/// four-level/non-PCID paging and CR4 bits 0..10 with PAE set. The adapter must
/// prove all actual mappings and CPU support, authenticate Diagnostic, and have
/// owned descriptors, panic state and COM1 prepared before the final map/exit.
/// After exit, the stub loads a bridge IDT, GDT, segments, TR and final IDT, reloads
/// CS using one local far transfer and validates descriptor readback before CR3.
/// CLI does not mask NMI or exceptions. Essential mappings and terminal handler
/// authentication remain adapter obligations. The declared stack bound must cover
/// both the diagnostic and the temporary IST=0 bridge handler's reachable graph.
/// </summary>
public static unsafe class FirstExitPermanentCr3Stub
{
    public const uint ContextBytes = 120;
    // 56-byte call frame (32-byte shadow space, retained context, alignment)
    // plus the diagnostic call's eight-byte return address.
    public const ulong CallStackBytes = 64;
    private const ulong LowAddressLimit = 0x0000800000000000;

    public static bool Prepare(ref FirstExitPermanentCr3Context context,
        ulong firmwareCr3, ulong ownedCr3, ulong firmwareCr4,
        ulong stackBottom, ulong stackTop, ulong ownedStackBytes,
        ulong lowerCanary, ulong upperCanary,
        delegate* unmanaged<FirstExitTransitionState*, FirstExitPermanentCr3Context*, void> diagnostic,
        void* diagnosticContext, FirstExitDescriptorContext* descriptors)
    {
        if (context.Phase != FirstExitAdoptionPhase.Empty || sizeof(FirstExitPermanentCr3Context) != ContextBytes ||
            (firmwareCr3 & ~0x18UL) == 0 || firmwareCr3 >= 0x100000000 || (firmwareCr3 & 0xfe7) != 0 ||
            ownedCr3 == 0 || ownedCr3 >= 0x100000000 || (ownedCr3 & 0xfff) != 0 ||
            ownedCr3 == (firmwareCr3 & ~0xfffUL) ||
            (firmwareCr4 & ~0x7ffUL) != 0 || (firmwareCr4 & 0x20) == 0 ||
            stackBottom < 8 || stackTop >= LowAddressLimit - 8 || stackTop <= stackBottom ||
            (stackBottom & 7) != 0 || (stackTop & 7) != 0 ||
            ownedStackBytes == 0 || ownedStackBytes >= stackTop - stackBottom ||
            stackTop - stackBottom - ownedStackBytes <= CallStackBytes ||
            lowerCanary == 0 || upperCanary == 0 || diagnostic == null ||
            (ulong)diagnostic >= LowAddressLimit || descriptors == null ||
            (ulong)descriptors >= LowAddressLimit - FirstExitDescriptors.ContextBytes ||
            descriptors->Prepared != 1 || descriptors->Tss == null ||
            (ulong)descriptors->Tss >= LowAddressLimit - 104 || *(ulong*)(descriptors->Tss + 4) != stackTop)
            return false;

        fixed (FirstExitPermanentCr3Context* address = &context)
        {
            ulong start = (ulong)address;
            if (start == 0 || start >= LowAddressLimit - ContextBytes ||
                (start < stackTop + 8 && stackBottom - 8 < start + ContextBytes))
                return false;
            ulong* descriptorRanges = stackalloc ulong[5];
            descriptorRanges[0] = (ulong)descriptors; descriptorRanges[1] = (ulong)descriptors->Gdt;
            descriptorRanges[2] = (ulong)descriptors->Tss; descriptorRanges[3] = (ulong)descriptors->BridgeIdt;
            descriptorRanges[4] = (ulong)descriptors->FinalIdt;
            for (int i = 0; i < 5; i++)
            {
                ulong length = i == 0 ? FirstExitDescriptors.ContextBytes : i == 2 ? 104UL : 4096UL;
                if (FirstExitDescriptors.Overlaps(start, ContextBytes, descriptorRanges[i], length) ||
                    FirstExitDescriptors.Overlaps(stackBottom - 8, stackTop - stackBottom + 16, descriptorRanges[i], length))
                    return false;
            }
        }

        context = default;
        context.FirmwareCr3 = firmwareCr3;
        context.OwnedCr3 = ownedCr3;
        context.FirmwareCr4 = firmwareCr4;
        context.StackBottom = stackBottom;
        context.StackTop = stackTop;
        context.MinimumRsp = stackBottom + ownedStackBytes + CallStackBytes;
        context.LowerCanary = lowerCanary;
        context.UpperCanary = upperCanary;
        context.Diagnostic = diagnostic;
        context.DiagnosticContext = diagnosticContext;
        context.Descriptors = descriptors;
        context.Phase = FirstExitAdoptionPhase.Prepared;
        return true;
    }

    public static uint RequiredBytes => Encode(null, 0);

    public static bool Emit(byte* destination, uint capacity, out uint written)
    {
        written = 0;
        uint bytes = RequiredBytes;
        if (destination == null || capacity < bytes ||
            (ulong)destination >= LowAddressLimit - bytes)
            return false;
        written = Encode(destination, bytes - 4);
        return true;
    }

    private static uint Encode(byte* destination, uint halt)
    {
        uint p = 0;
        Op(destination, ref p, 0xfcfa, 2);              // cli; cld
        Op(destination, ref p, 0xd28548, 3);            // test rdx,rdx
        StopIf(destination, ref p, halt, 0x84);         // jz halt
        Op(destination, ref p, 0xc98548, 3);            // test rcx,rcx
        StopIf(destination, ref p, halt, 0x84);
        Op(destination, ref p, 0x01607a83, 4);          // cmp dword [rdx+96],Prepared
        StopIf(destination, ref p, halt, 0x85);
        Phase(destination, ref p, FirstExitAdoptionPhase.ExitStateCheck);
        Op(destination, ref p, 0x033983, 3);            // cmp dword [rcx],OwnedContinuation
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x02047983, 4);          // cmp dword [rcx+4],BootServicesExited
        StopIf(destination, ref p, halt, 0x85);

        Phase(destination, ref p, FirstExitAdoptionPhase.ProcessorCheck);
        Op(destination, ref p, 0xd8200f, 3);            // mov rax,cr3
        Op(destination, ref p, 0x023b48, 3);            // cmp rax,[rdx]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0xe0200f, 3);            // mov rax,cr4
        Op(destination, ref p, 0x10423b48, 4);          // cmp rax,[rdx+16]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x704a8b4c, 4);          // mov r9,[rdx+112]
        Op(destination, ref p, 0xc9854d, 3);            // test r9,r9
        StopIf(destination, ref p, halt, 0x84);
        Op(destination, ref p, 0x015c798341, 5);        // cmp dword [r9+92],1
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0xc88c66, 3);            // mov ax,cs
        Op(destination, ref p, 0x52413b4166, 5);        // cmp ax,[r9+82]
        StopIf(destination, ref p, halt, 0x85);

        Phase(destination, ref p, FirstExitAdoptionPhase.StackCheck);
        Op(destination, ref p, 0x68628948, 4);          // mov [rdx+104],rsp
        Op(destination, ref p, 0xe08948, 3);            // mov rax,rsp
        Op(destination, ref p, 0x0fe083, 3);            // and eax,15
        Op(destination, ref p, 0x08f883, 3);            // cmp eax,8 (Win64 entry alignment)
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x28623b48, 4);          // cmp rsp,[rdx+40]
        StopIf(destination, ref p, halt, 0x82);         // jb halt
        Op(destination, ref p, 0x20623b48, 4);          // cmp rsp,[rdx+32]
        StopIf(destination, ref p, halt, 0x83);         // jae halt
        Phase(destination, ref p, FirstExitAdoptionPhase.CanaryBeforeCheck);
        Canaries(destination, ref p, halt);
        Descriptors(destination, ref p, halt);

        // No call or output before permanent CR3 adoption. PGE stays cleared:
        // flushing it prevents firmware global translations surviving the load.
        Op(destination, ref p, 0xe0200f, 3);            // mov rax,cr4
        Op(destination, ref p, 0xffffff7f2548, 6);      // and rax,~0x80
        Op(destination, ref p, 0xe0220f, 3);            // mov cr4,rax
        Op(destination, ref p, 0x08428b48, 4);          // mov rax,[rdx+8]
        Op(destination, ref p, 0xd8220f, 3);            // mov cr3,rax (only CR3 write)
        Phase(destination, ref p, FirstExitAdoptionPhase.Cr3ReadBackCheck);
        Op(destination, ref p, 0xd8200f, 3);            // mov rax,cr3
        Op(destination, ref p, 0x50428948, 4);          // mov [rdx+80],rax
        Op(destination, ref p, 0x08423b48, 4);          // cmp rax,[rdx+8]
        StopIf(destination, ref p, halt, 0x85);
        Phase(destination, ref p, FirstExitAdoptionPhase.Cr4ReadBackCheck);
        Op(destination, ref p, 0xe0200f, 3);            // mov rax,cr4
        Op(destination, ref p, 0x58428948, 4);          // mov [rdx+88],rax
        Op(destination, ref p, 0x10428b4c, 4);          // mov r8,[rdx+16]
        Op(destination, ref p, 0xffffff7fe08149, 7);    // and r8,~0x80
        Op(destination, ref p, 0xc0394c, 3);            // cmp rax,r8
        StopIf(destination, ref p, halt, 0x85);
        Phase(destination, ref p, FirstExitAdoptionPhase.CanaryAfterCheck);
        Canaries(destination, ref p, halt);

        Phase(destination, ref p, FirstExitAdoptionPhase.OwnedDiagnostic);
        Op(destination, ref p, 0x38ec8348, 4);          // sub rsp,56 (shadow/context/alignment)
        Op(destination, ref p, 0x2024548948, 5);        // mov [rsp+32],rdx
        Op(destination, ref p, 0x40428b48, 4);          // mov rax,[rdx+64]
        Op(destination, ref p, 0xd0ff, 2);              // call rax (owned diagnostic only)
        Op(destination, ref p, 0x2024548b48, 5);        // mov rdx,[rsp+32]
        Phase(destination, ref p, FirstExitAdoptionPhase.UnexpectedDiagnosticReturn);
        Op(destination, ref p, 0xfcebf4fa, 4);          // halt: cli; hlt; jmp halt
        return p;
    }

    private static void Canaries(byte* destination, ref uint p, uint halt)
    {
        Op(destination, ref p, 0x18428b48, 4);          // mov rax,[rdx+24]
        Op(destination, ref p, 0xf8408b48, 4);          // mov rax,[rax-8]
        Op(destination, ref p, 0x30423b48, 4);          // cmp rax,[rdx+48]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x20428b48, 4);          // mov rax,[rdx+32]
        Op(destination, ref p, 0x008b48, 3);            // mov rax,[rax]
        Op(destination, ref p, 0x38423b48, 4);          // cmp rax,[rdx+56]
        StopIf(destination, ref p, halt, 0x85);
    }

    private static void Descriptors(byte* destination, ref uint p, uint halt)
    {
        Phase(destination, ref p, FirstExitAdoptionPhase.DescriptorInstall);
        Op(destination, ref p, 0x000000025c41c741, 8);  // mov dword [r9+92],2 (consume)
        Op(destination, ref p, 0x1059010f41, 5);        // lidt [r9+16]: firmware CS, IST=0
        Op(destination, ref p, 0x11010f41, 4);          // lgdt [r9]: retain bridge CS alias
        Op(destination, ref p, 0x0010b866, 4);          // mov ax,16
        Op(destination, ref p, 0xd08ec08ed88e, 6);      // mov ds,ax; mov es,ax; mov ss,ax
        Op(destination, ref p, 0x0018b866, 4);          // mov ax,24
        Op(destination, ref p, 0xd8000f, 3);            // ltr ax
        Op(destination, ref p, 0x2059010f41, 5);        // lidt [r9+32]: owned CS and ISTs

        // One local far transfer refreshes the hidden CS descriptor. The pushed
        // target is RIP+3 (immediately after push rax/retfq), never an EFI return.
        Op(destination, ref p, 0x086a, 2);              // push 8
        Op(destination, ref p, 0x00000003058d48, 7);    // lea rax,[rip+3]
        Op(destination, ref p, 0xcb4850, 3);            // push rax; retfq

        Phase(destination, ref p, FirstExitAdoptionPhase.DescriptorReadBackCheck);
        Op(destination, ref p, 0x3041010f41, 5);        // sgdt [r9+48]
        Op(destination, ref p, 0x4049010f41, 5);        // sidt [r9+64]
        Op(destination, ref p, 0x5049000f41, 5);        // str [r9+80]
        Op(destination, ref p, 0x018b49, 3);            // mov rax,[r9]
        Op(destination, ref p, 0x30413b49, 4);          // cmp rax,[r9+48]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x08418b4166, 5);        // mov ax,[r9+8]
        Op(destination, ref p, 0x38413b4166, 5);        // cmp ax,[r9+56]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x20418b49, 4);          // mov rax,[r9+32]
        Op(destination, ref p, 0x40413b49, 4);          // cmp rax,[r9+64]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x28418b4166, 5);        // mov ax,[r9+40]
        Op(destination, ref p, 0x48413b4166, 5);        // cmp ax,[r9+72]
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x185079834166, 6);      // cmp word [r9+80],24
        StopIf(destination, ref p, halt, 0x85);
        Op(destination, ref p, 0x60418b49, 4);          // mov rax,[r9+96] (GDT)
        Op(destination, ref p, 0x8b1d7880, 4);          // cmp byte [rax+29],0x8b (busy TSS)
        StopIf(destination, ref p, halt, 0x85);
        for (uint segment = 0; segment < 4; segment++)
        {
            byte modrm = segment == 0 ? (byte)0xc8 : segment == 1 ? (byte)0xd8 : segment == 2 ? (byte)0xc0 : (byte)0xd0;
            Op(destination, ref p, 0x8c66 | ((ulong)modrm << 16), 3); // mov ax,cs/ds/es/ss
            Op(destination, ref p, 0x41894166 | ((ulong)(84 + segment * 2) << 32), 5); // retain observation
            Op(destination, ref p, 0x3d66 | ((segment == 0 ? 8UL : 16UL) << 16), 4); // cmp ax,selector
            StopIf(destination, ref p, halt, 0x85);
        }
    }

    private static void Phase(byte* destination, ref uint p, FirstExitAdoptionPhase phase)
    {
        Op(destination, ref p, 0x6042c7, 3);            // mov dword [rdx+96],phase
        Op(destination, ref p, (uint)phase, 4);
    }

    private static void StopIf(byte* destination, ref uint p, uint halt, byte condition)
    {
        Op(destination, ref p, 0x0f | ((ulong)condition << 8), 2);
        Op(destination, ref p, unchecked(halt - p - 4), 4);
    }

    // Fixed x64 instruction encoding, also used without a destination to measure.
    private static void Op(byte* destination, ref uint p, ulong instruction, uint bytes)
    {
        for (uint i = 0; i < bytes; i++)
        {
            if (destination != null) destination[p] = (byte)(instruction >> (int)(i * 8));
            p++;
        }
    }
}

// Every returned value is a refusal/failure. Successful exit never returns.
public enum FirstExitReturn
{
    NotStarted, ReentryRejected, AlreadyConsumed, FirmwareUnavailable,
    InvalidContinuation, InvalidStorage, ExitNotCompleted
}

public unsafe struct FirstExitTransitionState
{
    internal FirstExitPhase PhaseValue;
    internal FirmwareServicesMode ModeValue;
    internal BoundedExitRetryState Retry;
    internal BoundedExitRetryResult AttemptValue;

    public FirstExitPhase Phase => PhaseValue;
    public FirmwareServicesMode Mode => ModeValue;
    public BoundedExitRetryResult Attempt => AttemptValue;
    public bool RetainedFirmwareCallbacksCleared =>
        Retry.First.Exit.Function == null && Retry.First.Exit.Handle == null &&
        Retry.Retry.Exit.Function == null && Retry.Retry.Exit.Handle == null;
}

/// <summary>
/// One terminal handoff using the accepted final-map/two-attempt retry core.
/// A successful exit clears this coordinator's retained firmware callbacks and
/// transfers to the original owned continuation; it cannot return to an EFI
/// caller. An unexpected continuation return spins without calling anything.
///
/// Before entry, the adapter must authenticate the firmware pointers and owned
/// continuation, prepare all mappings/descriptors/COM1/stack/canaries, and prove
/// that state, map buffer, continuation code and context survive permanent CR3
/// adoption. The continuation must adopt that hierarchy before any output or
/// other callback and terminate using owned facilities. This coordinator does
/// not load CR3, authenticate pointers, or revoke independently copied handles.
///
/// Retain one zero-initialized state; never copy/reset it. Access is exclusive
/// and callbacks must not mutate its storage or throw. Returned failures leave
/// the last attempt available for owned diagnostics; after an issued exit call
/// the adapter must not resume ordinary firmware work even if exit failed.
/// </summary>
public static unsafe class FirstOptRealExitBootServicesTransition
{
    public static FirstExitReturn AttemptAndTransfer(ref FirstExitTransitionState state,
        FirmwareServicesMode mode,
        delegate* unmanaged<ulong*, void*, ulong*, ulong*, uint*, ulong> getMemoryMap,
        delegate* unmanaged<void*, ulong, ulong> exitBootServices,
        void* imageHandle, byte* ownedMap, ulong mapCapacity,
        delegate* unmanaged<FirstExitTransitionState*, void*, void> ownedContinuation,
        void* ownedContext, ulong contextBytes)
    {
        if (state.PhaseValue == FirstExitPhase.InFlight)
            return FirstExitReturn.ReentryRejected;
        if (state.PhaseValue != FirstExitPhase.Empty)
            return FirstExitReturn.AlreadyConsumed;
        if (mode != FirmwareServicesMode.BootServicesAvailable)
            return FirstExitReturn.FirmwareUnavailable;
        if (ownedContinuation == null)
            return FirstExitReturn.InvalidContinuation;

        ulong map = (ulong)ownedMap, context = (ulong)ownedContext;
        if (!Extent(map, mapCapacity) || mapCapacity < UefiMemoryMapCopyPlan.MinimumDescriptorSize ||
            !Extent(context, contextBytes) || Overlap(map, mapCapacity, context, contextBytes))
            return FirstExitReturn.InvalidStorage;

        fixed (FirstExitTransitionState* address = &state)
        {
            ulong stateStart = (ulong)address, stateBytes = (ulong)sizeof(FirstExitTransitionState);
            if (!Extent(stateStart, stateBytes) ||
                Overlap(stateStart, stateBytes, map, mapCapacity) ||
                Overlap(stateStart, stateBytes, context, contextBytes))
                return FirstExitReturn.InvalidStorage;

            state.PhaseValue = FirstExitPhase.InFlight;
            state.ModeValue = mode;
            BoundedExitRetryResult attempt = BoundedRetryStaleKeyStatus.Execute(ref state.Retry,
                mode, getMemoryMap, exitBootServices, imageHandle, ownedMap, mapCapacity);
            // No logging, allocation or firmware operation follows a successful exit.
            if (attempt.Succeeded)
                state.ModeValue = FirmwareServicesMode.BootServicesExited;
            state.AttemptValue = attempt;
            state.Retry = default;
            if (!attempt.Succeeded)
            {
                state.PhaseValue = FirstExitPhase.Failed;
                return FirstExitReturn.ExitNotCompleted;
            }

            state.PhaseValue = FirstExitPhase.OwnedContinuation;
            ownedContinuation(address, ownedContext);
            state.PhaseValue = FirstExitPhase.UnexpectedContinuationReturn;
            while (true) { }
        }
    }

    private static bool Extent(ulong start, ulong bytes) =>
        start != 0 && bytes != 0 && bytes <= UefiMemoryMapCopyPlan.MaximumUnsignedLong - start;

    private static bool Overlap(ulong a, ulong an, ulong b, ulong bn) =>
        a < b + bn && b < a + an;
}
