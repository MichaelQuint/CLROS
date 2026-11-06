using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Replayed complete prefix and retained in-flight owner state.
    /// Transfers count only completed supervisor observations. An unfinished
    /// syscall/IRQ/return is never counted as completed. Parent/native/packet/
    /// runner/source qualification remains outside this leaf report. The
    /// transport flag additionally requires original control events, exact
    /// peer I/O and actual fd close/private retirement observations.</summary>

    public readonly record struct WebReplayReport(WebObservationReport Observations, ThreadingProtocolReport Protocol, int Active, int Ready, ulong Current, string Boundary, bool ReturningToSupervisor, uint EntropyRemaining, int SocketAccepts, ulong Sent, ulong Received, bool TransportControlsChecked)
    {
        /// <summary>Optional original-order projection after successful complete
        /// replay. Copied role selection supplies no native or code-role authority.</summary>

        public WebSignalRaceReport? SignalRace
        {
            get; init;
        }
    }
}
