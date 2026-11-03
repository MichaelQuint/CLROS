using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;
/// <summary>Finite cooperative mechanical disk controls. Main parks before
/// the child runs; no CPL3 timer preemption is requested. Construction and the
/// copied instruction/call vectors prove bytes only. Original native, scheduler,
/// memory, device and runner joins remain separate obligations.</summary>

public static partial class LinuxDiskRaceProofFixture
{
    public const string AppPath = "apps/m16diskrace.elf";
    public static IReadOnlyList<string> OriginalVariants { get; } = Array.AsReadOnly<string>(["Progress", "CloseReuse"]);
    public static IReadOnlyList<string> LifecycleVariants { get; } = Array.AsReadOnly<string>(["InterruptWins", "CompletionWins", "ExitPending"]);
    public static IReadOnlyList<string> Variants { get; } = Array.AsReadOnly<string>(["Progress", "CloseReuse", "InterruptWins", "CompletionWins", "ExitPending"]);

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
    public const ulong SignalAction = 0x401680, SignalCount = 0x4016a0, SignalMask = 0x4016b0;
    public const int HandlerOffset = 0xc00, RestorerOffset = 0xe00;
    public static MemoryLayout Memory => new(ParentOutput, ParentOutputBytes, ChildOutput, ChildOutputBytes, Sentinel, ChildStackTop, ChildTls, ClearTid, ParentTid, PipePair, Payload, PipeOutput);

    public static string Witness(string variant)
    {
        Validate(variant);
        return variant switch
        {
            "Progress" => "M16_DISK_PROGRESS_OK!",
            "CloseReuse" => "M16_DISK_CLOSE_REUSE_OK!",
            "InterruptWins" => "M16_DISK_INTERRUPT_OK!",
            "CompletionWins" => "M16_DISK_EOF_WINNER_OK!",
            _ => "M16_DISK_EXIT_PENDING_OK!"
        };
    }

    public static byte[] CreateElf(string variant) => Build(variant).Elf;
    public static byte[] CreatePackage(string variant) => LinuxThreadProofFixture.CreateMechanicalPackage(CreateElf(variant), "DiskRace" + variant, AppPath, userInterrupts: false, socketListener: true);
    public static IReadOnlyList<CallSpec> Calls(string variant, bool child = false) => Array.AsReadOnly(child ? Build(variant).ChildCalls : Build(variant).MainCalls);
    public static IReadOnlyList<ulong> InstructionRips(string variant, bool child = false) => Array.AsReadOnly(child ? Build(variant).ChildInstructions : Build(variant).MainInstructions);
    public static ulong CloneReturnRip(string variant) => Build(variant).CloneCall;
    public static ulong MainEndRip(string variant) => Build(variant).MainEnd;
    public static ulong ChildEndRip(string variant) => Build(variant).ChildEnd;
    /// <summary>Actual main-thread handler/restorer calls and instruction starts.
    /// CompletionWins deliberately exercises zero-I/O EOF readiness, not backend
    /// data completion. ExitPending has no signal handler and cannot execute a
    /// user sentinel assertion after process death; final native drain is external.</summary>

    public static IReadOnlyList<CallSpec> HandlerCalls(string variant) => Array.AsReadOnly(Build(variant).HandlerCalls ?? []);
    public static IReadOnlyList<ulong> HandlerInstructionRips(string variant) => Array.AsReadOnly(Build(variant).HandlerInstructions ?? []);
    public static ulong HandlerEndRip(string variant) => Build(variant).HandlerEnd;
    public static ulong RestorerEndRip(string variant) => Build(variant).RestorerEnd;
    public static ulong FailureExitRip => ImageBase + FailureOffset + 35;

    /// <summary>Identify complete canonical bytes, never manifest args alone.
    /// Inputs are borrowed synchronously and not retained or mutated. Throws for
    /// unknown, changed or truncated packages; success grants no execution.</summary>

    public static string PackageVariant(ReadOnlySpan<byte> package)
    {
        foreach (string variant in Variants)
            if (package.SequenceEqual(CreatePackage(variant)))
                return variant;
        throw new ToolFailure("disk_race_package_identity");
    }

