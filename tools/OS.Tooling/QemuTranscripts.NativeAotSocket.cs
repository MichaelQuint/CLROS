using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Validate the original socket leaf against its canonical package,
    /// sealed instruction index and raw stdout. Scheduler, waits, signals and
    /// descriptor cleanup must agree. Inputs remain caller owned; unsupported or
    /// inconsistent observations throw. Parent firmware/NIC/packet/peer and runner
    /// provenance remain mandatory; this leaf grants no QEMU acceptance alone.</summary>

    public static void ValidateNativeAotSocketTail(string root, byte[] package, NativeAotSocketProgram program, ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null && string.Equals(Path.GetFullPath(root), program.SourceRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "socket_program_owner");
        NativeAotSocketPackage.RequireCurrentSources(root);
        NativeAotSocketPackage.RequirePackage(package, program!.Asynchronous);
        var capture = NativeAotSocketGuest.Read(program.Asynchronous, rawTail);
        int offset = 64 + Encoding.ASCII.GetByteCount(NativeAotSocketPackage.Manifest(program.Asynchronous));
        _ = new ThreadingProtocol(program.Asynchronous ? "NativeAotSocketSmokeAsync" : "NativeAotSocketSmoke", Rows(capture.Output), package[offset..], program.IsInstruction, program.IsSyscallReturn, capture.Output.WriteLengths, userRoot, kernelRoot, socketCapture: capture).Validate();
        NativeAotSocketPackage.RequireCurrentSources(root);
    }

    /// <summary>Value-only synthetic control seam. Raw stdout and all lifecycle
    /// rows are checked, but caller ELF/predicates grant no sealed program,
    /// package, firmware, packet or guest execution authority.</summary>

    public static ThreadingProtocolReport InspectNativeAotSocketProtocolForTests(bool asynchronous, byte[] elf, ReadOnlySpan<byte> rawTail, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot)
    {
        var capture = NativeAotSocketGuest.Read(asynchronous, rawTail);
        return new ThreadingProtocol(asynchronous ? "NativeAotSocketSmokeAsync" : "NativeAotSocketSmoke", Rows(capture.Output), elf, instruction, syscallReturn, capture.Output.WriteLengths, userRoot, kernelRoot, socketCapture: capture).Validate();
    }
}
