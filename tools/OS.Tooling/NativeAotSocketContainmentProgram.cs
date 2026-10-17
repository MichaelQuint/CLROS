using System.Text;

namespace OS.Tooling;

/// <summary>Sealed lookup for the dedicated containment ELF and reviewed
/// original complete decoder index. Owned numeric arrays retain no caller
/// buffers. Construction is host-only and proves neither app execution nor
/// guest socket/scheduler/network behavior.</summary>
public sealed class NativeAotSocketContainmentProgram
{
    private readonly NativeAotInstructionIndex index;
    internal string SourceRoot { get; }
    private NativeAotSocketContainmentProgram(string root, NativeAotInstructionIndex index)
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
    public static NativeAotSocketContainmentProgram Create(string root, ReadOnlySpan<byte> package, string disassembly)
    {
        NativeAotSocketContainmentPackage.RequireCurrentSources(root);
        NativeAotSocketContainmentPackage.RequirePackage(package);
        int offset = 64 + Encoding.ASCII.GetByteCount(NativeAotSocketContainmentPackage.Manifest);
        var parsed = NativeAotInstructionIndex.Parse(package[offset..], disassembly,
            "socket_containment_instruction_", "CLROS_SOCKET_INSTRUCTIONS_V1\n", 600_000, 32 * 1024 * 1024);
        // Pinned original decoder: all996316 bytes of five executable sections,
        // entry and179 syscall continuations independently checked. The88 load
        // padding bytes outside those sections grant no instruction starts.
        if (parsed.Report != new NativeAotInstructionIndex.Inspection(299_687, 179,
            "D4BEA07F33B58D9BDDCB0E82E0B2D468517BD6B98B29768D89B94331BE5B602F") || !parsed.SyscallContinuationsValid)
            throw new InvalidDataException("socket_containment_instruction_identity");
        NativeAotSocketContainmentPackage.RequireCurrentSources(root); return new(root, parsed);
    }
}