    private static Built Build(string variant)
    {
        if (LifecycleVariants.Contains(variant, StringComparer.Ordinal))
            return BuildLifecycle(variant);
        Validate(variant);
        bool close = variant == "CloseReuse";
        byte[] elf = new byte[ElfBytes];
        elf.AsSpan(EntryOffset, 4096 - EntryOffset).Fill(0xcc);
        "\u007fELF"u8.CopyTo(elf);
        elf[4] = 2;
        elf[5] = 1;
        elf[6] = 1;
        void U16(int at, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at), v);
        void U32(int at, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), v);
        void U64(int at, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), v);
        U16(16, 2);
        U16(18, 62);
        U32(20, 1);
        U64(24, ImageBase + EntryOffset);
        U64(32, 64);
        U16(52, 64);
        U16(54, 56);
        U16(56, 3);
        void Load(int at, uint type, uint flags, ulong offset, ulong address, ulong bytes, ulong alignment)
        {
            U32(at, type);
            U32(at + 4, flags);
            U64(at + 8, offset);
            U64(at + 16, address);
            U64(at + 24, address);
            U64(at + 32, bytes);
            U64(at + 40, bytes);
            U64(at + 48, alignment);
        }

        Load(64, 1, 5, 0, ImageBase, 4096, 4096);
        Load(120, 1, 6, 4096, ImageBase + 4096, 6 * 4096, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        void Text(ulong address, string text) => Encoding.ASCII.GetBytes(text).CopyTo(elf, checked((int)(address - ImageBase)));
        Text(Path, "/app/wwwroot/fixture/fragmented.bin\0");
        Text(EmptyPath, "/app/wwwroot/fixture/empty.bin\0");
        Text(ImageBase + 0x180, Witness(variant));
        U32((int)(ClearTid - ImageBase), 2);
        U32((int)(Payload - ImageBase), PipeWord);
        elf.AsSpan((int)(ParentOutput - ImageBase), (int)ParentOutputBytes).Fill(Sentinel);
        elf.AsSpan((int)(ChildOutput - ImageBase), (int)ChildOutputBytes).Fill(Sentinel);
        Writer p = new(elf, EntryOffset);
        p.Immediate(10, 0);
        p.Immediate(8, 0);
        p.Immediate(9, 0);
        p.Call("prep.original", 2, Path, 0, 0, 3, WaitPolicy.Disk);
        p.Call("prep.independent", 2, Path, 0, 0, 4, WaitPolicy.Disk);
        p.Call("prep.pipe", 293, PipePair, 0, 0, 0);
        p.MemoryDword(PipePair, 5);
        p.MemoryDword(PipePair + 4, 6);
        p.Call("prep.duplicate", 72, 3, 0, 7, 7);
        ulong clone = p.Syscall("prep.clone", 56, 0x7d0f00, ChildStackTop, ParentTid, 2, WaitPolicy.None, 0, ClearTid, ChildTls);
        int childPrefix = p.Instructions.Count;
        p.Immediate(10, 0);
        p.Immediate(8, 0);
        p.Instruction(0xc08548, 3);
        p.Branch(0x84, ChildOffset);
        p.Compare(2);
        ulong[] prefix = p.Instructions.Skip(childPrefix).Take(4).ToArray();
        p.MemoryDword(ParentTid, 2);
        p.Call("main.read", 0, 3, ParentOutput, 8192, close ? -9 : 8192, WaitPolicy.Disk, 8192);
        if (close)
            p.Fill(ParentOutput, 8192, Sentinel);
        else
        {
            p.MemoryByte(ParentOutput, Pattern(0));
            p.MemoryByte(ParentOutput + 8191, Pattern(8191));
        }

        p.Call("main.pipe", 0, 5, PipeOutput, 4, 4, WaitPolicy.PipeRead);
        p.MemoryDword(PipeOutput, PipeWord);
        if (!close)
        {
            p.Call("main.pread", 17, 4, ParentOutput, 16, 16, WaitPolicy.Disk, 16, 131000);
            p.MemoryByte(ParentOutput, Pattern(131000));
            p.MemoryByte(ParentOutput + 15, Pattern(131015));
        }

        p.Join();
        p.MemoryDword(ClearTid, 0);
        if (close)
        {
            p.Call("main.replacement", 17, 3, ParentOutput, 1, 0, WaitPolicy.Disk, 1, 0);
            p.Fill(ParentOutput, 8192, Sentinel);
            p.Call("main.duplicate", 0, 7, ChildOutput, 8, 8, WaitPolicy.Disk, 8);
            p.MemoryByte(ChildOutput, Pattern(0));
            p.MemoryByte(ChildOutput + 7, Pattern(7));
            p.Call("main.replacement-cursor", 8, 3, 0, 1, 0, WaitPolicy.Disk);
            p.Call("main.replacement-stat", 5, 3, Stat, 0, 0);
            p.MemoryQword(Stat + 48, 0);
        }

        p.Call("main.cursor", 8, 7, 0, 1, close ? 8 : 8200, WaitPolicy.Disk);
        p.Call("main.independent-cursor", 8, 4, 0, 1, 0, WaitPolicy.Disk);
        if (close)
            p.Fill(ParentOutput, 8192, Sentinel);
        foreach (uint fd in new uint[]
        {
            3,
            4,
            7,
            5,
            6
        }

        )
            p.Call("main.close" + fd, 3, fd, 0, 0, 0);
        p.Call("main.witness", 1, 1, ImageBase + 0x180, (uint)Witness(variant).Length, Witness(variant).Length);
        p.Syscall("main.exit", 231, 42, 0, 0, 0);
        p.JumpFailure();
        ulong mainEnd = ImageBase + (uint)p.Position;
        if (p.Position >= ChildOffset)
            throw new ToolFailure("disk_race_main_capacity");
        Writer c = new(elf, ChildOffset);
        c.Call("child.tid", 186, 0, 0, 0, 2);
        c.Call("child.pin", 10, ParentOutput, 4096, 1, -12);
        c.Call("child.disjoint-readonly", 10, ChildOutput, 4096, 1, 0);
        c.Call("child.disjoint-writable", 10, ChildOutput, 4096, 3, 0);
        if (close)
        {
            c.Call("child.close", 3, 3, 0, 0, 0);
            c.Call("child.reopen", 2, EmptyPath, 0, 0, 3, WaitPolicy.Disk);
            c.Call("child.empty", 17, 3, ChildOutput, 1, 0, WaitPolicy.Disk, 1, 0);
            c.Fill(ChildOutput, 4096, Sentinel);
            c.Call("child.notify", 1, 6, Payload, 4, 4);
        }
        else
        {
            c.Call("child.notify", 1, 6, Payload, 4, 4);
            c.Call("child.duplicate", 0, 7, ChildOutput, 8, 8, WaitPolicy.Disk, 8);
            c.MemoryByte(ChildOutput, Pattern(8192));
            c.MemoryByte(ChildOutput + 7, Pattern(8199));
            c.Call("child.pread", 17, 4, ChildOutput, 512, 512, WaitPolicy.Disk, 512, 65535);
            c.MemoryByte(ChildOutput, Pattern(65535));
            c.MemoryByte(ChildOutput + 511, Pattern(66046));
            c.Call("child.independent-cursor", 8, 4, 0, 1, 0, WaitPolicy.Disk);
        }

        c.Syscall("child.exit", 60, 0, 0, 0, 0);
        c.JumpFailure();
        ulong childEnd = ImageBase + (uint)c.Position;
        if (c.Position >= FailureOffset)
            throw new ToolFailure("disk_race_child_capacity");
        Writer f = new(elf, FailureOffset);
        f.Instruction(0xfc, 1);
        f.Syscall("failure", 231, 99, 0, 0, 0);
        f.Instruction(0xf4, 1);
        if (f.Calls[^1].ReturnRip != FailureExitRip)
            throw new ToolFailure("disk_race_failure_rip");
        return new(elf, p.Calls.ToArray(), c.Calls.ToArray(), p.Instructions.ToArray(), prefix.Concat(c.Instructions).Order().ToArray(), clone, mainEnd, childEnd);
    }

    private static Built BuildLifecycle(string variant)
    {
        bool interrupted = variant == "InterruptWins", exiting = variant == "ExitPending";
        byte[] elf = new byte[ElfBytes];
        elf.AsSpan(EntryOffset, 4096 - EntryOffset).Fill(0xcc);
        "\u007fELF"u8.CopyTo(elf);
        elf[4] = 2;
        elf[5] = 1;
        elf[6] = 1;
        void U16(int at, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at), v);
        void U32(int at, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), v);
        void U64(int at, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), v);
        U16(16, 2);
        U16(18, 62);
        U32(20, 1);
        U64(24, ImageBase + EntryOffset);
        U64(32, 64);
        U16(52, 64);
        U16(54, 56);
        U16(56, 3);
        void Load(int at, uint type, uint flags, ulong offset, ulong address, ulong bytes, ulong alignment)
        {
            U32(at, type);
            U32(at + 4, flags);
            U64(at + 8, offset);
            U64(at + 16, address);
            U64(at + 24, address);
            U64(at + 32, bytes);
            U64(at + 40, bytes);
            U64(at + 48, alignment);
        }

        Load(64, 1, 5, 0, ImageBase, 4096, 4096);
        Load(120, 1, 6, 4096, ImageBase + 4096, 6 * 4096, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        void Text(ulong address, string text) => Encoding.ASCII.GetBytes(text).CopyTo(elf, checked((int)(address - ImageBase)));
        Text(Path, "/app/wwwroot/fixture/fragmented.bin\0");
        Text(EmptyPath, "/app/wwwroot/fixture/empty.bin\0");
        Text(ImageBase + 0x180, Witness(variant));
        U32((int)(ClearTid - ImageBase), 2);
        U32((int)(Payload - ImageBase), PipeWord);
        elf.AsSpan((int)(ParentOutput - ImageBase), (int)ParentOutputBytes).Fill(Sentinel);
        elf.AsSpan((int)(ChildOutput - ImageBase), (int)ChildOutputBytes).Fill(Sentinel);
        if (!exiting)
        {
            U64((int)(SignalAction - ImageBase), ImageBase + HandlerOffset);
            U64((int)(SignalAction - ImageBase + 8), 0x04000004);
            U64((int)(SignalAction - ImageBase + 16), ImageBase + RestorerOffset);
        }

        Writer p = new(elf, EntryOffset);
        p.Immediate(10, 0);
        p.Immediate(8, 0);
        p.Immediate(9, 0);
        p.Call("prep.original", 2, Path, 0, 0, 3, WaitPolicy.Disk);
        p.Call("prep.independent", 2, interrupted || exiting ? Path : EmptyPath, 0, 0, 4, WaitPolicy.Disk);
        p.Call("prep.pipe", 293, PipePair, 0, 0, 0);
        p.MemoryDword(PipePair, 5);
        p.MemoryDword(PipePair + 4, 6);
        p.Call("prep.duplicate", 72, 3, 0, 7, 7);
        if (!exiting)
            p.Call("prep.signal", 13, 10, SignalAction, 0, 0, a3: 8);
        ulong clone = p.Syscall("prep.clone", 56, 0x7d0f00, ChildStackTop, ParentTid, 2, WaitPolicy.None, 0, ClearTid, ChildTls);
        int childPrefix = p.Instructions.Count;
        p.Immediate(10, 0);
        p.Immediate(8, 0);
        p.Instruction(0xc08548, 3);
        p.Branch(0x84, ChildOffset);
        p.Compare(2);
        ulong[] prefix = p.Instructions.Skip(childPrefix).Take(4).ToArray();
        p.MemoryDword(ParentTid, 2);
        long readResult = interrupted ? -4 : 0;
        ulong readRip = p.Syscall("main.read", 0, interrupted || exiting ? 3UL : 4UL, ParentOutput, 8192, readResult, WaitPolicy.Disk, 8192);
        if (exiting)
            p.JumpFailure(); // Any resumed target would invalidate ExitPending.
        else
        {
            p.Compare(readResult);
            p.Fill(ParentOutput, 8192, Sentinel);
            p.MemoryDword(SignalCount, 1);
            p.Join();
            p.MemoryDword(ClearTid, 0);
            p.Call("main.cursor", 8, 7, 0, 1, 0, WaitPolicy.Disk);
            // A real later read on the shared single-request device cannot finish
            // until any cancelled DMA orphan relinquished its lease. The parent
            // sentinel is checked again afterwards, before it is ever reused.
            p.Call("main.drain-probe", 17, 3, ChildOutput, 1, 1, WaitPolicy.Disk, 1, 0);
            p.MemoryByte(ChildOutput, Pattern(0));
            p.Fill(ParentOutput, 8192, Sentinel);
            p.Call("main.restored-mask", 14, 2, 0, SignalMask, 0, a3: 8);
            p.MemoryQword(SignalMask, 0);
            foreach (uint fd in new uint[]
            {
                3,
                4,
                7,
                5,
                6
            }

            )
                p.Call("main.close" + fd, 3, fd, 0, 0, 0);
            p.Call("main.witness", 1, 1, ImageBase + 0x180, (uint)Witness(variant).Length, Witness(variant).Length);
            p.Syscall("main.exit", 231, 42, 0, 0, 0);
            p.JumpFailure();
        }

        ulong mainEnd = ImageBase + (uint)p.Position;
        if (p.Position >= ChildOffset)
            throw new ToolFailure("disk_race_main_capacity");
        Writer c = new(elf, ChildOffset);
        if (exiting)
        {
            c.Fill(ParentOutput, 8192, Sentinel);
            // The comparison above reads the original sentinel. Exit is the
            // first child syscall: stdout would legitimately pump the read
            // before exit intent, defeating this outstanding-request control.
            c.Syscall("child.group-exit", 231, 42, 0, 0, 0);
            c.JumpFailure();
        }
        else
        {
            // First child syscall: no delays, repeated polls or synthetic gates.
            // The retained native pump is bounded to one disk step per turn.
            c.Call("child.signal", 234, 1, 1, 10, 0);
            c.Syscall("child.exit", 60, 0, 0, 0, 0);
            c.JumpFailure();
        }

        ulong childEnd = ImageBase + (uint)c.Position;
        if (c.Position >= HandlerOffset)
            throw new ToolFailure("disk_race_child_capacity");
        Writer h = new(elf, HandlerOffset), r = new(elf, RestorerOffset);
        if (!exiting)
        {
            h.Instruction(0xf88948, 3);
            h.Compare(10);
            h.Instruction(0x068b, 2);
            h.Compare(10);
            h.Instruction(0x08466348, 4);
            h.Compare(-6);
            h.Instruction(0x10468b, 3);
            h.Compare(1);
            h.Instruction(0x028b48, 3);
            h.Compare(6);
            h.Context(144, readResult);
            h.Context(168, (long)readRip);
            h.MemoryDword(SignalCount, 0);
            h.StoreDword(SignalCount, 1);
            h.Call("handler.tid", 186, 0, 0, 0, 1);
            h.Call("handler.mask", 14, 2, 0, SignalMask, 0, a3: 8);
            h.MemoryQword(SignalMask, 0x200);
            h.Instruction(0xc3, 1);
            r.Syscall("handler.return", 15, 0, 0, 0, readResult);
            r.Instruction(0x0b0f, 2);
            if (h.Position >= RestorerOffset || r.Position >= FailureOffset)
                throw new ToolFailure("disk_race_handler_capacity");
        }

        Writer f = new(elf, FailureOffset);
        f.Instruction(0xfc, 1);
        f.Syscall("failure", 231, 99, 0, 0, 0);
        f.Instruction(0xf4, 1);
        return new(elf, p.Calls.ToArray(), c.Calls.ToArray(), p.Instructions.ToArray(), prefix.Concat(c.Instructions).Order().ToArray(), clone, mainEnd, childEnd, h.Calls.Concat(r.Calls).ToArray(), h.Instructions.Concat(r.Instructions).ToArray(), exiting ? 0 : ImageBase + (uint)h.Position, exiting ? 0 : ImageBase + (uint)r.Position);
    }

    private static byte Pattern(uint offset) => unchecked((byte)((offset * 73 + 19) ^ (offset >> 9) ^ (offset >> 17)));
    private static void Validate(string variant)
    {
        if (!Variants.Contains(variant, StringComparer.Ordinal))
            throw new ToolFailure("disk_race_variant");
    }
}
