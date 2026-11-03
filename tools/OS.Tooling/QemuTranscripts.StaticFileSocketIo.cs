using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied syscall I/O counters from one original accepted open
    /// description, in actual accept order. Sent counts successfully queued
    /// application bytes; independent wire capture determines transmitted bytes.
    /// Closed state remains the observed shutdown state after descriptor close.</summary>

    public readonly record struct StaticFileSocketIo(int Connection, ulong Sent, ulong Received, int Reset, int Eof, bool WriteClosed, bool ReadClosed);
}
