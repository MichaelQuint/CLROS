using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

/// <summary>
/// Immutable host-side instruction boundaries for the exact qualified Hello.
/// Construction checks its canonical package and a reviewed LLVM-derived index,
/// including every instruction's file-backed executable bytes. This is input
/// admission only: it neither executes the payload nor validates a transcript.
/// Caller spans/text are consumed synchronously and no mutable input is retained.
/// Changed, incomplete or unsupported disassembly throws InvalidDataException.
/// </summary>
public sealed class NativeAotHelloProgram
{
    public const int InstructionCount = 243_126, SyscallCount = 166;
    public const string IndexSha256 = "13A2F6FF65B4A2EE0137EDE2C59CFD174D32C91656B54E7E3F09794D49C6F0FB";
    private const int MaximumDisassemblyBytes = 20 * 1024 * 1024;
    private readonly ulong[] instructions, syscallReturns;

    private NativeAotHelloProgram(Parsed parsed)
    { instructions = parsed.Instructions; syscallReturns = parsed.Returns; }

    /// <summary>Tests an exact decoded instruction start, excluding operand bytes and gaps.</summary>
    public bool IsInstruction(ulong address) => Array.BinarySearch(instructions, address) >= 0;

    /// <summary>Tests the captured RIP immediately after a genuine decoded SYSCALL instruction.</summary>
    public bool IsSyscallReturn(ulong address) => Array.BinarySearch(syscallReturns, address) >= 0;

    /// <summary>
    /// Binds current app/producer policy, exact package and complete reviewed index.
    /// Counts alone cannot admit omitted, shifted or forged boundaries. No source
    /// file, process, mapping or execution authority is published by construction.
    /// </summary>
    public static NativeAotHelloProgram Create(string root, ReadOnlySpan<byte> package, string llvmDisassembly)
    {
        NativeAotHelloGuest.RequireCurrentSources(root);
        NativeAotHelloGuest.RequirePackage(package);
        int offset = 64 + Encoding.ASCII.GetByteCount(NativeAotHelloGuest.Manifest);
        Parsed parsed = Parse(package[offset..], llvmDisassembly);
        Require(parsed.Report.InstructionCount == InstructionCount && parsed.Report.SyscallCount == SyscallCount &&
            parsed.Report.Sha256 == IndexSha256, "hello_instruction_identity " + parsed.Report);
        foreach (ulong address in parsed.Returns)
            Require(Array.BinarySearch(parsed.Instructions, address) >= 0, "hello_syscall_continuation");
        NativeAotHelloGuest.RequireCurrentSources(root);
        return new(parsed);
    }

