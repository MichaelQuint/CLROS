using System.Globalization;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotSocketGuest
{
    /// <summary>Owned immutable derived text and observations. PendingWaits may
    /// include waits later retired by process exit; this is not a drained report.</summary>

    public sealed class Capture
    {
        internal Capture(NativeAotThreadingOutput.Capture output, Readiness[] readiness, IReadOnlyList<QemuTranscripts.LinuxCallWaitObservation> waits, int pending)
        {
            Output = output;
            Readiness = Array.AsReadOnly(readiness);
            Waits = waits;
            PendingWaits = pending;
        }

        public NativeAotThreadingOutput.Capture Output
        {
            get;
        }
        public IReadOnlyList<Readiness> Readiness
        {
            get;
        }
        public IReadOnlyList<QemuTranscripts.LinuxCallWaitObservation> Waits
        {
            get;
        }
        public int PendingWaits
        {
            get;
        }
    }
}
