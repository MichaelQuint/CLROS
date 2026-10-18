using System.Text;

namespace OS.Tooling;

/// <summary>Sealed lookup for the dedicated vectors ELF and reviewed
/// original complete decoder index. Owned numeric arrays retain no caller
/// buffers. Construction is host-only and proves neither app execution nor
/// guest socket/scheduler/network behavior.</summary>
public sealed class NativeAotSocketVectorsProgram
{
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot { get; }
    private NativeAotSocketVectorsProgram(string root, NativeAotInstructionIndex index)
    { SourceRoot = Path.GetFullPath(root); this.index = index; }
    /// <summary>Value-only reviewed index identity.</summary>
    public NativeAotSocketProgram.Inspection Metadata => new(index.Report.InstructionCount, index.Report.SyscallCount, index.Report.Sha256);
    /// <summary>Accept only original decoded starts, excluding operands/gaps.</summary>
    public bool IsInstruction(ulong address) => index.IsInstruction(address);
    /// <summary>Accept only decoded original SYSCALL continuations.</summary>
    public bool IsSyscallReturn(ulong address) => index.IsSyscallReturn(address);

    /// <summary>Check current source, canonical package and the reviewed full
    /// index with all syscall continuations. Borrowed bytes/text are consumed
    /// synchronously; substitution, omission and invalid boundaries throw.</summary>
    public static NativeAotSocketVectorsProgram Create(string root, ReadOnlySpan<byte> package, string disassembly)
    {
        NativeAotSocketVectorsPackage.RequireCurrentSources(root);
        NativeAotSocketVectorsPackage.RequirePackage(package);
        int offset = 64 + Encoding.ASCII.GetByteCount(NativeAotSocketVectorsPackage.Manifest);
        var parsed = NativeAotInstructionIndex.Parse(package[offset..], disassembly,
            "socket_vectors_instruction_", "CLROS_SOCKET_INSTRUCTIONS_V1\n", 600_000, 32 * 1024 * 1024);
        // Pinned policy-fixture decoder: all1162046 bytes of five executable
        // sections, entry and186 syscall continuations independently checked.
        // Load padding outside those sections grants no instruction starts.
        if (parsed.Report != new NativeAotInstructionIndex.Inspection(350_241, 186,
            "12CD4B3227A27A20065C1BE9B1F2AB78C6C5C1C15A0A1D0197280E68E6A44C3B") || !parsed.SyscallContinuationsValid)
            throw new InvalidDataException("socket_vectors_instruction_identity");
        NativeAotSocketVectorsPackage.RequireCurrentSources(root); return new(root, parsed);
    }

    /// <summary>Read bounded distinct single-link original inputs under
    /// read-only locks. Consume owned snapshots through Create and verify their
    /// unchanged identity. Files remain caller owned; aliases, invalid text or
    /// changing inputs reject before a sealed program is returned.</summary>
    public static NativeAotSocketVectorsProgram Read(string root, string packagePath, string disassemblyPath)
    {
        packagePath=Path.GetFullPath(packagePath);disassemblyPath=Path.GetFullPath(disassemblyPath);
        using AuthorityFiles files=new();
        var package=files.Open(Path.GetDirectoryName(packagePath)!,packagePath,"vectors package","socket_vectors_package",
            NativeAotSocketVectorsPackage.ElfBytes+4096,singleLink:true);
        var dump=files.Open(Path.GetDirectoryName(disassemblyPath)!,disassemblyPath,"vectors disassembly","socket_vectors_disassembly",32*1024*1024,singleLink:true);
        AuthorityFiles.RequireDistinct(package,dump);
        var program=Create(root,package.ReadBytes(NativeAotSocketVectorsPackage.ElfBytes+4096,"socket_vectors_package"),
            dump.ReadUtf8(32*1024*1024,"socket_vectors_disassembly"));
        files.VerifyUnchanged();return program;
    }
}
