using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>C#-authored mechanical M16-008 parent/child ELF. One RX page and
/// six static RW pages hold separate user stacks/TLS, shared process data and
/// a disjoint pipe read buffer.
/// It requires real blocking pipes, per-thread masks, clone, futex completion,
/// clear-child-TID and a live timer. Construction is host evidence only; no
/// upstream application, native execution or signal delivery is qualified.</summary>
public static class LinuxThreadProofFixture
{
    public static IReadOnlyList<string> Variants { get; } = Array.AsReadOnly<string>(["Threads", "PipeReadClose", "PipeWriteClose", "PipeShutdown"]);
    public const string AppPath = "apps/m16threads.elf", ThreadWitness = "M16_THREADS_OK!";
    public const ulong ImageBase = 0x400000, DataBase = 0x401000;
    public const ulong ParentTls = 0x402000, ChildTls = 0x403000, ChangedChildTls = 0x403040;
    public const ulong ParentStackTop = 0x405000, ChildStackTop = 0x406000;
    /// <summary>Finite sequential child lifetimes within one two-thread process;
    /// each must exit/join/reap before the next real generation is created.</summary>
    public const int ReuseCycles = 4;
    public const int ElfBytes = 0x7000, EntryOffset = 0x200, ChildOffset = 0x900, FailureOffset = 0xf00, WitnessOffset = 0x180;
    public const ulong FailureExitRip = ImageBase + FailureOffset + 41;
    // Control-page offsets are public so the native owner can admit and inspect
    // the exact mechanical protocol without inventing fd/TID values.
    public const int PairOffset = 0x1000, ParentTidOffset = 0x1008, ClearTidOffset = 0x100c;
    public const int GateOffset = 0x1010, TimeoutWordOffset = 0x1014, ParentPidOffset = 0x1018, CloneTidOffset = 0x101c;
    public const int ParentMaskOffset = 0x1020, ChildMaskOffset = 0x1028, ParentMaskOut = 0x1030, ChildMaskOut = 0x1038;
    public const int ChildOldMask = 0x1040, ParentFsOut = 0x1048, ChildFsOut = 0x1050, TimeoutOffset = 0x1058;
    public const int PayloadOffset = 0x1070, ReadOffset = 0x1078;

    /// <summary>Actual syscall continuations on the selected assertion path,
    /// including exits. Threads expands its four child lifetimes and preserves
    /// repeated instruction RIPs; pipe controls have one child. Child inventory
    /// excludes the synthetic clone return and kernel-only cancelled exit;
    /// CloneReturnRip separately names the child's first saved continuation.
    /// Each call returns fresh immutable inventory storage; exit99 is excluded.</summary>
    public static IReadOnlyList<ulong> ExpectedReturnRips(string variant, bool child = false)
    { var built = Build(variant); return Array.AsReadOnly(child ? built.Child : built.Parent); }
    public static uint ExpectedCalls(string variant, bool child = false) => (uint)ExpectedReturnRips(variant, child).Count;
    public static ulong CloneReturnRip(string variant) => Build(variant).Clone;
    public static ulong SuccessRip(string variant) => Build(variant).Parent[^1];
    /// <summary>Actual user exit continuation; zero for PipeShutdown, whose
    /// parked child must exit through the kernel without user resumption.</summary>
    public static ulong ChildExitRip(string variant) => variant == "PipeShutdown" ? 0 : Build(variant).Child[^1];
    public static string Witness(string variant) { Validate(variant); return ThreadWitness; }

    /// <summary>Write this mechanical package to a fresh caller-selected path.
    /// CreateNew rejects existing files atomically; no existing bytes are changed.
    /// The output is a fixture only and establishes no execution evidence.</summary>
    public static void WritePackage(string path, string variant = "Threads")
    {
        byte[] package = CreatePackage(variant);
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(package);
        output.Flush(true);
    }

    /// <summary>Read the complete retained package input from its start and
    /// compare exact canonical bytes before any native build/launch. The caller
    /// keeps its read lock; success rewinds it for the original package consumer.
    /// Mismatches/truncation fail without changing input bytes or granting proof.</summary>
    public static void RequirePackage(Stream input)
    {
        if (!input.CanRead || !input.CanSeek || input.Position != 0 || input.Length is < ElfBytes or > ElfBytes + 4096)
            throw new ToolFailure("m16_008_package_identity");
        byte[] observed = new byte[checked((int)input.Length)];
        input.ReadExactly(observed);
        _ = PackageVariant(observed);
        input.Position = 0;
    }

