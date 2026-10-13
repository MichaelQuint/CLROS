namespace OS.Kernel.Threading;

/// <summary>
/// Fixed C#-authored CPL0 timer interrupt entry for an independently owned IST.
/// The live owner admits one BSP, legacy xAPIC, CS8/SS16, MSR/FXSAVE support,
/// no AVX state, guarded IST/callback stack capacity and RX code/RW-NX data.
/// All GPR/FS/GS/x87/SSE capture precedes the Win64 callback, which must freshly
/// admit the entire return frame before returning byte1. No SWAPGS or STI occurs.
/// Unknown callback results or failed restoration halt with IF clear and retain
/// IDT/TSS/stack/mapping lifetimes. Emission/byte identity alone proves neither
/// live ownership nor interrupt execution. No SMP, user-mode or general IRQ ABI.
/// </summary>
public static unsafe class X64TimerInterrupt
{
    public const uint Bytes = 512, FrameBytes = 704, PrefixBytes = 544;

    /// <summary>Actual stack image after fifteen pushes and a sixteen-aligned
    /// prefix. The last five words are the hardware no-error IRETQ frame, including
    /// old RSP/SS even at CPL0. The pointer borrows the owned IST until IRETQ; no
    /// caller may retain it or publish a partially modified return frame. Complete
    /// is written last before callback entry. Kernel-GS base is never changed.</summary>
    public struct Frame
    {
        // Natural sequential8-byte fields avoid adding interop attributes to the
        // explicit no-stdlib EFI surface. The owner supplies16-byte stack alignment;
        // focused tests independently check every byte offset and total size.
        public fixed byte FxSave[512];
        public ulong FsBase, GsBase, EntryFlags, Complete;
        public ulong R15, R14, R13, R12, R11, R10, R9, R8;
        public ulong Rdi, Rsi, Rbp, Rbx, Rdx, Rcx, Rax;
        public ulong Rip, Cs, Rflags, Rsp, Ss;
    }

    /// <summary>Retained native callback and immutable neutral FXSAVE image.
    /// Callback signature is byte(void* context, Frame* frame), Win64, on the owned
    /// IST with IF clear/DF clear. Returning1 authorizes every field for IRETQ;
    /// all other bytes fail closed. Data must remain mapped/pinned through cleanup.
    /// Addresses/ranges are planning inputs, not mapping/CPU/lifetime proof.</summary>
    public struct Plan
    {
        public ulong CodeAddress, Callback, CallbackContext, HelperFx;
        public uint CallbackBytes;
    }

    /// <summary>Materialize into live, unpublished caller-owned writable storage.
    /// Reject malformed/aliased plans before any write. The caller independently
    /// verifies neutral FP bytes, complete IDT/IST and every native callback before
    /// RX publication/IF enable. Success executes no privileged instruction.</summary>
    public static bool Emit(byte* output, uint capacity, Plan plan)
    {
        if (!Inputs(output, capacity, plan)) return false;
        Writer dry = new(null, false);
        if (!Encode(ref dry, plan)) return false;
        Writer writer = new(output, false);
        return Encode(ref writer, plan);
    }

    /// <summary>Read-only comparison of every code/padding/immediate/branch byte
    /// against the retained plan. Exclusive live storage is required; no native
    /// admission or execution follows from a matching result.</summary>
    public static bool Verify(byte* code, uint capacity, Plan plan)
    {
        if (!Inputs(code, capacity, plan)) return false;
        Writer writer = new(code, true);
        return Encode(ref writer, plan);
    }

