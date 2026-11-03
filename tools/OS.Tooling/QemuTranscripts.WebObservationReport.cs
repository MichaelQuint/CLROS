using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Value-only observations of a complete web-leaf prefix and its
    /// final incomplete kernel row. Hashes retain the original raw bytes; the
    /// fragment is never replayed as a completed syscall or invented cleanup.
    /// Call shapes/ordinals/returns, readiness bindings, write lengths/channels
    /// and M10 startup observations are checked. This is not full syscall,
    /// scheduler, packet, native, source, runner or guest acceptance.</summary>

    public readonly record struct WebObservationReport(int Calls, int Writes, int StdoutBytes, int StderrBytes, int PendingEpollWaits, string TrailingFragment, string RawSha256, string CompletePrefixSha256);
}
