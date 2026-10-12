using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;

/// <summary>
/// C#-authored bounded memory/startup syscall witnesses for M16-006. These
/// immutable mechanical ELFs use the ordinary package and user boundary;
/// their execution does not qualify NativeAOT Hello or an upstream runtime.
/// </summary>
public static class LinuxMemoryProofFixture
{
    public static IReadOnlyList<string> Variants { get; } = Array.AsReadOnly<string>(["Memory", "GuardFault", "ProtNoneFault"]);
    public const string Witness = "M16_MEMORY_HIGH_COPY_OK!";
    public const ulong HugeReservation = 8234266624, DynamicBase = 0x100000000, HeapBase = 0x400000000;
    public const ulong ImageBase = 0x400000, DataBase = 0x401000, StackStart = 0x6fff0000;
    public const ulong GuardFaultAddress = DynamicBase, ProtNoneFaultAddress = DynamicBase + 4096;
    public const uint MemoryCalls = 34;
    private const int FailureOffset = 0xf00, WitnessOffset = 0x1200;
    private const int AffinityOffset = 0x1100, FirstTimeOffset = 0x1120, SecondTimeOffset = 0x1130;
    private const int FirstRandomOffset = 0x1140, SecondRandomOffset = 0x1150, SystemInfoOffset = 0x1180;
    private const ulong FirstPage = DynamicBase + 4096, SecondPage = DynamicBase + 8192;

