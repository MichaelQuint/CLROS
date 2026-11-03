using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Immutable copied stage values only. ResetAbortedRequest means
    /// the selected counters report reset retirement rather than a used-ring
    /// completion. No native stop, user-memory observation, scheduler, original
    /// artifact, image or guest authority follows from inspecting these values.</summary>

    public sealed class LinuxDiskExitPendingValues
    {
        public IReadOnlyList<LinuxDiskExitPendingRecord> Records { get; }
        public bool ResetAbortedRequest { get; }
        public bool CheckedOriginalExecution => false;
        public bool CheckedNativeStopOrUserMemory => false;

        internal LinuxDiskExitPendingValues(LinuxDiskExitPendingRecord[] records, bool resetAborted)
        {
            Records = Array.AsReadOnly(records);
            ResetAbortedRequest = resetAborted;
        }
    }
}
