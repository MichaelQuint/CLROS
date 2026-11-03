using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Complete inner-leaf accounting, without parent firmware, native
    /// ABI, NIC, packet, peer, runner or guest-qualification authority.</summary>

    public readonly record struct ServiceReplayReport(ThreadingProtocolReport Protocol, ServiceStartupInspection Startup, ServiceRandomInspection Random, ServiceTransportInspection Transport, int SocketAccepts = 0, ulong SocketSent = 0, ulong SocketReceived = 0, ServiceGcInspection? Gc = null, ServiceMemoryInspection? Memory = null);
}
