using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;

/// <summary>Two finite cooperative mechanical disk controls. Main parks before
/// the child runs; no CPL3 timer preemption is requested. Construction and the
/// copied instruction/call vectors prove bytes only. Original native, scheduler,
/// memory, device and runner joins remain separate obligations.</summary>
public static class LinuxDiskRaceProofFixture
{
    public const string AppPath = "apps/m16diskrace.elf";
    public static IReadOnlyList<string> Variants { get; } = Array.AsReadOnly<string>(["Progress", "CloseReuse"]);
    public const ulong ImageBase = 0x400000, ChildTls = 0x402000, ChildStackTop = 0x404000;
    public const ulong ParentOutput = 0x404000, ChildOutput = 0x406000;
    public const uint ParentOutputBytes = 8192, ChildOutputBytes = 4096;
    public const int ElfBytes = 0x7000, EntryOffset = 0x200, ChildOffset = 0x900, FailureOffset = 0xf00;
    public const ulong Path = 0x401000, EmptyPath = 0x401080, Stat = 0x401400;
    public const ulong PipePair = 0x401600, ParentTid = 0x401608, ClearTid = 0x40160c;
    public const ulong Payload = 0x401620, PipeOutput = 0x401630;
    public const uint OriginalFd = 3, IndependentFd = 4, PipeReadFd = 5, PipeWriteFd = 6, DuplicateFd = 7;
    public const uint PipeWord = 0x45434152; // RACE, little endian.
    public const ulong MainIdentity = 0x100000001, ChildIdentity = 0x100000002;
    public const byte Sentinel = 0xa5;
    public const bool UserInterrupts = false;

    /// <summary>None is immediate; Disk always owns a source4 wait. PipeRead
    /// may return4 immediately or park on source2. Join may return-EAGAIN after
    /// child exit or park on source1 and return0. These are exact finite fixture
    /// alternatives, not authority to invent a wait or a completion.</summary>
    public enum WaitPolicy : byte { None, Disk, PipeRead, Join }

    /// <summary>Copied scalar syscall inventory. Child has no synthetic clone
    /// call: CloneReturnRip identifies its initial saved continuation. Output
    /// ceiling is the initially mapped writable range for a disk completion;
    /// result is the checked final value (Join additionally admits-EAGAIN).
    /// Role binds cross-worker requirements without exposing writer objects.</summary>
    public readonly record struct CallSpec(string Role, ulong Number, ulong A0, ulong A1, ulong A2,
        ulong A3, ulong A4, ulong A5, ulong ReturnRip, long Result, WaitPolicy Wait, uint OutputBytes);

    /// <summary>Fixed user-memory ownership for native sentinel/data joins.
    /// The child stack occupies the page below ChildStackTop, outside both
    /// output ranges. ClearTid starts at2 and must become0 on actual child exit.
    /// These addresses grant no permission to read an unvalidated user mapping.</summary>
    public readonly record struct MemoryLayout(ulong ParentBuffer, uint ParentBytes, ulong ChildBuffer,
        uint ChildBytes, byte Fill, ulong ChildStack, ulong ChildTlsBase, ulong ClearTidAddress,
        ulong ParentTidAddress, ulong PipePairAddress, ulong PayloadAddress, ulong PipeOutputAddress);

    public static MemoryLayout Memory => new(ParentOutput, ParentOutputBytes, ChildOutput, ChildOutputBytes,
        Sentinel, ChildStackTop, ChildTls, ClearTid, ParentTid, PipePair, Payload, PipeOutput);
    public static string Witness(string variant) { Validate(variant); return variant == "Progress" ? "M16_DISK_PROGRESS_OK!" : "M16_DISK_CLOSE_REUSE_OK!"; }
    public static byte[] CreateElf(string variant) => Build(variant).Elf;
    public static byte[] CreatePackage(string variant) => LinuxThreadProofFixture.CreateMechanicalPackage(
        CreateElf(variant), "DiskRace" + variant, AppPath, userInterrupts: false, socketListener: true);
    public static IReadOnlyList<CallSpec> Calls(string variant, bool child = false) =>
        Array.AsReadOnly(child ? Build(variant).ChildCalls : Build(variant).MainCalls);
    public static IReadOnlyList<ulong> InstructionRips(string variant, bool child = false) =>
        Array.AsReadOnly(child ? Build(variant).ChildInstructions : Build(variant).MainInstructions);
    public static ulong CloneReturnRip(string variant) => Build(variant).CloneCall;
    public static ulong MainEndRip(string variant) => Build(variant).MainEnd;
    public static ulong ChildEndRip(string variant) => Build(variant).ChildEnd;
    public static ulong FailureExitRip => ImageBase + FailureOffset + 35;