    /// <summary>Identify only a complete byte-identical canonical mechanical
    /// package. Untrusted manifest names alone cannot select a validator.
    /// Unknown, altered and truncated input throws before native execution.</summary>
    public static string PackageVariant(ReadOnlySpan<byte> package)
    {
        foreach (string variant in Variants)
            if (package.SequenceEqual(CreatePackage(variant))) return variant;
        throw new ToolFailure("m16_008_package_identity");
    }

    /// <summary>Build a fresh deterministic package containing the complete ELF
    /// and its digest. No native source, executable or upstream payload is used.</summary>
    public static byte[] CreatePackage(string variant)
    {
        return CreateMechanicalPackage(CreateElf(variant), variant, AppPath);
    }

    // The two mechanical consumers share only fixed ELF/package geometry;
    // their instruction streams, protocol and acceptance remain independent.
    internal static byte[] CreateMechanicalPackage(byte[] elf, string variant, string appPath, bool userInterrupts = false, bool socketListener = false, uint descriptorCount = 16)
    {
        if (descriptorCount is not (16 or 32)) throw new ToolFailure("m16_008_fixture_descriptor_count");
        byte[] manifest = Encoding.ASCII.GetBytes("format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
            "app.path=" + appPath + "\nargs=" + variant + "\ndigest=" + Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant() +
            "\nmemory=67108864\nstack=65536\nthreads=2\ndescriptors=" + descriptorCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\nlisten=" + (socketListener ? "0.0.0.0" : "127.0.0.1") + ":8080\n" + (userInterrupts ? "env.CLROS_USER_IRQS=1\n" : ""));
        byte[] package = new byte[64 + manifest.Length + elf.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(package, LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), (uint)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length);
        manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length);
        if (!LinuxElfPackage.Validate(package).Succeeded) throw new ToolFailure("m16_008_fixture_package_invalid");
        return package;
    }

    /// <summary>Build a static non-PIE x64 ELF. Parent exit_group42 follows all
    /// checks. Threads joins four child exits; pipe controls exercise opposite
    /// peer close or cancellation of an actual parked read. Every
    /// assertion mismatch terminates the whole process with exit_group99.</summary>
    public static byte[] CreateElf(string variant) => Build(variant).Elf;

