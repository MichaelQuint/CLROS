using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

// Shared host-only decoder index. Parsing binds exact executable bytes but does
// not trust the supplied instruction boundaries. Only a public prepared-program
// factory checking a reviewed complete digest can grant lookup authority.
internal sealed class NativeAotInstructionIndex
{
    private readonly ulong[] instructions, returns;
    internal readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
    internal Inspection Report { get; }
    private NativeAotInstructionIndex(ulong[] instructions, ulong[] returns, Inspection report)
    { this.instructions = instructions; this.returns = returns; Report = report; }
    internal bool IsInstruction(ulong address) => Array.BinarySearch(instructions, address) >= 0;
    internal bool IsSyscallReturn(ulong address) => Array.BinarySearch(returns, address) >= 0;
    internal bool SyscallContinuationsValid => returns.All(IsInstruction);

    internal static unsafe NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf, string disassembly, string prefix, string domain, int maximumInstructions, int maximumDisassemblyBytes)
    {
        Require(disassembly is not null && disassembly.Length > 0 && disassembly.Length <= maximumDisassemblyBytes &&
            !disassembly.Contains('\0'), prefix + "text");
        Require(elf.Length > 0 && elf.Length <= 16 * 1024 * 1024, prefix + "elf");
        LinuxElfLoadPlan plan;
        fixed (byte* source = elf)
            Require(LinuxElfLoader.Validate(source, (uint)elf.Length, 64 * 1024 * 1024, 1_572_864, out plan) ==
                LinuxElfLoadStatus.Ready, prefix + "elf");
        List<ulong> starts = [], returns = [];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(domain));
        Span<byte> instruction = stackalloc byte[15], row = stackalloc byte[25];
        ulong previousEnd = 0;
        using StringReader reader = new(disassembly);
        while (reader.ReadLine() is string line)
        {
            Require(line.Length <= 1024, prefix + "line");
            ReadOnlySpan<char> text = line.AsSpan().TrimStart();
            if (text.Contains("file format elf64-x86-64", StringComparison.Ordinal)) continue;
            int colon = text.IndexOf(':');
            if (colon <= 0 || !ulong.TryParse(text[..colon], NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out ulong address)) continue; // Section/label/header, not an instruction row.
            text = text[(colon + 1)..].TrimStart();
            int tab = text.IndexOf('\t');
            Require(tab >= 0, prefix + "row");
            ReadOnlySpan<char> encoded = text[..tab].Trim(), decoded = text[(tab + 1)..].Trim();
            int count = 0;
            while (!encoded.IsEmpty)
            {
                int end = encoded.IndexOf(' ');
                if (end < 0) end = encoded.Length;
                Require(count < 15 && end == 2 && byte.TryParse(encoded[..end], NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture, out _), prefix + "bytes");
                instruction[count++] = byte.Parse(encoded[..end], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                encoded = encoded[end..].TrimStart(' ');
            }
            Require(count > 0 && !decoded.IsEmpty && !decoded.Contains("<unknown>", StringComparison.Ordinal) &&
                decoded[0] != '.', prefix + "decode");
            int mnemonicEnd = decoded.IndexOfAny(' ', '\t');
            ReadOnlySpan<char> mnemonic = mnemonicEnd < 0 ? decoded : decoded[..mnemonicEnd];
            bool syscall = count == 2 && instruction[0] == 0x0f && instruction[1] == 0x05;
            Require(mnemonic.SequenceEqual("syscall") == syscall, prefix + "syscall");
            Require(address >= previousEnd && address <= ulong.MaxValue - (uint)count,
                prefix + "order");
            bool backed = false;
            for (uint i = 0; i < plan.LoadCount; i++)
            {
                uint at = i * 5;
                ulong start = plan.Loads[at], fileBytes = plan.Loads[at + 2];
                if ((plan.Loads[at + 4] & 1) == 0 || address < start || fileBytes < (uint)count ||
                    address - start > fileBytes - (uint)count) continue;
                ulong offset = plan.Loads[at + 1] + address - start;
                Require(elf.Slice(checked((int)offset), count).SequenceEqual(instruction[..count]), prefix + "backing");
                backed = true;
            }
            Require(backed, prefix + "executable");
            Require(starts.Count < maximumInstructions, prefix + "count");
            starts.Add(address); previousEnd = address + (uint)count;
            if (syscall) returns.Add(previousEnd);
            BinaryPrimitives.WriteUInt64LittleEndian(row, address);
            row[8] = (byte)count; instruction[..count].CopyTo(row[9..]); row[9 + count] = syscall ? (byte)1 : (byte)0;
            hash.AppendData(row[..(10 + count)]);
        }
        Require(starts.Count > 0, prefix + "empty");
        return new(starts.ToArray(), returns.ToArray(), new(starts.Count, returns.Count,
            Convert.ToHexString(hash.GetHashAndReset())));
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string reason)
    { if (!condition) throw new InvalidDataException(reason); }
}