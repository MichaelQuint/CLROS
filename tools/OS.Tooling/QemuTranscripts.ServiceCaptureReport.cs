using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Joined original parent, complete service leaf and outer cleanup
    /// observations. Native ABI, independent peer/packets and runner remain separate.</summary>

    public readonly record struct ServiceCaptureReport(WebParentReport Parent, ServiceReplayReport Leaf, ServiceEnvelopeInspection Envelope);
}
