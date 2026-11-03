using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    // Object identity follows the original open description, never a reused fd.
    // This bounded shadow validates observed calls; it does not invent packet
    // readiness or inspect opaque copied user event data.
    private sealed class SocketDescriptor(bool port, uint flags)
    {
        internal readonly bool Port = port;
        internal uint Flags = flags;
        internal int Phase; // 0 created, 1 bound, 2 listening, 3 accepted.
        internal int Connection = -1, Eof, Reset, TimedOut, ShutdownErrors;
        internal ulong Sent, Received, PeekBytes;
        internal bool WriteClosed, ReadClosed, StatusUnknown, SendBlocked;
        internal readonly Dictionary<int, SocketDescriptor> Watches = [];
        internal readonly Dictionary<int, WebProbePipe> ProbeReadWatches = [];
    }
}
