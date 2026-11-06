using System.Text;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private readonly record struct SocketContainmentPeerResult(ulong ArpReplies, ulong HeadTransmissions, ulong HealthyReplies, ulong VlanFrames);
}
