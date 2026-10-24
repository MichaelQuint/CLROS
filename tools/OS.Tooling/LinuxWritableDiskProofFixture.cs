using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;

/// <summary>Finite C#-authored writable Linux disk fixture. One worker checks
/// exact syscall results and copied bytes, then exits42; any mismatch exits99.
/// Construction supplies package/instruction values only. Continuing create,
/// growth, delete, descriptor waits and real OUT/FLUSH joins remain requirements
/// of a separate original guest owner; no SQLite or durability claim follows.</summary>
public static class LinuxWritableDiskProofFixture
{
    public const string AppPath = "apps/m16writabledisk.elf", Variant = "WritableDisk";
    public const string Witness = "M16_WRITABLE_DISK_OK!", FilePath = "/data/m16-write.bin", RootPath = "/data";
    public const string OutsidePath = "/data/../private/sentinel";
    public const ulong ImageBase = 0x400000;
    public const int ElfBytes = 0x4000, EntryOffset = 0x200, FailureOffset = 0xf00, WitnessOffset = 0x180;
    public const ulong FileName = 0x401000, RootName = 0x401080, OutsideName = 0x4010c0;
    public const ulong Iovec = 0x401200, WriteLock = 0x401240, QueryLock = 0x401280, Unlock = 0x4012c0;
    public const ulong Zeroes = 0x401300, Payload = 0x402000, Output = 0x403000;
    public const uint PayloadBytes = 1032, FileOffset = 4094, ResultingFileBytes = FileOffset + PayloadBytes;
    public const uint RwfNoAppend = 0x20, CreateFlags = 2 | 0x40 | 0x80 | 0x20000 | 0x80000;
    public const uint ReadFlags = 0x8000 | 0x20000 | 0x80000, RootFlags = 0x8000 | 0x80000;
    public const long ReservedLockByte = 1073741825;
    public const byte Sentinel = 0xa5;

    /// <summary>Copied six-register Linux x64 syscall contract. Positional
    /// pwritev2 uses A3/A4 for the low/high offset and A5 for RWF_NOAPPEND.
    /// OutputBytes bounds the checked copy-out range; it does not authenticate
    /// user mappings, continuations, device requests or completion timing.</summary>
    public readonly record struct CallSpec(string Role, ulong Number, ulong A0, ulong A1, ulong A2,
        ulong A3, ulong A4, ulong A5, ulong ReturnRip, long Result, uint OutputBytes);

    /// <summary>Return fresh deterministic bytes for the fixed static ELF and
    /// existing mechanical package envelope. The envelope permits two threads;
    /// this instruction stream never clones or requests user timer interrupts.</summary>
    public static byte[] CreatePackage() => LinuxThreadProofFixture.CreateMechanicalPackage(
        CreateElf(), Variant, AppPath, userInterrupts: false, socketListener: true);
    public static byte[] CreateElf() => Build().Elf;
    public static IReadOnlyList<CallSpec> Calls() => Array.AsReadOnly(Build().Calls);
    public static IReadOnlyList<ulong> InstructionRips() => Array.AsReadOnly(Build().Instructions);
    public static IReadOnlyList<ulong> ExpectedReturnRips() => Array.AsReadOnly(Build().Calls.Select(c => c.ReturnRip).ToArray());
    public static uint ExpectedCalls => checked((uint)Build().Calls.Length);
    public static ulong SuccessRip => Build().Calls[^1].ReturnRip;
    public static ulong MainEndRip => Build().MainEnd;
    public const ulong FailureExitRip = ImageBase + FailureOffset + 41;

    /// <summary>Return an independent payload copy. Its deterministic varying
    /// bytes cross both the 4096-byte boundary and multiple 512-byte sectors.</summary>
    public static byte[] ExpectedPayload()
    {
        byte[] result = new byte[PayloadBytes];
        for (uint i = 0; i < PayloadBytes; i++) result[i] = unchecked((byte)((i * 73 + 19) ^ (i >> 3) ^ (i >> 9)));
        return result;
    }

    /// <summary>Borrow complete package bytes synchronously and require the
    /// exact canonical envelope and ELF. Changed, foreign or truncated input
    /// throws; success supplies no native execution or persistence authority.</summary>
    public static void RequirePackage(ReadOnlySpan<byte> package)
    { if (!package.SequenceEqual(CreatePackage())) throw new ToolFailure("writable_disk_package_identity"); }