    /// <summary>
    /// Reads bounded immutable snapshots through read-only file locks, then uses
    /// Create. Files remain caller-owned; this operation writes and executes nothing.
    /// </summary>
    public static NativeAotHelloProgram Read(string root, string packagePath, string disassemblyPath)
    {
        byte[] package = ReadBounded(packagePath, NativeAotHelloGuest.ElfBytes + 4096);
        byte[] dump = ReadBounded(disassemblyPath, MaximumDisassemblyBytes);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(dump); }
        catch (DecoderFallbackException) { throw new InvalidDataException("hello_instruction_utf8"); }
        return Create(root, package, text);
    }

    /// <summary>
    /// Reports parsed metadata for independent host controls or offline review of
    /// an admitted static ELF. A report may describe a partial instruction set:
    /// it is not a prepared Hello, and cannot grant instruction/execution authority.
    /// Only Create checks the exact qualified package and complete index digest.
    /// </summary>
    public static InstructionInspection Inspect(ReadOnlySpan<byte> elf, string llvmDisassembly) =>
        Parse(elf, llvmDisassembly).Report;

    /// <summary>Value-only parser metadata; no instruction lookup or prepared-program authority.</summary>
    public readonly record struct InstructionInspection(int InstructionCount, int SyscallCount, string Sha256);

    private sealed record Parsed(ulong[] Instructions, ulong[] Returns, InstructionInspection Report);

    private static unsafe Parsed Parse(ReadOnlySpan<byte> elf, string disassembly)
    {
        Require(disassembly is not null && disassembly.Length > 0 && disassembly.Length <= MaximumDisassemblyBytes &&
            !disassembly.Contains('\0'), "hello_instruction_text");
        Require(elf.Length > 0 && elf.Length <= 16 * 1024 * 1024, "hello_instruction_elf");
        LinuxElfLoadPlan plan;
        fixed (byte* source = elf)
            Require(LinuxElfLoader.Validate(source, (uint)elf.Length, 64 * 1024 * 1024, 1_572_864, out plan) ==
                LinuxElfLoadStatus.Ready, "hello_instruction_elf");
        List<ulong> starts = [], returns = [];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("CLROS_HELLO_INSTRUCTIONS_V1\n"u8);
        Span<byte> instruction = stackalloc byte[15], row = stackalloc byte[25];
        ulong previousEnd = 0;
        using StringReader reader = new(disassembly);
        while (reader.ReadLine() is string line)
        {
            Require(line.Length <= 1024, "hello_instruction_line");
            ReadOnlySpan<char> text = line.AsSpan().TrimStart();
            if (text.Contains("file format elf64-x86-64", StringComparison.Ordinal)) continue;
            int colon = text.IndexOf(':');
            if (colon <= 0 || !ulong.TryParse(text[..colon], NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out ulong address)) continue; // Section/label/header, not an instruction row.
            text = text[(colon + 1)..].TrimStart();
            int tab = text.IndexOf('\t');
            Require(tab >= 0, "hello_instruction_row");
            ReadOnlySpan<char> encoded = text[..tab].Trim(), decoded = text[(tab + 1)..].Trim();
            int count = 0;
            while (!encoded.IsEmpty)
            {
                int end = encoded.IndexOf(' ');
                if (end < 0) end = encoded.Length;
                Require(count < 15 && end == 2 && byte.TryParse(encoded[..end], NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture, out _), "hello_instruction_bytes");
                instruction[count++] = byte.Parse(encoded[..end], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                encoded = encoded[end..].TrimStart(' ');
            }
            Require(count > 0 && !decoded.IsEmpty && !decoded.Contains("<unknown>", StringComparison.Ordinal) &&
                decoded[0] != '.', "hello_instruction_decode");
            int mnemonicEnd = decoded.IndexOfAny(' ', '\t');
            ReadOnlySpan<char> mnemonic = mnemonicEnd < 0 ? decoded : decoded[..mnemonicEnd];
            bool syscall = count == 2 && instruction[0] == 0x0f && instruction[1] == 0x05;
            Require(mnemonic.SequenceEqual("syscall") == syscall, "hello_instruction_syscall");
            Require(address >= previousEnd && address <= ulong.MaxValue - (uint)count,
                "hello_instruction_order");
            bool backed = false;
            for (uint i = 0; i < plan.LoadCount; i++)
            {
                uint at = i * 5;
                ulong start = plan.Loads[at], fileBytes = plan.Loads[at + 2];
                if ((plan.Loads[at + 4] & 1) == 0 || address < start || fileBytes < (uint)count ||
                    address - start > fileBytes - (uint)count) continue;
                ulong offset = plan.Loads[at + 1] + address - start;
                Require(elf.Slice(checked((int)offset), count).SequenceEqual(instruction[..count]), "hello_instruction_backing");
                backed = true;
            }
            Require(backed, "hello_instruction_executable");
            Require(starts.Count < 300_000, "hello_instruction_count");
            starts.Add(address); previousEnd = address + (uint)count;
            if (syscall) returns.Add(previousEnd);
            BinaryPrimitives.WriteUInt64LittleEndian(row, address);
            row[8] = (byte)count; instruction[..count].CopyTo(row[9..]); row[9 + count] = syscall ? (byte)1 : (byte)0;
            hash.AppendData(row[..(10 + count)]);
        }
        Require(starts.Count > 0, "hello_instruction_empty");
        return new(starts.ToArray(), returns.ToArray(), new(starts.Count, returns.Count,
            Convert.ToHexString(hash.GetHashAndReset())));
    }

    private static byte[] ReadBounded(string path, int maximum)
    {
        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(file.Length > 0 && file.Length <= maximum, "hello_instruction_file_size");
        byte[] bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes);
        Require(file.ReadByte() == -1, "hello_instruction_file_changed");
        return bytes;
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string reason)
    { if (!condition) throw new InvalidDataException(reason); }
}
