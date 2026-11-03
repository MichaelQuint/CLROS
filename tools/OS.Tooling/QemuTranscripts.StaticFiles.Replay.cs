using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Borrow one complete bounded raw inner M16-009 static-app tail,
    /// original ELF and instruction predicates. Replay the existing memory,
    /// syscall, FIFO, futex, pipe, epoll, signal-frame and cleanup model with
    /// explicit 32-descriptor staged admission. Mandatory path/file witnesses
    /// bind immutable package views rather than guessing user path bytes. The
    /// output decoder retains every channel/length; stderr entry/end belong to
    /// the original main generation. Missing witness, partial prefix, timeout,
    /// duplicate request or mismatched cleanup rejects. Caller values supply
    /// no prepared-program, source, native, packet, process or guest authority.</summary>

    public static StaticFileReplayReport InspectNativeAotStaticFileProtocolForTests(byte[] elf, ReadOnlySpan<byte> raw, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot) => ReplayNativeAotStaticFileTail(elf, raw, instruction, syscallReturn, userRoot, kernelRoot);
    // The enclosing consumer alone supplies sealed package/program/source and
    // native authority. This common leaf returns only copied protocol facts.
    internal static StaticFileReplayReport ReplayNativeAotStaticFileTail(byte[] elf, ReadOnlySpan<byte> raw, Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot)
    {
        var framed = NativeAotThreadingOutput.ReadWeb(raw);
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "static_replay_complete_tail");
        var capture = NativeAotSocketGuest.ReadReadiness(framed);
        var replay = new ThreadingProtocol(NativeAotStaticFilePublisher.Application, Rows(framed), elf, instruction, syscallReturn, framed.WriteLengths, userRoot, kernelRoot, socketCapture: capture);
        var protocol = replay.Validate();
        return replay.StaticReport(protocol);
    }
}
