using System.Text;

namespace OS.Tooling;

/// <summary>Immutable instruction lookup for the original socket ELF in either
/// mode. Construction requires current relevant sources, a sealed original
/// package and the reviewed complete LLVM index. Borrowed snapshots are not
/// retained. Missing, stale or forged inputs throw; no code or guest executes.</summary>
public sealed class NativeAotSocketProgram
{
    internal const int MaximumDisassemblyBytes = 32 * 1024 * 1024;
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot { get; }
    /// <summary>The canonical package selects the unchanged app's --async mode.</summary>
    public bool Asynchronous { get; }
    /// <summary>Value-only index identity; reports grant no lookup authority.</summary>
    public readonly record struct Inspection(int InstructionCount, int SyscallCount, string Sha256);
    /// <summary>Reviewed complete index identity of this prepared lookup.</summary>
    public Inspection Metadata => Report(index);
    private NativeAotSocketProgram(string root, bool asynchronous, NativeAotInstructionIndex index)
    { SourceRoot = Path.GetFullPath(root); Asynchronous = asynchronous; this.index = index; }
    /// <summary>Accepts decoded instruction starts, excluding operands and gaps.</summary>
    public bool IsInstruction(ulong address) => index.IsInstruction(address);
    /// <summary>Accepts captured RIP immediately following a decoded SYSCALL.</summary>
    public bool IsSyscallReturn(ulong address) => index.IsSyscallReturn(address);
    private static Inspection Report(NativeAotInstructionIndex value) => new(value.Report.InstructionCount, value.Report.SyscallCount, value.Report.Sha256);
    private static NativeAotInstructionIndex Parse(ReadOnlySpan<byte> elf, string dump) => NativeAotInstructionIndex.Parse(elf, dump,
        "socket_instruction_", "CLROS_SOCKET_INSTRUCTIONS_V1\n", 600_000, MaximumDisassemblyBytes);

    /// <summary>Parses bounded executable-byte rows for offline review. Supplied
    /// ELF/text are not retained and even a complete report grants no authority.</summary>
    public static Inspection Inspect(ReadOnlySpan<byte> elf, string dump) => Report(Parse(elf, dump));

    /// <summary>Requires exact package policy/ELF and the independently reviewed
    /// complete decoder index, including every syscall continuation. Caller
    /// bytes/text remain borrowed only during this synchronous call.</summary>
    public static NativeAotSocketProgram Create(string root, bool asynchronous, ReadOnlySpan<byte> package, string dump)
    {
        NativeAotSocketPackage.RequireCurrentSources(root); NativeAotSocketPackage.RequirePackage(package, asynchronous);
        var parsed = Parse(package[(64 + Encoding.ASCII.GetByteCount(NativeAotSocketPackage.Manifest(asynchronous)))..], dump);
        if (Report(parsed) != Reviewed || !parsed.SyscallContinuationsValid) throw new InvalidDataException("socket_instruction_identity");
        NativeAotSocketPackage.RequireCurrentSources(root); return new(root, asynchronous, parsed);
    }
    // Original LLVM23.1 capture independently checked against all five executable
    // sections: 1,307,710 decoded bytes, entry and all186 syscall continuations.
    // The86 executable-load bytes outside those sections grant no lookup starts.
    private static Inspection Reviewed => new(398_041, 186, "0AB8E40505E8C17E9CF67410366F2D5DD962430253058B668DEB4816CCE59F82");

    /// <summary>Reads bounded plain single-link snapshots under concurrent
    /// read-only locks, then calls Create. Distinct files stay caller-owned.
    /// Aliases, invalid UTF8, substitution or changing reads throw before return.</summary>
    public static NativeAotSocketProgram Read(string root, bool asynchronous, string packagePath, string disassemblyPath)
    {
        packagePath = Path.GetFullPath(packagePath); disassemblyPath = Path.GetFullPath(disassemblyPath);
        if (string.Equals(packagePath, disassemblyPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("socket_instruction_alias");
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(packagePath)!, packagePath, "socket package", "socket_instruction_package", 2_117_424 + 4096, singleLink: true);
        var dump = files.Open(Path.GetDirectoryName(disassemblyPath)!, disassemblyPath, "socket disassembly", "socket_instruction_dump", MaximumDisassemblyBytes, singleLink: true);
        AuthorityFiles.RequireDistinct(package, dump);
        byte[] bytes = package.ReadBytes(2_117_424 + 4096, "socket_instruction_package");
        string text = dump.ReadUtf8(MaximumDisassemblyBytes, "socket_instruction_dump");
        var program = Create(root, asynchronous, bytes, text); files.VerifyUnchanged(); return program;
    }
}
