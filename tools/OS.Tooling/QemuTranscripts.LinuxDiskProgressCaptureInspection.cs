using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Complete copied Progress parent/image/device/request joins.
    /// A progress witness is another worker's owned syscall-entry sample inside
    /// a submitted but not yet validated main-read device transaction. It does
    /// not establish syscall dispatch/completion or hardware busy time. Original files,
    /// native/source, firmware, packets and process remain separate obligations.
    /// No FIFO/fairness or arbitrary user-buffer emulation is claimed.</summary>

    public sealed class LinuxDiskProgressCaptureInspection
    {
        public WebParentReport Parent
        {
            get;
        }
        public LinuxDiskRaceLeafReport Leaf
        {
            get;
        }
        public NativeDiskReadObservations.Inspection Device
        {
            get;
        }
        public string ImageSha256
        {
            get;
        }
        public IReadOnlyList<LinuxDiskProgressRequest> Requests
        {
            get;
        }
        public IReadOnlyList<LinuxDiskRaceCall> DevicePendingProgress
        {
            get;
        }
        public bool CheckedOriginalExecution => false;
        public bool CheckedSchedulerReplay => false;
        public bool ReplayedArbitraryUserMemory => false;

        internal LinuxDiskProgressCaptureInspection(WebParentReport parent, LinuxDiskRaceLeafReport leaf, NativeDiskReadObservations.Inspection device, string hash, LinuxDiskProgressRequest[] requests, LinuxDiskRaceCall[] progress)
        {
            Parent = parent;
            Leaf = leaf;
            Device = device;
            ImageSha256 = hash;
            Requests = Array.AsReadOnly(requests);
            DevicePendingProgress = Array.AsReadOnly(progress);
        }
    }
}