    /// <summary>Build a fresh canonical CLRELF01 package with an ELF digest.
    /// The caller owns the returned bytes; unknown variants fail before output.</summary>
    public static byte[] CreatePackage(string variant)
    {
        byte[] elf = CreateElf(variant);
        byte[] manifest = Encoding.ASCII.GetBytes("format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" +
            "app.path=apps/m16memory.elf\nargs=" + variant + "\ndigest=" + Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant() +
            "\nmemory=67108864\nstack=65536\nthreads=1\ndescriptors=2\nlisten=127.0.0.1:8080\n");
        byte[] package = new byte[64 + manifest.Length + elf.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(package, LinuxElfPackage.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(16), 64);
        BinaryPrimitives.WriteUInt32LittleEndian(package.AsSpan(20), (uint)manifest.Length);
        BinaryPrimitives.WriteUInt64LittleEndian(package.AsSpan(24), (ulong)elf.Length);
        manifest.CopyTo(package, 64); elf.CopyTo(package, 64 + manifest.Length);
        if (!LinuxElfPackage.Validate(package).Succeeded) throw new ToolFailure("m16_006_fixture_package_invalid");
        return package;
    }

    /// <summary>Build a fresh non-PIE static x64 ELF. Memory returns exit 42
    /// after 34 actual calls; negatives fault after owning a sparse mapping.
    /// Every failed user assertion transfers to a distinct exit-99 path.</summary>
    public static byte[] CreateElf(string variant)
    {
        if (!Variants.Contains(variant, StringComparer.Ordinal)) throw new ToolFailure("m16_006_fixture_variant");
        byte[] elf = new byte[12288]; elf.AsSpan(0x200, 4096 - 0x200).Fill(0xcc);
        "\u007fELF"u8.CopyTo(elf); elf[4] = 2; elf[5] = 1; elf[6] = 1;
        void U16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(offset), value);
        void U32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(offset), value);
        void U64(int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(offset), value);
        U16(16, 2); U16(18, 62); U32(20, 1); U64(24, ImageBase + 0x200);
        U64(32, 64); U16(52, 64); U16(54, 56); U16(56, 3);
        void Load(int at, uint type, uint flags, ulong offset, ulong address, ulong bytes, ulong alignment)
        { U32(at, type); U32(at + 4, flags); U64(at + 8, offset); U64(at + 16, address); U64(at + 24, address); U64(at + 32, bytes); U64(at + 40, bytes); U64(at + 48, alignment); }
        Load(64, 1, 5, 0, ImageBase, 4096, 4096); Load(120, 1, 6, 4096, DataBase, 8192, 4096);
        Load(176, 0x6474e551, 6, 0, 0, 0, 16);
        Encoding.ASCII.GetBytes(Witness).CopyTo(elf, WitnessOffset);
        Writer w = new(elf, 0x200);
        w.Map(0, HugeReservation, 0, 0x22); w.CompareRegister(0, DynamicBase);
        if (variant != "Memory")
        {
            if (variant == "GuardFault") { w.Syscall(10, FirstPage, 4096, 3); w.CompareRax(0); }
            w.Immediate(0, variant == "GuardFault" ? GuardFaultAddress : ProtNoneFaultAddress);
            w.Op(0x008b48, 3); // Actual read from owned but absent PROT_NONE leaf -> #PF.
            w.JumpFailure();
        }
        else
        {
            w.Syscall(10, FirstPage, 8192, 3); w.CompareRax(0); w.ZeroRegion(FirstPage, 8192);
            const ulong sentinel = 0x1122334455667788;
            w.Immediate(0, FirstPage); w.Immediate(2, sentinel); w.Op(0x108948, 3);
            w.Syscall(10, FirstPage, 4096, 1); w.CompareRax(0); w.ReadCompare(FirstPage, sentinel);
            w.Syscall(10, FirstPage, 4096, 3); w.CompareRax(0); w.ReadCompare(FirstPage, sentinel);
            w.Map(FirstPage, 4096, 0, 0x32); w.CompareRegister(0, FirstPage);
            w.Syscall(10, FirstPage, 4096, 3); w.CompareRax(0); w.ZeroRegion(FirstPage, 4096);
            // Copy the literal through real high backing, straddling the two committed pages.
            const ulong highBuffer = SecondPage - 8;
            w.Immediate(7, highBuffer); w.Immediate(6, ImageBase + WitnessOffset); w.Immediate(1, (ulong)Witness.Length);
            w.Op(0xfc, 1); w.Op(0xa4f3, 2);
            w.Syscall(1, 1, highBuffer, (ulong)Witness.Length); w.CompareRax(Witness.Length);
            w.Syscall(11, SecondPage, 4096, 0); w.CompareRax(0);
            w.Syscall(10, FirstPage, 8192, 3); w.CompareRax(-12);
            w.Syscall(10, FirstPage, 4096, 7); w.CompareRax(-22);
            w.Map(0, 4096, 3, 0x800022); w.CompareRax(-22); // Unknown bit 23, beyond the admitted mapping flags.
            w.Map(FirstPage, 4096, 0, 0x100022); w.CompareRax(-17);
            w.Map(0, 4096UL * 4096 + 4096, 3, 0x22); w.CompareRax(-12);
            w.Syscall(28, FirstPage, 4096, 16); w.CompareRax(0);
            w.Syscall(28, FirstPage, 4096, 17); w.CompareRax(0);
            w.Syscall(11, DynamicBase, 4096, 0); w.CompareRax(0);
            w.Syscall(11, DynamicBase, HugeReservation, 0); w.CompareRax(0);

            w.Syscall(204, 0, 8, ImageBase + AffinityOffset); w.CompareRax(8);
            w.Rip(0x058b48, 3, AffinityOffset); w.CompareRax(1);
            w.Syscall(99, ImageBase + SystemInfoOffset, 0, 0); w.CompareRax(0);
            w.Rip(0x058b48, 3, SystemInfoOffset + 32); w.NonzeroRax();
            w.Rip(0x058b, 2, SystemInfoOffset + 104); w.CompareRax(1);
            w.Syscall(324, 0, 0, 0); w.CompareRax(24);
            w.Syscall(324, 16, 0, 0); w.CompareRax(0);
            w.Syscall(324, 8, 0, 0); w.CompareRax(0);
            w.Syscall(228, 1, ImageBase + FirstTimeOffset, 0); w.CompareRax(0);
            w.Rip(0x258b4c, 3, FirstTimeOffset); w.Rip(0x2d8b4c, 3, FirstTimeOffset + 8); // R12/R13 retain first timespec.
            w.Syscall(228, 1, ImageBase + SecondTimeOffset, 0); w.CompareRax(0);
            w.Rip(0x058b48, 3, SecondTimeOffset); w.Op(0xe0394c, 3); w.BranchFailure(0x82);
            int secondsAdvanced = w.ForwardBranch(0x87);
            w.Rip(0x058b48, 3, SecondTimeOffset + 8); w.Op(0xe8394c, 3); w.BranchFailure(0x82);
            w.PatchBranch(secondsAdvanced);
            w.Syscall(318, ImageBase + FirstRandomOffset, 16, 0); w.CompareRax(16);
            w.Syscall(318, ImageBase + SecondRandomOffset, 16, 0); w.CompareRax(16);
            w.NonzeroPair(FirstRandomOffset); w.NonzeroPair(SecondRandomOffset);
            w.Rip(0x058b48, 3, FirstRandomOffset); w.Rip(0x053348, 3, SecondRandomOffset); w.Op(0xc28948, 3);
            w.Rip(0x058b48, 3, FirstRandomOffset + 8); w.Rip(0x053348, 3, SecondRandomOffset + 8); w.Op(0xd00948, 3); w.NonzeroRax();
            w.Syscall(318, ImageBase + FirstRandomOffset, 16, 1); w.CompareRax(-11); // Depleted finite pool, genuine NONBLOCK EAGAIN.
            w.Syscall(12, 0, 0, 0); w.CompareRegister(0, HeapBase);
            w.Syscall(12, HeapBase + 8192, 0, 0); w.CompareRegister(0, HeapBase + 8192);
            w.Syscall(12, 0, 0, 0); w.CompareRegister(0, HeapBase + 8192);
            w.Syscall(12, HeapBase, 0, 0); w.CompareRegister(0, HeapBase);
            // musl's mapped main-stack probe cannot grow in place; the absent guard gives EFAULT.
            w.Immediate(0, 25); w.Op(0xe78948, 3); w.Op(0xe78148, 3); w.Op(0xfffff000, 4);
            w.Immediate(6, 4096); w.Immediate(2, 8192); w.Immediate(10, 0); w.Immediate(8, 0); w.Op(0x050f, 2); w.Calls++;
            w.CompareRax(-12);
            w.Syscall(25, StackStart - 4096, 4096, 8192, 0, 0); w.CompareRax(-14);
            w.Syscall(231, 42, 0, 0); w.JumpFailure();
            if (w.Calls != MemoryCalls) throw new ToolFailure("m16_006_fixture_call_inventory");
        }
        if (w.Position >= FailureOffset) throw new ToolFailure("m16_006_fixture_code_capacity");
        w.Position = FailureOffset; w.Op(0xfc, 1); w.Syscall(231, 99, 0, 0); w.Op(0xf4, 1);
        return elf;
    }

    // Same narrow C# byte writer as the earlier syscall fixture; six arguments
    // and local forward branches are needed for the Linux memory ABI.
    private sealed class Writer(byte[] bytes, int position)
    {
        public int Position = position;
        public uint Calls;
        public void Op(ulong value, int count) { for (int i = 0; i < count; i++) bytes[Position++] = (byte)(value >> (i * 8)); }
        public void Immediate(uint register, ulong value) { Op(register < 8 ? 0x48UL : 0x49UL, 1); Op(0xb8 + (register & 7), 1); Op(value, 8); }
        public void Rip(ulong opcode, int count, int target) { Op(opcode, count); Op(unchecked((uint)(target - (Position + 4))), 4); }
        public void JumpFailure() { Op(0xe9, 1); Op(unchecked((uint)(FailureOffset - (Position + 4))), 4); }
        public void BranchFailure(byte condition) { Op(0x0f, 1); Op(condition, 1); Op(unchecked((uint)(FailureOffset - (Position + 4))), 4); }
        public int ForwardBranch(byte condition) { Op(0x0f, 1); Op(condition, 1); int at = Position; Op(0, 4); return at; }
        public void PatchBranch(int at) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), unchecked((uint)(Position - (at + 4))));
        public void CompareRax(long expected) { Op(0x3d48, 2); Op(unchecked((uint)expected), 4); BranchFailure(0x85); }
        public void CompareRegister(uint register, ulong expected)
        {
            if (register != 0) throw new ToolFailure("m16_006_fixture_compare_register");
            Immediate(2, expected); Op(0xd03948, 3); BranchFailure(0x85);
        }
        public void NonzeroRax() { Op(0xc08548, 3); BranchFailure(0x84); }
        public void NonzeroPair(int at) { Rip(0x058b48, 3, at); Rip(0x050b48, 3, at + 8); NonzeroRax(); }
        public void ReadCompare(ulong address, ulong expected) { Immediate(0, address); Op(0x008b48, 3); CompareRegister(0, expected); }
        public void ZeroRegion(ulong address, ulong bytes)
        { Immediate(0, 0); Immediate(7, address); Immediate(1, bytes / 8); Op(0xfc, 1); Op(0xaf48f3, 3); BranchFailure(0x85); }
        public void Map(ulong address, ulong size, ulong protection, ulong flags) => Syscall(9, address, size, protection, flags, ulong.MaxValue, 0);
        public void Syscall(ulong number, ulong a0, ulong a1, ulong a2, ulong? a3 = null, ulong? a4 = null, ulong? a5 = null)
        {
            Immediate(0, number); Immediate(7, a0); Immediate(6, a1); Immediate(2, a2);
            Immediate(10, a3 ?? 0); Immediate(8, a4 ?? 0); Immediate(9, a5 ?? 0);
            Op(0x050f, 2); Calls++;
        }
    }
}
