using System.Buffers.Binary;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only transport observation accounting for synthetic
    /// controls. It grants no firmware, packet, process or guest authority.</summary>

    public readonly record struct SocketTransportInspection(int Fresh, int Retired, int TimeWaitLifetimes, ulong MaximumDeadline);
}
