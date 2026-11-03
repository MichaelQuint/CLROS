using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied joins for the exact ExitPending package. Parent, image,
    /// physical bounds, FIFO/stages, metadata and abandoned data-prefix checks
    /// are combined from one row snapshot. This retains no live owner or input
    /// aliases and supplies no firmware/native/original-process/packet authority.</summary>

    public sealed class LinuxDiskExitPendingCaptureInspection
    {
        public WebParentReport Parent { get; }
        public LinuxDiskRaceLeafReport Leaf { get; }
        public LinuxDiskExitPendingDeviceValues Pending { get; }
        public string ImageSha256 { get; }
        public int LeafStartIndex { get; }
        public IReadOnlyList<ulong> CopiedDataSectors { get; }
        public bool CheckedOriginalExecution => false;

        internal LinuxDiskExitPendingCaptureInspection(WebParentReport parent, LinuxDiskRaceLeafReport leaf, LinuxDiskExitPendingDeviceValues pending, string hash, int first, ulong[] sectors)
        {
            Parent = parent;
            Leaf = leaf;
            Pending = pending;
            ImageSha256 = hash;
            LeafStartIndex = first;
            CopiedDataSectors = Array.AsReadOnly(sectors);
        }
    }
}
