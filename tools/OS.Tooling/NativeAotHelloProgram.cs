using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace OS.Tooling;
/// <summary>
/// Immutable host-side instruction boundaries for the exact qualified Hello.
/// Construction checks its canonical package and a reviewed LLVM-derived index,
/// including every instruction's file-backed executable bytes. This is input
/// admission only: it neither executes the payload nor validates a transcript.
/// Caller spans/text are consumed synchronously and no mutable input is retained.
/// Changed, incomplete or unsupported disassembly throws InvalidDataException.
/// </summary>

public sealed partial class NativeAotHelloProgram
{
    public const int InstructionCount = 243_126, SyscallCount = 166;
    public const string IndexSha256 = "13A2F6FF65B4A2EE0137EDE2C59CFD174D32C91656B54E7E3F09794D49C6F0FB";
    private const int MaximumDisassemblyBytes = 20 * 1024 * 1024;
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot
    {
        get;
    }

    private NativeAotHelloProgram(NativeAotInstructionIndex parsed, string root)
    {
        index = parsed;
        SourceRoot = Path.GetFullPath(root);
    }

    /// <summary>Tests an exact decoded instruction start, excluding operand bytes and gaps.</summary>

    public bool IsInstruction(ulong address) => index.IsInstruction(address);
    /// <summary>Tests the captured RIP immediately after a genuine decoded SYSCALL instruction.</summary>

    public bool IsSyscallReturn(ulong address) => index.IsSyscallReturn(address);
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
        NativeAotInstructionIndex parsed = Parse(package[offset..], llvmDisassembly);
        Require(parsed.Report.InstructionCount == InstructionCount && parsed.Report.SyscallCount == SyscallCount && parsed.Report.Sha256 == IndexSha256, "hello_instruction_identity " + PublicReport(parsed));
        Require(parsed.SyscallContinuationsValid, "hello_syscall_continuation");
        NativeAotHelloGuest.RequireCurrentSources(root);
        return new(parsed, root);
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
        try
        {
            text = new UTF8Encoding(false, true).GetString(dump);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException("hello_instruction_utf8");
        }

        return Create(root, package, text);
    }

    /// <summary>
    /// Reports parsed metadata for independent host controls or offline review of
    /// an admitted static ELF. A report may describe a partial instruction set:
    /// it is not a prepared Hello, and cannot grant instruction/execution authority.
    /// Only Create checks the exact qualified package and complete index digest.
    /// </summary>

    public static InstructionInspection Inspect(ReadOnlySpan<byte> elf, string llvmDisassembly) => PublicReport(Parse(elf, llvmDisassembly));
    private static InstructionInspection PublicReport(NativeAotInstructionIndex index) => new(index.Report.InstructionCount, index.Report.SyscallCount, index.Report.Sha256);
    private static NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf, string disassembly) => NativeAotInstructionIndex.Parse(elf, disassembly, "hello_instruction_", "CLROS_HELLO_INSTRUCTIONS_V1\n", 300_000, MaximumDisassemblyBytes);
    private static byte[] ReadBounded(string path, int maximum)
    {
        using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(file.Length > 0 && file.Length <= maximum, "hello_instruction_file_size");
        byte[] bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        Require(file.ReadByte() == -1, "hello_instruction_file_changed");
        return bytes;
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string reason)
    {
        if (!condition)
            throw new InvalidDataException(reason);
    }
}
