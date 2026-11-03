using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;
/// <summary>
/// Mechanical M16-007 Linux descriptor witnesses authored entirely in C#.
/// The owner exposes the complete original ELF as the immutable selected file,
/// admits sixteen descriptors, two 4KiB pipes and 8KiB scratch, and explicitly
/// ignores SIGPIPE for this fixture. These bounded CPL3 checks do not qualify
/// an upstream application, blocking waits, threads or signal delivery.
/// </summary>

public static partial class LinuxDescriptorProofFixture
{
    public static IReadOnlyList<string> Variants { get; } = Array.AsReadOnly<string>(["Descriptors", "Pipes"]);

    public const string AppPath = "apps/m16descriptors.elf", SelectedPath = "/app/apps/m16descriptors.elf";
    public const string DescriptorWitness = "M16_DESCRIPTORS_OK!", PipeWitness = "M16_PIPES_OK!";
    public const string FirstOutput = "M16_FD1_OWNED!", SecondOutput = "M16_FD2_OWNED!", VectorOutput = "M16_WRITEV_OK!";
    public const ulong ImageBase = 0x400000, DataBase = 0x401000, OutputAddress = 0x402000;
    public const int ElfBytes = 12288, EntryOffset = 0x200, FailureOffset = 0xf00, WitnessOffset = 0x180;
    public const uint DescriptorCalls = 52, PipeCalls = 57;
    public const ulong FailureExitRip = ImageBase + FailureOffset + 23;
    private const uint NonBlock = 0x800, CloseOnExec = 0x80000;
    private const int PackagePath = 0x1000, MemInfoPath = 0x1040, MapsPath = 0x1060, AbsentPath = 0x1080, TraversalPath = 0x10a0;
    private const int FirstText = 0x1100, SecondText = 0x1120, VectorText = 0x1140, Vectors = 0x1180, BadVectors = 0x11c0;
    private const int MapsPrefix = 0x1200, StatOffset = 0x2100, PairOffset = 0x2ff0;
    /// <summary>Return the exact successful syscall count; unknown variants fail.</summary>

    public static uint ExpectedCalls(string variant) => variant switch
    {
        "Descriptors" => DescriptorCalls,
        "Pipes" => PipeCalls,
        _ => throw new ToolFailure("m16_007_fixture_variant")};
    /// <summary>Fresh successful-path return RIPs, including the terminating exit.
    /// The failure block is excluded so transcript owners cannot accept exit 99.</summary>

    public static IReadOnlyList<ulong> ExpectedReturnRips(string variant) => Array.AsReadOnly(Build(variant).Rips);
    public static ulong SuccessRip(string variant) => Build(variant).Rips[^1];
    public static string Witness(string variant) => variant switch
    {
        "Descriptors" => DescriptorWitness,
        "Pipes" => PipeWitness,
        _ => throw new ToolFailure("m16_007_fixture_variant")};
    /// <summary>Build a fresh canonical immutable package with the full ELF digest.
    /// The caller owns the returned bytes; no native source or host executable is used.</summary>

    public static byte[] CreatePackage(string variant)
    {
        byte[] elf = CreateElf(variant);
        byte[] manifest = Encoding.ASCII.GetBytes("format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" + "app.path=" + AppPath + "\nargs=" + variant + "\ndigest=" + Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant() + "\nmemory=67108864\nstack=65536\nthreads=1\ndescriptors=16\nlisten=127.0.0.1:8080\n");
        byte[] package = new byte[64 + manifest.Length + elf.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(package, LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), (uint)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length);
        manifest.CopyTo(package, 64);
        elf.CopyTo(package, 64 + manifest.Length);
        if (!LinuxElfPackage.Validate(package).Succeeded)
            throw new ToolFailure("m16_007_fixture_package_invalid");
        return package;
    }

    /// <summary>Build a fresh static non-PIE x64 ELF. Every successful variant
    /// terminates with exit 42; every assertion mismatch branches to exit 99.</summary>

