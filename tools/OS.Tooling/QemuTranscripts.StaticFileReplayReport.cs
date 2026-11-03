using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Complete finite scheduler replay facts only. Every count comes
    /// from original instruction-bound calls, real exits and generation reaps.
    /// This value grants no native/source, packet, runner or guest authority.</summary>

    public readonly record struct StaticFileReplayReport(ThreadingProtocolReport Protocol, ulong ReadyNanoseconds, ulong RequestedNanoseconds, ulong ExitedNanoseconds, int FileObservations, int PathObservations, int Signal15Returns, IReadOnlyList<StaticFileSocketIo> Connections);
}