    private sealed record Built(byte[] Elf, CallSpec[] Calls, ulong[] Instructions, ulong MainEnd);
    private static Built Build()
    {
        byte[] elf = new byte[ElfBytes]; elf.AsSpan(EntryOffset, 4096 - EntryOffset).Fill(0xcc);
        "\u007fELF"u8.CopyTo(elf); elf[4] = 2; elf[5] = 1; elf[6] = 1;
        void U16(int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at), value);
        void U32(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), value);
        void U64(int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), value);
        void Text(int at, string value) => Encoding.ASCII.GetBytes(value).CopyTo(elf, at);
        U16(16, 2); U16(18, 62); U32(20, 1); U64(24, ImageBase + EntryOffset);
        U64(32, 64); U16(52, 64); U16(54, 56); U16(56, 3);
        void Load(int at, uint type, uint flags, ulong offset, ulong address, ulong length, ulong alignment)
        { U32(at, type); U32(at + 4, flags); U64(at + 8, offset); U64(at + 16, address); U64(at + 24, address); U64(at + 32, length); U64(at + 40, length); U64(at + 48, alignment); }
        Load(64, 1, 5, 0, ImageBase, 4096, 4096);
        Load(120, 1, 6, 4096, ImageBase + 4096, ElfBytes - 4096, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        Text(WitnessOffset, Witness); Text((int)(FileName - ImageBase), FilePath + "\0");
        Text((int)(RootName - ImageBase), RootPath + "\0"); Text((int)(OutsideName - ImageBase), OutsidePath + "\0");
        U64((int)(Iovec - ImageBase), Payload); U64((int)(Iovec - ImageBase + 8), PayloadBytes);
        void Flock(ulong address, ushort type)
        { int at = (int)(address - ImageBase); U16(at, type); U64(at + 8, (ulong)ReservedLockByte); U64(at + 16, 1); }
        Flock(WriteLock, 1); Flock(QueryLock, 1); Flock(Unlock, 2);
        ExpectedPayload().CopyTo(elf, (int)(Payload - ImageBase));
        elf.AsSpan((int)(Output - ImageBase), 4096).Fill(Sentinel);
        Writer w = new(elf, EntryOffset);
        w.Instruction(0xc031, 2); w.Instruction(0xc88c66, 3); w.Compare(0x3b);
        w.Call("create", 2, FileName, CreateFlags, 0x180, 3); // mode0600, with no POSIX-mode persistence claim.
        w.Call("write.positioned", 328, 3, Iovec, 1, PayloadBytes, a3: FileOffset, a4: 0, a5: RwfNoAppend);
        w.Call("write.cursor", 8, 3, 0, 1, 0);
        w.Call("read.hole-boundary", 17, 3, Output, 16, 16, 16, FileOffset - 8);
        w.CompareBytes(Output, Zeroes, 8); w.CompareBytes(Output + 8, Payload, 8);
        w.Call("open.independent", 2, FileName, ReadFlags, 0, 4);
        w.Call("read.hole", 0, 4, Output, 16, 16, 16); w.CompareBytes(Output, Zeroes, 16);
        w.Call("read.seek", 8, 4, FileOffset, 0, FileOffset);
        w.Call("read.payload", 0, 4, Output, 8, 8, 8); w.CompareBytes(Output, Payload, 8);
        w.Call("read.independent-cursor", 8, 3, 0, 1, 0);
        w.Call("lock.set", 72, 3, 6, WriteLock, 0);
        w.Call("lock.get-own", 72, 3, 5, QueryLock, 0, 32); w.MemoryDword(QueryLock, 2);
        w.Call("lock.release", 72, 3, 6, Unlock, 0);
        w.Call("sync.file", 74, 3, 0, 0, 0);
        w.Call("sync.parent-open", 2, RootName, RootFlags, 0, 5);
        w.Call("sync.parent", 74, 5, 0, 0, 0); w.Call("sync.parent-close", 3, 5, 0, 0, 0);
        w.Call("close.independent", 3, 4, 0, 0, 0); w.Call("close.original", 3, 3, 0, 0, 0);
        w.Call("reopen", 2, FileName, ReadFlags, 0, 3);
        w.Call("read.reopened", 17, 3, Output, PayloadBytes, PayloadBytes, PayloadBytes, FileOffset);
        w.CompareBytes(Output, Payload, PayloadBytes); w.MemoryByte(Output + PayloadBytes, Sentinel);
        w.Call("close.reopened", 3, 3, 0, 0, 0);
        w.Call("unlink", 87, FileName, 0, 0, 0); w.Call("open.deleted", 2, FileName, ReadFlags, 0, -2);
        w.Call("sync.deleted-parent-open", 2, RootName, RootFlags, 0, 3);
        w.Call("sync.deleted-parent", 74, 3, 0, 0, 0); w.Call("sync.deleted-parent-close", 3, 3, 0, 0, 0);
        w.Call("read.closed", 0, 3, Output, 1, -9); w.Call("open.outside", 2, OutsideName, ReadFlags, 0, -13);
        w.Call("witness", 1, 1, ImageBase + WitnessOffset, (uint)Witness.Length, Witness.Length);
        w.Syscall("exit", 231, 42, 0, 0, 0); w.JumpFailure();
        if (w.Position >= FailureOffset || w.Calls.Count > 64) throw new ToolFailure("writable_disk_code_capacity");
        Built result = new(elf, w.Calls.ToArray(), w.Instructions.ToArray(), ImageBase + (uint)w.Position);
        Writer failure = new(elf, FailureOffset); failure.Instruction(0xfc, 1); failure.Syscall("failure", 231, 99, 0, 0, 0); failure.Instruction(0xf4, 1);
        if (failure.Calls[0].ReturnRip != FailureExitRip) throw new ToolFailure("writable_disk_failure_rip");
        return result;
    }

    private sealed class Writer(byte[] bytes, int position)
    {
        public int Position = position;
        public readonly List<CallSpec> Calls = [];
        public readonly List<ulong> Instructions = [];
        private void Start() => Instructions.Add(ImageBase + (uint)Position);
        private void Op(ulong value, int count) { for (int i = 0; i < count; i++) bytes[Position++] = (byte)(value >> (i * 8)); }
        public void Instruction(ulong value, int count) { Start(); Op(value, count); }
        private void Immediate(uint register, ulong value)
        { Start(); if (register >= 8) Op(0x41, 1); Op(0xb8 + (register & 7), 1); Op(checked((uint)value), 4); }
        private void Rip(ulong opcode, int count, ulong address)
        { Start(); Op(opcode, count); Op(unchecked((uint)((long)(address - ImageBase) - (Position + 4))), 4); }
        private void BranchFailure()
        { Start(); Op(0x850f, 2); Op(unchecked((uint)(FailureOffset - (Position + 4))), 4); }
        public void JumpFailure()
        { Start(); Op(0xe9, 1); Op(unchecked((uint)(FailureOffset - (Position + 4))), 4); }
        public void Compare(long value)
        { Start(); Op(0x3d48, 2); Op(unchecked((uint)value), 4); BranchFailure(); }
        public void MemoryByte(ulong address, byte value) { Rip(0x05b60f, 3, address); Compare(value); }
        public void MemoryDword(ulong address, uint value) { Rip(0x058b, 2, address); Compare(value); }
        public void CompareBytes(ulong destination, ulong source, uint count)
        { Immediate(7, destination); Immediate(6, source); Immediate(1, count); Instruction(0xfc, 1); Instruction(0xa6f3, 2); BranchFailure(); }
        public void Call(string role, uint number, ulong a0, ulong a1, ulong a2, long result,
            uint output = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        { Syscall(role, number, a0, a1, a2, result, output, a3, a4, a5); Compare(result); }
        public void Syscall(string role, uint number, ulong a0, ulong a1, ulong a2, long result,
            uint output = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            Immediate(0, number); Immediate(7, a0); Immediate(6, a1); Immediate(2, a2);
            Immediate(10, a3); Immediate(8, a4); Immediate(9, a5); Instruction(0x050f, 2);
            Calls.Add(new(role, number, a0, a1, a2, a3, a4, a5, ImageBase + (uint)Position, result, output));
        }
    }
}