    public static byte[] CreateElf(string variant) => Build(variant).Elf;
    private static (byte[] Elf, ulong[] Rips) Build(string variant)
    {
        uint expectedCalls = ExpectedCalls(variant);
        byte[] elf = new byte[ElfBytes];
        elf.AsSpan(EntryOffset, 4096 - EntryOffset).Fill(0xcc);
        "\u007fELF"u8.CopyTo(elf);
        elf[4] = 2;
        elf[5] = 1;
        elf[6] = 1;
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(offset), value);
        void U64(int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(offset), value);
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
        Load(120, 1, 6, 4096, DataBase, 8192, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        Encoding.ASCII.GetBytes(Witness(variant)).CopyTo(elf, WitnessOffset);
        if (variant == "Descriptors")
        {
            void Text(int at, string value) => Encoding.ASCII.GetBytes(value).CopyTo(elf, at);
            Text(PackagePath, SelectedPath + "\0");
            Text(MemInfoPath, "/proc/meminfo\0");
            Text(MapsPath, "/proc/self/maps\0");
            Text(AbsentPath, "/app/missing\0");
            Text(TraversalPath, "/app/../apps/m16descriptors.elf\0");
            Text(FirstText, FirstOutput);
            Text(SecondText, SecondOutput);
            Text(VectorText, VectorOutput);
            U64(Vectors, ImageBase + VectorText);
            U64(Vectors + 8, 5);
            U64(Vectors + 16, ImageBase + VectorText + 5);
            U64(Vectors + 24, (ulong)VectorOutput.Length - 5);
            U64(BadVectors, 0x403000);
            U64(BadVectors + 8, 1);
            Text(MapsPrefix, "00400000-00401000 r-xp");
            elf.AsSpan(0x2000, 4096).Fill(0xa5);
        }
        else
        {
            for (int i = 0; i < 5000; i++)
                elf[0x1000 + i] = (byte)(i * 29 + (i >> 8) * 13 + 7);
            U64(0x1a0, DataBase);
            U64(0x1a8, 2);
            U64(0x1b0, DataBase + 2);
            U64(0x1b8, 2);
        }

        Writer w = new(elf, EntryOffset);
        w.Op(0xc031, 2);
        w.Op(0xc88c66, 3);
        w.CompareRax(0x3b); // Actual CPL3 CS, before any syscall.
        w.Immediate(10, 0);
        w.Immediate(8, 0);
        w.Immediate(9, 0);
        if (variant == "Descriptors")
            Descriptors(w);
        else
            Pipes(w);
        w.Syscall(1, 1, ImageBase + WitnessOffset, (uint)Witness(variant).Length);
        w.CompareRax(Witness(variant).Length);
        w.Syscall(231, 42, 0, 0);
        w.JumpFailure();
        if (w.Rips.Count != expectedCalls || w.Rips.Count > 64)
            throw new ToolFailure("m16_007_fixture_call_inventory");
        if (w.Position >= FailureOffset)
            throw new ToolFailure("m16_007_fixture_code_capacity");
        ulong[] rips = w.Rips.ToArray();
        w.Position = FailureOffset;
        w.Op(0xfc, 1);
        w.Syscall(231, 99, 0, 0);
        w.Op(0xf4, 1);
        if (w.Rips[^1] != FailureExitRip)
            throw new ToolFailure("m16_007_fixture_failure_rip");
        return (elf, rips);
    }