    private static (byte[] Elf, ulong[] Parent, ulong[] Child, ulong Clone) Build(string variant)
    {
        Validate(variant);
        byte[] elf = CreateMechanicalImage(ThreadWitness);
        void U32(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), value);
        void U64(int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), value);
        U32(ClearTidOffset, 1); U64(ParentMaskOffset, 0x200); U64(ChildMaskOffset, 0x800);
        U64(TimeoutOffset, 0); U64(TimeoutOffset + 8, 1_000_000); // Genuine relative 1ms, not a synthetic yield.
        "PIPE"u8.CopyTo(elf.AsSpan(PayloadOffset));
        U64(0x2000, 0x13579bdf); U64(0x2ff8, 0x5a5a5a5a); U64(0x3000, 0x2468ace0); U64(0x3040, 0x31415926);

        if (variant != "Threads") return BuildPipeControl(elf, variant);

        Writer p = new(elf, EntryOffset);
        p.Op(0xc031, 2); p.Op(0xc88c66, 3); p.Compare(0x3b); // Actual CPL3 CS.
        p.Immediate(4, (uint)ParentStackTop); p.Store32(0x4ff8, 0x13579bdf);
        p.Call(39, 0, 0, 0); p.Positive(); p.Op(0xc48941, 3); p.StoreRax(ParentPidOffset); // Live PID -> R12D.
        p.Call(186, 0, 0, 0); p.CompareR12();
        p.Call(158, 0x1002, ParentTls, 0); p.Compare(0);
        p.Call(14, 2, Address(ParentMaskOffset), 0, 8); p.Compare(0);
        p.Call(293, Address(PairOffset), 0, 0); p.Compare(0); // Blocking pipe, real returned fds.
        p.Call(56, 0x7d0f01, ChildStackTop, Address(ParentTidOffset), Address(ClearTidOffset), ChildTls); p.Compare(-22);
        p.Call(202, Address(GateOffset) + 1, 128, 0); p.Compare(-22);
        p.Call(14, 2, Address(ParentMaskOffset), 0, 16); p.Compare(-22);
        p.Call(158, 0x1002, 0x500000, 0); p.Compare(-14); // Unmapped FS must preserve the parent's existing TLS.
        p.Immediate(13, ReuseCycles); // R13 survives every real syscall, wait and child lifetime.
        int loop = p.Position;
        p.Store32(GateOffset, 0); p.Store32(ClearTidOffset, 1);
        p.Call(56, 0x7d0f00, ChildStackTop, Address(ParentTidOffset), Address(ClearTidOffset), ChildTls);
        ulong clone = p.Rips[^1]; p.Op(0xc08548, 3); p.Branch(0x84, ChildOffset);
        p.Positive(); p.CompareR12(0x84); p.StoreRax(CloneTidOffset); p.CompareMemory(ParentTidOffset);
        p.Memory(ClearTidOffset, 1);
        p.FdCall(0, PairOffset, Address(ReadOffset), 4); p.Compare(4); p.Memory(ReadOffset, 0x45504950);
        p.Call(56, 0x7d0f00, ChildStackTop, Address(ParentTidOffset), Address(ClearTidOffset), ChildTls); p.Compare(-11);
        p.Memory(ClearTidOffset, 1); // Full table cannot publish or clear the already parked child.
        p.Call(39, 0, 0, 0); p.CompareR12(); p.Memory(0x4ff8, 0x13579bdf);
        p.Call(158, 0x1003, Address(ParentFsOut), 0); p.Compare(0); p.Memory(ParentFsOut, ParentTls, true);
        p.Fs(); p.Compare(0x13579bdf);
        p.Call(14, 2, 0, Address(ParentMaskOut), 8); p.Compare(0); p.Memory(ParentMaskOut, 0x200, true);
        p.Store32(GateOffset, 1);
        p.Call(202, Address(GateOffset), 129, 1); p.Compare(1); // Child already parked on its private gate.
        p.FdCall(0, PairOffset, 0x406000, 4096); p.Compare(4096); // Empty pipe: child must run and fill its entire capacity.
        p.Memory(0x6000, 0x13579bdf, true); p.Memory(0x6ff8, 0x5a5a5a5a, true);
        p.Call(202, Address(ClearTidOffset), 0, 1); p.Compare(0); // Shared key used by actual child exit.
        p.Memory(ClearTidOffset, 0);
        p.FdCall(0, PairOffset, Address(ReadOffset), 4); p.Compare(4); p.Memory(ReadOffset, 0x2468ace0);
        p.Op(0xcdff49, 3); p.Branch(0x85, loop); // DEC R13; JNZ only after the genuine clear-TID join.
        p.Call(202, Address(GateOffset), 128, 0); p.Compare(-11); // Wrong current word never parks.
        p.Call(202, Address(TimeoutWordOffset), 128, 0, Address(TimeoutOffset)); p.Compare(-110);
        p.FdCall(3, PairOffset, 0, 0); p.Compare(0); p.FdCall(3, PairOffset + 4, 0, 0); p.Compare(0);
        p.Call(1, 1, ImageBase + WitnessOffset, (uint)ThreadWitness.Length); p.Compare(ThreadWitness.Length);
        p.Call(231, 42, 0, 0); p.JumpFailure();
        if (p.Position > ChildOffset) throw new ToolFailure("m16_008_fixture_parent_capacity");

        Writer c = new(elf, ChildOffset);
        c.Compare(0); c.Op(0xe08948, 3); c.Compare((long)ChildStackTop); c.Store32(0x5ff8, 0x2468ace0);
        c.Call(39, 0, 0, 0); c.CompareR12(); c.CompareMemory(ParentPidOffset); c.Op(0xd4f741, 3); // Change only child's inherited R12D.
        c.Call(186, 0, 0, 0); c.Positive(); c.CompareMemory(ParentTidOffset); c.CompareMemory(CloneTidOffset);
        c.Call(158, 0x1003, Address(ChildFsOut), 0); c.Compare(0); c.Memory(ChildFsOut, ChildTls, true); c.Fs(); c.Compare(0x2468ace0);
        c.Call(14, 2, 0, Address(ChildMaskOut), 8); c.Compare(0); c.Memory(ChildMaskOut, 0x200, true);
        c.Call(14, 2, Address(ChildMaskOffset), Address(ChildOldMask), 8); c.Compare(0); c.Memory(ChildOldMask, 0x200, true);
        c.Call(14, 2, 0, Address(ChildMaskOut), 8); c.Compare(0); c.Memory(ChildMaskOut, 0x800, true);
        c.Call(158, 0x1002, ChangedChildTls, 0); c.Compare(0);
        c.Call(158, 0x1003, Address(ChildFsOut), 0); c.Compare(0); c.Memory(ChildFsOut, ChangedChildTls, true); c.Fs(); c.Compare(0x31415926);
        c.FdCall(1, PairOffset + 4, Address(PayloadOffset), 4); c.Compare(4);
        c.Call(202, Address(GateOffset), 128, 0); c.Compare(0); c.Memory(GateOffset, 1); c.Memory(0x5ff8, 0x2468ace0);
        c.FdCall(1, PairOffset + 4, ParentTls, 4100); c.Compare(4100); // Retain 4096-byte progress while blocked; complete the final four bytes once.
        c.Call(60, 0, 0, 0); c.JumpFailure();
        if (c.Position > FailureOffset) throw new ToolFailure("m16_008_fixture_child_capacity");
        WriteFailure(elf);
        ulong[] parent = [.. p.Rips.Take(9), .. Enumerable.Range(0, ReuseCycles).SelectMany(_ => p.Rips.Skip(9).Take(10)), .. p.Rips.Skip(19)];
        ulong[] children = Enumerable.Range(0, ReuseCycles).SelectMany(_ => c.Rips).ToArray();
        return (elf, parent, children, clone);
    }

    private static (byte[] Elf, ulong[] Parent, ulong[] Child, ulong Clone) BuildPipeControl(byte[] elf, string variant)
    {
        bool write = variant == "PipeWriteClose", shutdown = variant == "PipeShutdown";
        Writer p = new(elf, EntryOffset);
        p.Op(0xc031, 2); p.Op(0xc88c66, 3); p.Compare(0x3b);
        p.Immediate(4, (uint)ParentStackTop); p.Store32(0x4ff8, 0x13579bdf);
        p.Call(39, 0, 0, 0); p.Positive(); p.Op(0xc48941, 3); p.StoreRax(ParentPidOffset);
        p.Call(186, 0, 0, 0); p.CompareR12();
        p.Call(158, 0x1002, ParentTls, 0); p.Compare(0);
        p.Call(14, 2, Address(ParentMaskOffset), 0, 8); p.Compare(0);
        p.Call(293, Address(PairOffset), 0, 0); p.Compare(0);
        p.FdCall(write || shutdown ? 0U : 1U, PairOffset + (write || shutdown ? 0 : 4), 0, 0); p.Compare(0);
        if (write) { p.FdCall(1, PairOffset + 4, ParentTls, 4096); p.Compare(4096); }
        p.Call(56, 0x7d0f00, ChildStackTop, Address(ParentTidOffset), Address(ClearTidOffset), ChildTls);
        ulong clone = p.Rips[^1]; p.Op(0xc08548, 3); p.Branch(0x84, ChildOffset);
        p.Positive(); p.CompareR12(0x84); p.StoreRax(CloneTidOffset); p.CompareMemory(ParentTidOffset); p.Memory(ClearTidOffset, 1);
        if (shutdown)
        { p.Call(202, Address(GateOffset), 128, 0, Address(TimeoutOffset)); p.Compare(-110); }
        else
        {
            p.FdCall(write ? 1U : 0U, PairOffset + (write ? 4 : 0), Address(ReadOffset), 4); p.Compare(write ? -32 : 0);
            p.Memory(ClearTidOffset, 0);
            p.FdCall(3, PairOffset + (write ? 4 : 0), 0, 0); p.Compare(0);
        }
        p.Memory(0x4ff8, 0x13579bdf); p.Fs(); p.Compare(0x13579bdf);
        p.Call(1, 1, ImageBase + WitnessOffset, (uint)ThreadWitness.Length); p.Compare(ThreadWitness.Length);
        p.Call(231, 42, 0, 0); p.JumpFailure();
        if (p.Position > ChildOffset) throw new ToolFailure("m16_008_fixture_parent_capacity");

        Writer c = new(elf, ChildOffset);
        c.Compare(0); c.Op(0xe08948, 3); c.Compare((long)ChildStackTop);
        c.Fs(); c.Compare(0x2468ace0);
        if (shutdown) c.FdCall(0, PairOffset, Address(ReadOffset), 4);
        else
        {
            c.FdCall(3, PairOffset + (write ? 0 : 4), 0, 0); c.Compare(0);
            c.Call(60, 0, 0, 0);
        }
        c.JumpFailure(); // Shutdown must drain the parked child without another user return.
        if (c.Position > FailureOffset) throw new ToolFailure("m16_008_fixture_child_capacity");
        WriteFailure(elf);
        return (elf, p.Rips.ToArray(), c.Rips.ToArray(), clone);
    }

    private static void WriteFailure(byte[] elf)
    {
        Writer failure = new(elf, FailureOffset); failure.Op(0xfc, 1); failure.Call(231, 99, 0, 0); failure.Op(0xf4, 1);
        if (failure.Rips[0] != FailureExitRip) throw new ToolFailure("m16_008_fixture_failure_rip");
    }

    internal static byte[] CreateMechanicalImage(string witness)
    {
        byte[] elf = new byte[ElfBytes]; elf.AsSpan(EntryOffset, 4096 - EntryOffset).Fill(0xcc);
        "\u007fELF"u8.CopyTo(elf); elf[4] = 2; elf[5] = 1; elf[6] = 1;
        void U16(int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(at), value);
        void U32(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(at), value);
        void U64(int at, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(at), value);
        U16(16, 2); U16(18, 62); U32(20, 1); U64(24, ImageBase + EntryOffset);
        U64(32, 64); U16(52, 64); U16(54, 56); U16(56, 3);
        void Load(int at, uint type, uint flags, ulong offset, ulong address, ulong bytes, ulong alignment)
        { U32(at, type); U32(at + 4, flags); U64(at + 8, offset); U64(at + 16, address); U64(at + 24, address); U64(at + 32, bytes); U64(at + 40, bytes); U64(at + 48, alignment); }
        Load(64, 1, 5, 0, ImageBase, 4096, 4096); Load(120, 1, 6, 4096, DataBase, ElfBytes - 4096, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        Encoding.ASCII.GetBytes(witness).CopyTo(elf, WitnessOffset);
        return elf;
    }

    private static ulong Address(int offset) => ImageBase + (uint)offset;
    private static void Validate(string variant)
    { if (variant is not ("Threads" or "PipeReadClose" or "PipeWriteClose" or "PipeShutdown")) throw new ToolFailure("m16_008_fixture_variant"); }

    // All syscall arguments are explicit. Only pipe fd arguments are replaced
    // with actual pair loads; no allocation order or initial TID is assumed.
    internal sealed class Writer(byte[] bytes, int position)
    {
        public int Position = position;
        public readonly List<ulong> Rips = [];
        public void Op(ulong value, int count) { for (int i = 0; i < count; i++) bytes[Position++] = (byte)(value >> (i * 8)); }
        public void Immediate(uint register, uint value)
        { if (register >= 8) Op(0x41, 1); Op(0xb8 + (register & 7), 1); Op(value, 4); }
        internal void Rip(ulong op, int length, int target, int tail = 0)
        { Op(op, length); Op(unchecked((uint)(target - (Position + 4 + tail))), 4); }
        public void Branch(byte condition, int target = FailureOffset) { Op(0x0f, 1); Op(condition, 1); Op(unchecked((uint)(target - (Position + 4))), 4); }
        public void JumpFailure() { Op(0xe9, 1); Op(unchecked((uint)(FailureOffset - (Position + 4))), 4); }
        public void Compare(long value) { Op(0x3d48, 2); Op(unchecked((uint)value), 4); Branch(0x85); }
        public void Positive() { Op(0xc08548, 3); Branch(0x8e); }
        public void CompareR12(byte condition = 0x85) { Op(0xe0394c, 3); Branch(condition); }
        public void StoreRax(int offset) => Rip(0x0589, 2, offset);
        public void CompareMemory(int offset) { Rip(0x053b, 2, offset); Branch(0x85); }
        public void Store32(int offset, uint value) { Rip(0x05c7, 2, offset, 4); Op(value, 4); }
        public void Memory(int offset, ulong value, bool qword = false)
        { Rip(qword ? 0x058b48UL : 0x058bUL, qword ? 3 : 2, offset); Compare((long)value); }
        public void Fs() { Op(0x64, 1); Op(0x25048b48, 8); } // MOV RAX,QWORD PTR FS:[0].
        public void Call(uint number, ulong a0, ulong a1, ulong a2, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        { Arguments(number, a0, a1, a2, a3, a4, a5); Finish(); }
        public void FdCall(uint number, int fdOffset, ulong address, uint count)
        { Arguments(number, 0, address, count, 0, 0, 0); Rip(0x3d8b, 2, fdOffset); Finish(); }
        internal void Arguments(uint number, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4, ulong a5)
        {
            Immediate(0, number); Immediate(7, checked((uint)a0)); Immediate(6, checked((uint)a1)); Immediate(2, checked((uint)a2));
            Immediate(10, checked((uint)a3)); Immediate(8, checked((uint)a4)); Immediate(9, checked((uint)a5));
        }
        internal void Finish() { Op(0x050f, 2); Rips.Add(ImageBase + (uint)Position); }
    }
}
