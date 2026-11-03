using System.Buffers.Binary;
using System.Text;

namespace OS.Tooling;
/// <summary>C#-authored mechanical read-only FAT file exercise. Construction
/// alone proves no native instruction, device, scheduler or file execution.
/// The owning guest runner separately authenticates this exact package.</summary>

public static partial class LinuxDiskFileProofFixture
{
    public const string AppPath = "apps/m16diskfiles.elf", Variant = "DiskFiles";
    public const string Witness = "M16_DISK_FILES_OK!";
    public const ulong ImageBase = 0x400000, DataBase = 0x401000;
    public const int ElfBytes = 0x7000, EntryOffset = 0x200, FailureOffset = 0xf00, WitnessOffset = 0x180;
    private const int PathOffset = 0x1000, MissingOffset = 0x1060, OutsideOffset = 0x10c0;
    private const int NotDirectoryOffset = 0x1140, EmptyOffset = 0x11c0, DirectoryOffset = 0x1220;
    private const int StatOffset = 0x3000, OutputOffset = 0x4000;
    private const ulong Output = ImageBase + OutputOffset;
    public static byte[] CreatePackage() => LinuxThreadProofFixture.CreateMechanicalPackage(CreateElf(), Variant, AppPath, userInterrupts: true, socketListener: true);
    public static byte[] CreateElf() => Build().Elf;
    public static IReadOnlyList<ulong> ExpectedReturnRips() => Array.AsReadOnly(Build().Rips);
    /// <summary>Exact sorted instruction starts emitted for the active main
    /// path, including the jump after its exit syscall. Failure-handler and
    /// padding bytes are excluded. This copied readonly inventory supplies only
    /// byte boundaries; native/source/IRQ execution require independent joins.</summary>

    public static IReadOnlyList<ulong> MainInstructionRips() => Array.AsReadOnly(Build().Instructions);
    public static uint ExpectedCalls => (uint)Build().Rips.Length;
    public static ulong SuccessRip => Build().Rips[^1];
    public static ulong FailureExitRip => ImageBase + FailureOffset + 23;

    public static void RequirePackage(ReadOnlySpan<byte> package)
    {
        if (!package.SequenceEqual(CreatePackage()))
            throw new ToolFailure("m16_025_fixture_package_identity");
    }

    /// <summary>Write one fresh fixture below artifacts and verify its complete
    /// bytes while retained. Existing paths, reparse traversal and aliases reject;
    /// the output supplies no native or device execution authority.</summary>