    private static void Descriptors(Writer w)
    {
        w.Call(0, 0, OutputAddress, 1, 0);
        w.MemoryByte(0x2000, 0xa5);
        w.Call(1, 1, ImageBase + FirstText, (uint)FirstOutput.Length, FirstOutput.Length);
        w.Call(1, 2, ImageBase + SecondText, (uint)SecondOutput.Length, SecondOutput.Length);
        w.Call(20, 1, ImageBase + Vectors, 2, VectorOutput.Length);
        w.Call(16, 1, 0x541b, OutputAddress, -25);
        w.Call(257, 0xffff_ff9c, ImageBase + PackagePath, 0, 3); // openat(AT_FDCWD), Linux's signed int dirfd ABI.
        w.Call(5, 3, ImageBase + StatOffset, 0, 0);
        w.MemoryDword(StatOffset + 24, 0x8124);
        w.MemoryQword(StatOffset + 48, ElfBytes);
        w.Call(0, 3, OutputAddress, 4, 4);
        w.MemoryDword(0x2000, 0x464c457f);
        w.Syscall(17, 3, OutputAddress + 8, 4, 0);
        w.CompareRax(4);
        w.MemoryDword(0x2008, 0x464c457f);
        w.Call(72, 3, 0, 6, 6); // F_DUPFD; the cursor is shared with fd6.
        w.Call(0, 6, OutputAddress, 4, 4);
        w.MemoryDword(0x2000, 0x00010102);
        w.Call(8, 3, 0, 1, 8); // pread did not move position; duplicate read did.
        // The loaded user output already differs from its original ELF page.
        // The selected immutable file must still expose the original payload.
        w.Syscall(17, 3, OutputAddress, 1, 0x2000);
        w.CompareRax(1);
        w.MemoryByte(0x2000, 0xa5);
        w.Call(72, 6, 4, NonBlock, 0);
        w.Call(72, 3, 3, 0, NonBlock | 0x8000); // Linux x64 forces O_LARGEFILE.
        w.Call(72, 6, 2, 1, 0);
        w.Call(72, 3, 1, 0, 0);
        w.Call(72, 6, 1, 0, 1);
        w.Call(8, 6, 0, 2, ElfBytes);
        w.Call(0, 3, OutputAddress, 4, 0);
        w.Call(3, 3, 0, 0, 0);
        w.Call(0, 3, OutputAddress, 4, -9);
        w.Call(2, ImageBase + AbsentPath, 0, 0, -2);
        w.Call(2, ImageBase + PackagePath, 1, 0, -30);
        w.Call(257, 0xffff_ff9c, ImageBase + TraversalPath, 0, -13);
        w.Call(20, 1, 0, 1, -14);
        w.Call(20, 1, ImageBase + BadVectors, 1, -14);
        w.Call(20, 1, ImageBase + Vectors, 17, -22);
        w.Call(257, 0xffff_ff9c, ImageBase + PackagePath, CloseOnExec, 3);
        w.Call(72, 3, 1, 0, 1);
        w.Call(0, 3, OutputAddress, 4, 4);
        w.MemoryDword(0x2000, 0x464c457f);
        w.Call(0, 6, OutputAddress, 4, 0); // Reused fd3 has a distinct fresh description.
        w.Call(72, 3, 1030, 7, 7);
        w.Call(72, 7, 1, 0, 1);
        w.Call(72, 7, 3, 0, 0x8000);
        w.Call(3, 7, 0, 0, 0);
        w.Call(3, 3, 0, 0, 0);
        w.Call(3, 6, 0, 0, 0);
        w.Call(2, ImageBase + MemInfoPath, 0, 0, 3);
        w.Syscall(0, 3, OutputAddress, 128);
        w.PositiveRax();
        w.MemoryQword(0x2000, 0x6c61746f546d654d); // MemTotal
        w.Call(3, 3, 0, 0, 0);
        w.Call(2, ImageBase + MapsPath, 0, 0, 3);
        w.Syscall(0, 3, OutputAddress, 128);
        w.PositiveRax();
        w.CompareBytes(OutputAddress, ImageBase + MapsPrefix, 22);
        w.Call(3, 3, 0, 0, 0);
        w.Call(293, ImageBase + PairOffset, NonBlock | CloseOnExec, 0, 0);
        w.Pair(3, 4);
        w.Call(1, 4, ImageBase + FirstText, 4, 4);
        w.Call(0, 3, OutputAddress, 4, 4);
        w.CompareBytes(OutputAddress, ImageBase + FirstText, 4);
        w.Call(3, 4, 0, 0, 0);
        w.Call(0, 3, OutputAddress, 1, 0);
        w.Call(3, 3, 0, 0, 0);
    }