    private static bool Inputs(byte* output, uint capacity, Plan p)
    {
        ulong address = (ulong)output;
        if (capacity < Bytes || !Range(address, Bytes, 1) || !Range(p.CodeAddress, Bytes, 16) ||
            !Range(p.Callback, 1, 1) || p.CallbackBytes < 8 || !Range(p.CallbackContext, p.CallbackBytes, 8) ||
            !Range(p.HelperFx, 512, 16)) return false;
        return !Overlap(address, Bytes, p.CallbackContext, p.CallbackBytes) && !Overlap(address, Bytes, p.HelperFx, 512) &&
            !Overlap(p.CodeAddress, Bytes, p.CallbackContext, p.CallbackBytes) && !Overlap(p.CodeAddress, Bytes, p.HelperFx, 512) &&
            !Overlap(p.CallbackContext, p.CallbackBytes, p.HelperFx, 512) &&
            !Overlap(p.Callback, 1, p.CodeAddress, Bytes) && !Overlap(p.Callback, 1, address, Bytes) &&
            !Overlap(p.Callback, 1, p.CallbackContext, p.CallbackBytes) && !Overlap(p.Callback, 1, p.HelperFx, 512);
    }
    private static bool Range(ulong address, uint bytes, uint alignment) => address != 0 && bytes != 0 &&
        (address & (alignment - 1)) == 0 && address <= ulong.MaxValue - (bytes - 1UL) &&
        Canonical(address) && Canonical(address + bytes - 1UL);
    private static bool Canonical(ulong v) => v >> 48 == ((v & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
    private static bool Overlap(ulong a, uint aBytes, ulong b, uint bBytes) => a <= b + bBytes - 1UL && b <= a + aBytes - 1UL;

    private static bool Encode(ref Writer w, Plan p)
    {
        w.Data(0xfa1e0ff3, 4); // ENDBR64.
        // RAX, RCX, RDX, RBX, RBP, RSI, RDI, R8..R15; never use scratch first.
        w.Data(0x57565553525150, 7);
        for (uint register = 0; register < 8; register++) { w.Byte(0x41); w.Byte((byte)(0x50 + register)); }
        w.Data(0xec8148, 3); w.Data(PrefixBytes, 4); // SUB RSP,544; entry8mod16 -120 -544 =0mod16.
        w.Data(0x2404ae0f48, 5); // FXSAVE64 [RSP], BEFORE any C# or neutral-FP restore.
        CaptureBase(ref w, 0xc0000100, 512);
        CaptureBase(ref w, 0xc0000101, 520);
        w.Data(0x589c, 2); StoreRax(ref w, 528); // Actual entry flags; hardware flags remain at680.
        w.Data(0x2484c748, 4); w.Data(536, 4); w.Data(1, 4); // Complete publication last.
        w.Byte(0xfc); // CLD after complete capture.
        w.Data(0xb848, 2); w.Data(p.HelperFx, 8); w.Data(0x08ae0f48, 4); // Neutral FXRSTOR64 [RAX].
        w.Data(0xb948, 2); w.Data(p.CallbackContext, 8); // RCX=context.
        w.Data(0xe28948, 3); // RDX=frame, before shadow allocation.
        w.Data(0x20ec8348, 4); // Win6432-byte shadow; CALL-site RSP remains16 aligned.
        w.Data(0xb848, 2); w.Data(p.Callback, 8); w.Data(0xd0ff, 2); // CALL RAX.
        w.Byte(0xfa); // Restoration stays IF clear; only the admitted IRETQ frame can restore IF.
        w.Data(0x20c48348, 4);
        w.Data(0x013c, 2); uint callbackRejected = w.Jne(); // Exactly byte1.
        CompareWord(ref w, 536, 1); uint incomplete = w.Jne();
        CompareWord(ref w, 672, 8); uint wrongCs = w.Jne();
        CompareWord(ref w, 696, 16); uint wrongSs = w.Jne();
        uint wrongFs = RestoreBase(ref w, 0xc0000100, 512);
        uint wrongGs = RestoreBase(ref w, 0xc0000101, 520);
        w.Data(0x240cae0f48, 5); // FXRSTOR64 [RSP]; callback clobbers are discarded.
        w.Data(0xc48148, 3); w.Data(PrefixBytes, 4);
        for (int register = 7; register >= 0; register--) { w.Byte(0x41); w.Byte((byte)(0x58 + register)); }
        w.Data(0x58595a5b5d5e5f, 7); // RDI,RSI,RBP,RBX,RDX,RCX,RAX.
        w.Data(0xcf48, 2); // IRETQ restores exact old RSP/SS, not an inferred entryRSP+40.
        uint reject = w.Offset;
        w.Data(0xfceb90f3fa, 5); // CLI; PAUSE; JMP back to PAUSE. Retain owned resources.
        w.Patch(callbackRejected, reject); w.Patch(incomplete, reject); w.Patch(wrongCs, reject);
        w.Patch(wrongSs, reject); w.Patch(wrongFs, reject); w.Patch(wrongGs, reject);
        while (w.Offset < Bytes) w.Byte(0);
        return w.Valid && w.Offset == Bytes;
    }
    private static void StoreRax(ref Writer w, uint offset) { w.Data(0x24848948, 4); w.Data(offset, 4); }
    private static void CaptureBase(ref Writer w, uint msr, uint offset)
    {
        w.Byte(0xb9); w.Data(msr, 4); w.Data(0x320f, 2);
        w.Data(0x20e2c148, 4); w.Data(0xd00948, 3); StoreRax(ref w, offset);
    }
    private static void CompareWord(ref Writer w, uint offset, byte value)
    { w.Data(0x24bc8348, 4); w.Data(offset, 4); w.Byte(value); }
    private static uint RestoreBase(ref Writer w, uint msr, uint offset)
    {
        w.Data(0x24848b4c, 4); w.Data(offset, 4); // R8=[frame+offset], independently retained desired base.
        w.Data(0xc0894c, 3); w.Data(0xc28948, 3); w.Data(0x20eac148, 4);
        w.Byte(0xb9); w.Data(msr, 4); w.Data(0x300f, 2); w.Data(0x320f, 2);
        w.Data(0x20e2c148, 4); w.Data(0xd00948, 3); w.Data(0xc0394c, 3); // CMP RAX,R8.
        return w.Jne();
    }
    private struct Writer(byte* output, bool compare)
    {
        public uint Offset;
        public bool Valid = true;
        private readonly byte* _output = output;
        private readonly bool _compare = compare;
        public void Byte(byte value)
        {
            if (Offset >= Bytes) { Valid = false; return; }
            if (_output != null) { if (_compare) Valid &= _output[Offset] == value; else _output[Offset] = value; }
            Offset++;
        }
        public void Data(ulong value, uint bytes) { for (uint i = 0; i < bytes; i++) Byte((byte)(value >> (int)(8 * i))); }
        public uint Jne() { Data(0x850f, 2); uint position = Offset; Offset += 4; return position; }
        public void Patch(uint position, uint target)
        {
            if (position > Offset || Offset - position < 4 || target >= Bytes) { Valid = false; return; }
            uint displacement = target - (position + 4);
            for (uint i = 0; i < 4; i++)
            {
                byte value = (byte)(displacement >> (int)(8 * i));
                if (_output != null) { if (_compare) Valid &= _output[position + i] == value; else _output[position + i] = value; }
            }
        }
    }
}