    /// <summary>Identify complete canonical bytes, never manifest args alone.
    /// Inputs are borrowed synchronously and not retained or mutated. Throws for
    /// unknown, changed or truncated packages; success grants no execution.</summary>
    public static string PackageVariant(ReadOnlySpan<byte> package)
    {
        foreach (string variant in Variants) if (package.SequenceEqual(CreatePackage(variant))) return variant;
        throw new ToolFailure("disk_race_package_identity");
    }

    private sealed record Built(byte[] Elf, CallSpec[] MainCalls, CallSpec[] ChildCalls,
        ulong[] MainInstructions, ulong[] ChildInstructions, ulong CloneCall, ulong MainEnd, ulong ChildEnd);

    private static Built Build(string variant)
    {
        Validate(variant); bool close = variant == "CloseReuse";
        byte[] elf = new byte[ElfBytes]; elf.AsSpan(EntryOffset, 4096 - EntryOffset).Fill(0xcc);
        "\u007fELF"u8.CopyTo(elf); elf[4] = 2; elf[5] = 1; elf[6] = 1;
        void U16(int at, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at), v);
        void U32(int at, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), v);
        void U64(int at, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), v);
        U16(16, 2); U16(18, 62); U32(20, 1); U64(24, ImageBase + EntryOffset);
        U64(32, 64); U16(52, 64); U16(54, 56); U16(56, 3);
        void Load(int at, uint type, uint flags, ulong offset, ulong address, ulong bytes, ulong alignment)
        { U32(at, type); U32(at + 4, flags); U64(at + 8, offset); U64(at + 16, address); U64(at + 24, address); U64(at + 32, bytes); U64(at + 40, bytes); U64(at + 48, alignment); }
        Load(64, 1, 5, 0, ImageBase, 4096, 4096); Load(120, 1, 6, 4096, ImageBase + 4096, 6 * 4096, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        void Text(ulong address, string text) => Encoding.ASCII.GetBytes(text).CopyTo(elf, checked((int)(address - ImageBase)));
        Text(Path, "/app/wwwroot/fixture/fragmented.bin\0"); Text(EmptyPath, "/app/wwwroot/fixture/empty.bin\0");
        Text(ImageBase + 0x180, Witness(variant)); U32((int)(ClearTid - ImageBase), 2); U32((int)(Payload - ImageBase), PipeWord);
        elf.AsSpan((int)(ParentOutput - ImageBase), (int)ParentOutputBytes).Fill(Sentinel);
        elf.AsSpan((int)(ChildOutput - ImageBase), (int)ChildOutputBytes).Fill(Sentinel);
        Writer p = new(elf, EntryOffset);
        p.Immediate(10, 0); p.Immediate(8, 0); p.Immediate(9, 0);
        p.Call("prep.original", 2, Path, 0, 0, 3, WaitPolicy.Disk);
        p.Call("prep.independent", 2, Path, 0, 0, 4, WaitPolicy.Disk);
        p.Call("prep.pipe", 293, PipePair, 0, 0, 0); p.MemoryDword(PipePair, 5); p.MemoryDword(PipePair + 4, 6);
        p.Call("prep.duplicate", 72, 3, 0, 7, 7);
        ulong clone = p.Syscall("prep.clone", 56, 0x7d0f00, ChildStackTop, ParentTid, 2,
            WaitPolicy.None, 0, ClearTid, ChildTls);
        int childPrefix = p.Instructions.Count;
        p.Immediate(10, 0); p.Immediate(8, 0); p.Instruction(0xc08548, 3); p.Branch(0x84, ChildOffset); p.Compare(2);
        ulong[] prefix = p.Instructions.Skip(childPrefix).Take(4).ToArray();
        p.MemoryDword(ParentTid, 2);
        p.Call("main.read", 0, 3, ParentOutput, 8192, close ? -9 : 8192, WaitPolicy.Disk, 8192);
        if (close) p.Fill(ParentOutput, 8192, Sentinel);
        else { p.MemoryByte(ParentOutput, Pattern(0)); p.MemoryByte(ParentOutput + 8191, Pattern(8191)); }
        p.Call("main.pipe", 0, 5, PipeOutput, 4, 4, WaitPolicy.PipeRead); p.MemoryDword(PipeOutput, PipeWord);
        if (!close)
        {
            p.Call("main.pread", 17, 4, ParentOutput, 16, 16, WaitPolicy.Disk, 16, 131000);
            p.MemoryByte(ParentOutput, Pattern(131000)); p.MemoryByte(ParentOutput + 15, Pattern(131015));
        }
        p.Join(); p.MemoryDword(ClearTid, 0);
        if (close)
        {
            p.Call("main.replacement", 17, 3, ParentOutput, 1, 0, WaitPolicy.Disk, 1, 0); p.Fill(ParentOutput, 8192, Sentinel);
            p.Call("main.duplicate", 0, 7, ParentOutput, 8, 8, WaitPolicy.Disk, 8);
            p.MemoryByte(ParentOutput, Pattern(0)); p.MemoryByte(ParentOutput + 7, Pattern(7));
            p.Call("main.replacement-stat", 5, 3, Stat, 0, 0); p.MemoryQword(Stat + 48, 0);
        }
        p.Call("main.cursor", 8, 7, 0, 1, close ? 8 : 8200, WaitPolicy.Disk);
        p.Call("main.independent-cursor", 8, 4, 0, 1, 0, WaitPolicy.Disk);
        foreach (uint fd in new uint[] { 3, 4, 7, 5, 6 }) p.Call("main.close" + fd, 3, fd, 0, 0, 0);
        p.Call("main.witness", 1, 1, ImageBase + 0x180, (uint)Witness(variant).Length, Witness(variant).Length);
        p.Syscall("main.exit", 231, 42, 0, 0, 0); p.JumpFailure();
        ulong mainEnd = ImageBase + (uint)p.Position;
        if (p.Position >= ChildOffset) throw new ToolFailure("disk_race_main_capacity");
        Writer c = new(elf, ChildOffset);
        c.Call("child.tid", 186, 0, 0, 0, 2);
        c.Call("child.pin", 10, ParentOutput, 4096, 1, -12);
        c.Call("child.disjoint-readonly", 10, ChildOutput, 4096, 1, 0);
        c.Call("child.disjoint-writable", 10, ChildOutput, 4096, 3, 0);
        if (close)
        {
            c.Call("child.close", 3, 3, 0, 0, 0);
            c.Call("child.reopen", 2, EmptyPath, 0, 0, 3, WaitPolicy.Disk);
            c.Call("child.empty", 17, 3, ChildOutput, 1, 0, WaitPolicy.Disk, 1, 0); c.Fill(ChildOutput, 4096, Sentinel);
            c.Call("child.notify", 1, 6, Payload, 4, 4);
        }
        else
        {
            c.Call("child.notify", 1, 6, Payload, 4, 4);
            c.Call("child.duplicate", 0, 7, ChildOutput, 8, 8, WaitPolicy.Disk, 8);
            c.MemoryByte(ChildOutput, Pattern(8192)); c.MemoryByte(ChildOutput + 7, Pattern(8199));
            c.Call("child.pread", 17, 4, ChildOutput, 512, 512, WaitPolicy.Disk, 512, 65535);
            c.MemoryByte(ChildOutput, Pattern(65535)); c.MemoryByte(ChildOutput + 511, Pattern(66046));
            c.Call("child.independent-cursor", 8, 4, 0, 1, 0, WaitPolicy.Disk);
        }
        c.Syscall("child.exit", 60, 0, 0, 0, 0); c.JumpFailure();
        ulong childEnd = ImageBase + (uint)c.Position;
        if (c.Position >= FailureOffset) throw new ToolFailure("disk_race_child_capacity");
        Writer f = new(elf, FailureOffset); f.Instruction(0xfc, 1); f.Syscall("failure", 231, 99, 0, 0, 0); f.Instruction(0xf4, 1);
        if (f.Calls[^1].ReturnRip != FailureExitRip) throw new ToolFailure("disk_race_failure_rip");
        return new(elf, p.Calls.ToArray(), c.Calls.ToArray(), p.Instructions.ToArray(),
            prefix.Concat(c.Instructions).Order().ToArray(), clone, mainEnd, childEnd);
    }

    private static byte Pattern(uint offset) => unchecked((byte)((offset * 73 + 19) ^ (offset >> 9) ^ (offset >> 17)));
    private static void Validate(string variant)
    { if (variant is not ("Progress" or "CloseReuse")) throw new ToolFailure("disk_race_variant"); }

    private sealed class Writer(byte[] bytes, int position)
    {
        public int Position = position;
        public readonly List<CallSpec> Calls = [];
        public readonly List<ulong> Instructions = [];
        private void Start() => Instructions.Add(ImageBase + (uint)Position);
        private void Op(ulong value, int count) { for (int i = 0; i < count; i++) bytes[Position++] = (byte)(value >> (i * 8)); }
        public void Instruction(ulong opcode, int count) { Start(); Op(opcode, count); }
        public void Immediate(uint register, uint value)
        { Start(); if (register >= 8) Op(0x41, 1); Op(0xb8 + (register & 7), 1); Op(value, 4); }
        private void Rip(ulong opcode, int count, ulong address)
        { Start(); Op(opcode, count); Op(unchecked((uint)((long)(address - ImageBase) - (Position + 4))), 4); }
        public void Branch(byte condition, int target)
        { Start(); Op(0x0f, 1); Op(condition, 1); Op(unchecked((uint)(target - (Position + 4))), 4); }
        public void JumpFailure() { Start(); Op(0xe9, 1); Op(unchecked((uint)(FailureOffset - (Position + 4))), 4); }
        public void Compare(long expected) { Start(); Op(0x3d48, 2); Op(unchecked((uint)expected), 4); Branch(0x85, FailureOffset); }
        public void MemoryByte(ulong address, byte value) { Rip(0x05b60f, 3, address); Compare(value); }
        public void MemoryDword(ulong address, uint value) { Rip(0x058b, 2, address); Compare(value); }
        public void MemoryQword(ulong address, uint value) { Rip(0x058b48, 3, address); Compare(value); }
        public void Fill(ulong address, uint count, byte value)
        { Immediate(0, value); Immediate(7, (uint)address); Immediate(1, count); Instruction(0xfc, 1); Instruction(0xaef3, 2); Branch(0x85, FailureOffset); }
        public ulong Syscall(string role, uint number, ulong a0, ulong a1, ulong a2, long result,
            WaitPolicy wait = WaitPolicy.None, uint output = 0, ulong a3 = 0, ulong a4 = 0)
        {
            Immediate(0, number); Immediate(7, checked((uint)a0)); Immediate(6, checked((uint)a1)); Immediate(2, checked((uint)a2));
            Immediate(10, checked((uint)a3)); Immediate(8, checked((uint)a4)); Instruction(0x050f, 2);
            ulong rip = ImageBase + (uint)Position;
            Calls.Add(new(role, number, a0, a1, a2, a3, a4, 0, rip, result, wait, output)); return rip;
        }
        public void Call(string role, uint number, ulong a0, ulong a1, ulong a2, long result,
            WaitPolicy wait = WaitPolicy.None, uint output = 0, ulong a3 = 0)
        { Syscall(role, number, a0, a1, a2, result, wait, output, a3); Compare(result); }
        public void Join()
        {
            Syscall("main.join", 202, ClearTid, 0, 2, 0, WaitPolicy.Join);
            Instruction(0xc08548, 3); int jump = Position; Branch(0x84, 0);
            Compare(-11); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(jump + 2), Position - (jump + 6));
        }
    }
}