    public static void WritePackage(string repositoryRoot, string outputPath)
    {
        string root = Path.GetFullPath(repositoryRoot), output = Path.GetFullPath(outputPath);
        string artifacts = Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar;
        if (!output.StartsWith(artifacts, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ToolFailure("m16_025_fixture_artifacts_required");
        EfiBuildFiles.InitializeDirectory(Path.GetDirectoryName(output)!, "m16_025_fixture_output");
        EfiBuildFiles.RequirePlainOrMissing(root, output, "m16_025_fixture_output");
        byte[] package = CreatePackage();
        using (FileStream file = new(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            file.Write(package);
            file.Flush(true);
        }

        using AuthorityFiles authority = new();
        var held = authority.Open(root, output, "disk mechanical package", "m16_025_fixture_output", package.Length, singleLink: true);
        RequirePackage(held.ReadBytes(package.Length, "m16_025_fixture_output"));
        authority.VerifyUnchanged();
    }

    private static (byte[] Elf, ulong[] Rips, ulong[] Instructions) Build()
    {
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
        Load(120, 1, 6, 4096, DataBase, 6 * 4096, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        void Text(int at, string value) => Encoding.ASCII.GetBytes(value).CopyTo(elf, at);
        Text(WitnessOffset, Witness);
        Text(PathOffset, "/app/wwwroot/fixture/fragmented.bin\0");
        Text(MissingOffset, "/app/wwwroot/fixture/missing.bin\0");
        Text(OutsideOffset, "/app/wwwroot/../../private/sentinel.txt\0");
        Text(NotDirectoryOffset, "/app/wwwroot/fixture/large.bin/child\0");
        Text(EmptyOffset, "/app/wwwroot/fixture/empty.bin\0");
        Text(DirectoryOffset, "/app/wwwroot/fixture/nested\0");
        elf.AsSpan(OutputOffset, 3 * 4096).Fill(0xa5);
        Writer w = new(elf, EntryOffset);
        w.Instruction(0xc031, 2);
        w.Instruction(0xc88c66, 3);
        w.CompareRax(0x3b);
        w.Immediate(10, 0);
        w.Immediate(8, 0);
        w.Immediate(9, 0);
        const uint length = WebRootImageFixture.FragmentedFixtureBytes;
        ulong path = ImageBase + PathOffset, stat = ImageBase + StatOffset;
        w.Call(4, path, stat, 0, 0);
        w.MemoryDword(StatOffset + 24, 0x8124);
        w.MemoryQword(StatOffset + 48, length);
        w.Call(6, ImageBase + DirectoryOffset, stat, 0, 0);
        w.MemoryDword(StatOffset + 24, 0x416d);
        w.Call(257, 0xffffff9c, path, 0, 3);
        w.Call(5, 3, stat, 0, 0);
        w.MemoryQword(StatOffset + 48, length);
        w.Call(0, 3, Output, 16384, 8192);
        PatternByte(0, 0);
        PatternByte(511, 511);
        PatternByte(512, 512);
        PatternByte(8191, 8191);
        w.Call(8, 3, 0, 1, 8192);
        w.Call(72, 3, 0, 6, 6);
        w.Call(0, 6, Output + 4094, 8, 8);
        PatternByte(4094, 8192);
        PatternByte(4101, 8199);
        w.Syscall(17, 3, Output, 4, 65535);
        w.CompareRax(4);
        PatternByte(0, 65535);
        PatternByte(3, 65538);
        w.Call(8, 3, 0, 1, 8200);
        w.Call(2, path, 0, 0, 4);
        w.Call(0, 4, Output, 1, 1);
        PatternByte(0, 0);
        w.Call(3, 4, 0, 0, 0);
        w.Call(2, path, 0, 0, 4);
        w.Call(0, 4, Output, 1, 1);
        PatternByte(0, 0);
        w.Call(3, 4, 0, 0, 0);
        w.Call(8, 3, length - 2, 0, length - 2);
        w.Call(0, 3, Output, 8, 2);
        PatternByte(0, length - 2);
        PatternByte(1, length - 1);
        w.Call(0, 3, 0, 8, 0); // EOF requires no output authority.
        w.Call(8, 3, 0, 0, 0);
        w.Call(0, 3, 0, 1, -14);
        w.Call(8, 3, 0, 1, 0);
        w.Call(0, 3, ImageBase + EntryOffset, 1, -14);
        w.Call(8, 3, 0, 1, 0);
        w.Call(0, 3, ImageBase + ElfBytes - 1, 4, -14);
        w.Call(8, 3, 0, 1, 0);
        w.MemoryByte(ElfBytes - 1, 0xa5);
        w.Call(8, 3, length - 1, 0, length - 1);
        w.Call(0, 3, ImageBase + ElfBytes - 1, 4, 1);
        w.MemoryByte(ElfBytes - 1, Pattern(length - 1));
        w.Call(8, 3, 0, 99, -22);
        w.Call(0, 3, Output, 65537, -22);
        w.Call(2, path, 1, 0, -30);
        w.Call(2, path, 0x40, 0, -30);
        w.Call(2, ImageBase + MissingOffset, 0, 0, -2);
        w.Call(2, ImageBase + OutsideOffset, 0, 0, -13);
        w.Call(2, ImageBase + NotDirectoryOffset, 0, 0, -20);
        // Eight independent filesystem handles, plus a shared duplicate.
        uint[] more = [4, 5, 7, 8, 9, 10, 11];
        foreach (uint fd in more)
            w.Call(2, path, 0, 0, fd);
        w.Call(2, path, 0, 0, -23);
        foreach (uint fd in more)
            w.Call(3, fd, 0, 0, 0);
        w.Call(3, 3, 0, 0, 0);
        w.Call(0, 3, Output, 1, -9);
        w.Call(3, 6, 0, 0, 0);
        w.Call(2, ImageBase + EmptyOffset, 0, 0, 3);
        w.Call(5, 3, stat, 0, 0);
        w.MemoryQword(StatOffset + 48, 0);
        w.Call(0, 3, 0, 1, 0);
        w.Call(3, 3, 0, 0, 0);
        w.Call(1, 1, ImageBase + WitnessOffset, (uint)Witness.Length, Witness.Length);
        w.Syscall(231, 42, 0, 0);
        w.JumpFailure();
        ulong[] rips = w.Rips.ToArray();
        ulong[] instructions = w.Instructions.ToArray();
        if (w.Position >= FailureOffset || rips.Length > 128)
            throw new ToolFailure("m16_025_fixture_code_capacity");
        w.Position = FailureOffset;
        w.Instruction(0xfc, 1);
        w.Syscall(231, 99, 0, 0);
        w.Instruction(0xf4, 1);
        if (w.Rips[^1] != FailureExitRip)
            throw new ToolFailure("m16_025_fixture_failure_rip");
        return (elf, rips, instructions);
        void PatternByte(int output, uint offset) => w.MemoryByte(OutputOffset + output, Pattern(offset));
    }

    private static byte Pattern(uint offset) => unchecked((byte)((offset * 73 + 19) ^ (offset >> 9) ^ (offset >> 17)));
}
