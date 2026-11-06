using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Validate the separate containment leaf against its canonical
    /// package, sealed original instruction index and bounded raw observations.
    /// Borrow inputs synchronously; do not retain caller buffers. Inconsistent
    /// syscall/scheduler/descriptor lifetimes throw. Parent NIC/packet/peer,
    /// original native provenance and runner cleanup remain mandatory; this
    /// leaf alone grants no complete QEMU or backlog acceptance.</summary>

    public static void ValidateNativeAotSocketContainmentTail(string root, byte[] package, NativeAotSocketContainmentProgram program, ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null && string.Equals(Path.GetFullPath(root), program.SourceRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "socket_containment_program_owner");
        NativeAotSocketContainmentPackage.RequireCurrentSources(root);
        NativeAotSocketContainmentPackage.RequirePackage(package);
        var capture = NativeAotSocketContainmentGuest.Read(rawTail);
        int offset = 64 + Encoding.ASCII.GetByteCount(NativeAotSocketContainmentPackage.Manifest);
        var owner = program!;
        _ = new ThreadingProtocol(NativeAotSocketContainmentPublisher.Application, Rows(capture.Scheduling.Output), package[offset..], owner.IsInstruction, owner.IsSyscallReturn, capture.Scheduling.Output.WriteLengths, userRoot, kernelRoot, containmentCapture: capture).Validate();
        NativeAotSocketContainmentPackage.RequireCurrentSources(root);
    }

    /// <summary>Synthetic value-only control seam for full containment leaf
    /// replay. Supplied ELF/predicates grant no sealed program, original package,
    /// packet, parent firmware or guest execution authority.</summary>

    public static ThreadingProtocolReport InspectNativeAotSocketContainmentProtocolForTests(byte[] elf, ReadOnlySpan<byte> rawTail, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot)
    {
        var capture = NativeAotSocketContainmentGuest.Read(rawTail);
        return new ThreadingProtocol(NativeAotSocketContainmentPublisher.Application, Rows(capture.Scheduling.Output), elf, instruction, syscallReturn, capture.Scheduling.Output.WriteLengths, userRoot, kernelRoot, containmentCapture: capture).Validate();
    }
}
