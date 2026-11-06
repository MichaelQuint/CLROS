using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OS.Manifest;

namespace OS.Tooling;
/// <summary>C#-authored mechanical ELF fixtures for M16-005 only. No upstream
/// app/runtime acceptance follows from their execution. Every fixture uses the
/// ordinary immutable package, protected loader and user-entry boundary.</summary>

public static partial class LinuxSyscallProofFixture
{
    public static IReadOnlyList<string> Variants { get; } = Array.AsReadOnly<string>(["WriteExit", "PrivilegedFault", "KernelReadFault", "InvalidRsp", "ReturnToData"]);

    public const string Witness = "CPL3=3b SIMD=ok ERRNO=ok!!";
    public const ulong ImageBase = 0x400000, DataBase = 0x401000, BufferAddress = 0x401ff8;
    private const int FailureOffset = 0xf00;
    public static byte[] CreatePackage(string variant)
    {
        byte[] elf = CreateElf(variant);
        byte[] manifest = Encoding.ASCII.GetBytes("format=clros-linux-elf\nversion=1\nprofile=linux-musl-x64-static-v1\n" + "app.path=apps/m16proof.elf\nargs=" + variant + "\ndigest=" + Convert.ToHexString(SHA256.HashData(elf)).ToLowerInvariant() + "\nmemory=67108864\nstack=65536\nthreads=1\ndescriptors=2\nlisten=127.0.0.1:8080\n");
        // Canonical CLRELF01 header; admission independently checks all bytes.
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
            throw new ToolFailure("m16_005_fixture_package_invalid");
        return package;
    }

    public static byte[] CreateElf(string variant)
    {
        if (!Variants.Contains(variant, StringComparer.Ordinal))
            throw new ToolFailure("m16_005_fixture_variant");
        byte[] elf = new byte[12288];
        elf.AsSpan(0x200, 4096 - 0x200).Fill(0xcc);
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
        U64(24, ImageBase + (variant == "ReturnToData" ? 4094UL : 0x200));
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
        Encoding.ASCII.GetBytes(Witness).CopyTo(elf, (int)(BufferAddress - ImageBase));
        U64(0x1100, 0x0123456789abcdef);
        U64(0x1108, 0xfedcba9876543210);
        U32(0x1120, 0x3f80);
        Writer w = new(elf, variant == "ReturnToData" ? 4094 : 0x200);
        if (variant == "ReturnToData")
        {
            w.Op(0x050f, 2);
            return elf;
        }

        w.Op(0xc031, 2);
        w.Op(0xc88c66, 3);
        w.CompareRax(0x3b); // Actual CPL3 CS witness.
        w.Op(0xc031, 2);
        w.Op(0xd08c66, 3);
        w.CompareRax(0x33); // Actual user SS.
        if (variant == "PrivilegedFault")
        {
            w.Op(0xf4, 1);
            w.JumpFailure();
        }
        else if (variant == "KernelReadFault")
        {
            w.Rip(0x05010f, 3, 0x1140); // SGDT is permitted in this UMIP-off profile.
            w.Rip(0x058b48, 3, 0x1142);
            w.Op(0x008b48, 3); // Read present supervisor GDT page -> #PF.
            w.JumpFailure();
        }
        else if (variant == "InvalidRsp")
        {
            w.Immediate(4, 0xffff800000001000);
            w.Immediate(0, 999);
            w.Op(0x050f, 2);
            w.JumpFailure();
        }
        else
        {
            // Actual negative returns must reach user code; any mismatch exits 99.
            w.Syscall(999, 0, 0, 0);
            w.CompareRax(-38);
            w.Syscall(1, 0, 0, 1);
            w.CompareRax(-9);
            w.Syscall(1, 1, 0x402fff, 2);
            w.CompareRax(-14); // Unmapped next page.
            w.Syscall(1, 1, ulong.MaxValue, 1);
            w.CompareRax(-14);
            w.Syscall(1, 1, BufferAddress, 4097);
            w.CompareRax(-22);
            w.Rip(0x056f0ff3, 4, 0x1100); // MOVDQU XMM0, sentinel.
            w.Rip(0x356f0ff3, 4, 0x1100); // MOVDQU XMM6, same sentinel.
            w.Rip(0x15ae0f, 3, 0x1120); // LDMXCSR changed rounding mode.
            uint[] registers = [3, 5, 12, 13, 14, 15, 8, 9, 10];
            foreach (uint register in registers)
                w.Immediate(register, 0x1111000000000000UL + register);
            w.Immediate(0, 1);
            w.Immediate(7, 0xffffffff00000002);
            w.Immediate(6, BufferAddress);
            w.Immediate(2, (ulong)Witness.Length);
            w.Op(0x68, 1);
            w.Op(0x200cd7, 4);
            w.Op(0x9d, 1); // Admitted arithmetic/DF/ID flags, IF remains clear.
            w.Op(0x050f, 2);
            ulong nextRip = ImageBase + (uint)w.Position;
            w.Rip(0x058f9c, 3, 0x1130); // PUSHFQ; POP [flags] before changing flags.
            w.CompareRax(Witness.Length);
            foreach (uint register in registers)
                w.CompareRegister(register, 0x1111000000000000UL + register);
            w.CompareRegister(7, 0xffffffff00000002);
            w.CompareRegister(6, BufferAddress);
            w.CompareRegister(2, (ulong)Witness.Length);
            w.CompareRegister(1, nextRip);
            w.CompareRegister(11, 0x200cd7);
            w.Rip(0x058b48, 3, 0x1130);
            w.CompareRax(0x200cd7);
            w.Rip(0x057f0ff3, 4, 0x1180);
            w.Rip(0x357f0ff3, 4, 0x1190); // Both SSE register images survive.
            foreach (int offset in new[]
            {
                0x1180,
                0x1190
            }

            )
            {
                w.Rip(0x058b48, 3, offset);
                w.CompareRegister(0, 0x0123456789abcdef); // RAX comparison uses RDX scratch below.
                w.Rip(0x058b48, 3, offset + 8);
                w.CompareRegister(0, 0xfedcba9876543210);
            }

            w.Rip(0x1dae0f, 3, 0x1124);
            w.Rip(0x058b, 2, 0x1124);
            w.CompareRax(0x3f80);
            w.Op(0xa14864, 3);
            w.Op(DataBase + 0x100, 8);
            w.CompareRegister(0, 0x0123456789abcdef);
            w.Op(0xa14865, 3);
            w.Op(DataBase + 0x100, 8);
            w.CompareRegister(0, 0x0123456789abcdef);
            w.Op(0xfc, 1);
            w.Syscall(231, 42, 0, 0);
            w.JumpFailure();
        }

        if (w.Position >= FailureOffset)
            throw new ToolFailure("m16_005_fixture_code_capacity");
        w.Position = FailureOffset;
        w.Op(0xfc, 1);
        w.Syscall(231, 99, 0, 0);
        w.Op(0xf4, 1);
        return elf;
    }
}