    private static void Pipes(Writer w)
    {
        const ulong source = DataBase;
        w.Call(293, ImageBase + PairOffset, NonBlock | CloseOnExec, 0, 0);
        w.Pair(3, 4);
        w.Call(0, 3, OutputAddress, 1, -11);
        w.Call(72, 4, 0, 5, 5);
        w.Call(3, 4, 0, 0, 0);
        w.Call(1, 5, source, 4, 4);
        w.Call(0, 3, OutputAddress, 2, 2);
        w.CompareBytes(OutputAddress, source, 2);
        w.Call(3, 5, 0, 0, 0);
        w.Call(0, 3, OutputAddress, 8, 2);
        w.CompareBytes(OutputAddress, source + 2, 2);
        w.Call(0, 3, OutputAddress, 1, 0);
        w.Call(1, 5, source, 1, -9);
        w.Call(3, 3, 0, 0, 0);
        w.Call(293, ImageBase + PairOffset, NonBlock, 0, 0);
        w.Pair(3, 4);
        w.Call(1, 4, source, 4096, 4096);
        w.Call(1, 4, source, 1, -11);
        w.Call(1, 4, source, 5000, -11);
        w.Call(0, 3, OutputAddress, 1024, 1024);
        w.CompareBytes(OutputAddress, source, 1024);
        w.Call(1, 4, source, 1025, -11);
        w.Call(1, 4, source, 5000, 1024);
        w.Call(0, 3, OutputAddress, 4096, 4096);
        w.CompareBytes(OutputAddress, source + 1024, 3072);
        w.CompareBytes(OutputAddress + 3072, source, 1024);
        w.Call(0, 3, OutputAddress, 1, -11);
        w.Call(72, 3, 0, 5, 5);
        w.Call(3, 3, 0, 0, 0);
        w.Call(1, 4, source, 4, 4);
        w.Call(3, 5, 0, 0, 0);
        w.Call(1, 4, source, 1, -32);
        w.Call(3, 4, 0, 0, 0);
        w.Call(293, ImageBase + PairOffset, 0, 0, 0);
        w.Pair(3, 4);
        w.Call(0, 3, OutputAddress, 0, 0);
        w.Call(1, 4, source, 0, 0);
        w.Call(0, 3, OutputAddress, 1, -38);
        w.Call(1, 4, source, 1, -38);
        w.Call(3, 3, 0, 0, 0);
        w.Call(1, 4, source, 0, 0);
        w.Call(1, 4, source, 1, -38);
        w.Call(3, 4, 0, 0, 0);
        w.Call(293, ImageBase + PairOffset, NonBlock, 0, 0);
        w.Pair(3, 4);
        w.Call(293, ImageBase + PairOffset, NonBlock, 0, 0);
        w.Pair(5, 6);
        w.Call(293, ImageBase + PairOffset, NonBlock, 0, -23); // Both admitted pipe owners are occupied.
        w.Call(1, 4, source, 4, 4);
        w.Call(0, 5, OutputAddress, 1, -11);
        w.Call(1, 6, source + 4, 4, 4);
        w.Call(0, 3, OutputAddress, 4, 4);
        w.CompareBytes(OutputAddress, source, 4);
        w.Call(0, 5, OutputAddress, 4, 4);
        w.CompareBytes(OutputAddress, source + 4, 4);
        w.Call(3, 3, 0, 0, 0);
        w.Call(3, 4, 0, 0, 0);
        w.Call(3, 5, 0, 0, 0);
        w.Call(3, 6, 0, 0, 0);
        w.Call(293, ImageBase + PairOffset, 1, 0, -22);
        w.Call(293, 0, NonBlock, 0, -14);
        w.Call(293, 0x402fff, NonBlock, 0, -14); // Complete output range must fit before ownership changes.
        w.Call(293, ImageBase + PairOffset, NonBlock, 0, 0);
        w.Pair(3, 4);
        w.Call(20, 4, ImageBase + 0x1a0, 2, 4);
        w.Call(0, 3, OutputAddress, 4, 4);
        w.CompareBytes(OutputAddress, source, 4);
        w.Call(3, 3, 0, 0, 0);
        w.Call(3, 4, 0, 0, 0);
    }
}
