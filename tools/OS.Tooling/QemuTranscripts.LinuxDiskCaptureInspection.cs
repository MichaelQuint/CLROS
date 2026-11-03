using System.Buffers.Binary;
using System.Security.Cryptography;
using OS.Kernel.Compatibility.LinuxX64;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied value joins only. Parent values, canonical leaf, independent
    /// image inventory, every observed sector byte, read windows, timing period
    /// and exact instruction boundaries are checked. This is not CPU instruction
    /// emulation, user-buffer replay, firmware framing, original-runner, packet,
    /// source/native provenance, device identity or complete-guest authority.</summary>

    public sealed class LinuxDiskCaptureInspection
    {
        public WebParentReport Parent { get; }
        public LinuxDiskFileLeafReport Leaf { get; }
        public NativeDiskReadObservations.Inspection Device { get; }
        public string ImageSha256 { get; }
        public int LeafStartIndex { get; }
        public IReadOnlyList<LinuxDiskAuxiliary> Auxiliary { get; }
        public IReadOnlyList<int> IoRecordIndices { get; }
        public IReadOnlyList<DiskReadWindow> ReadWindows { get; }
        public int InstructionIrqs { get; }
        public bool CheckedRunnerNativeFirmwarePackets => false;
        public bool ReplayedArbitraryInstructionsOrUserBuffers => false;

        internal LinuxDiskCaptureInspection(WebParentReport parent, LinuxDiskFileLeafReport leaf, NativeDiskReadObservations.Inspection device, string hash, int first, LinuxDiskAuxiliary[] auxiliary, int[] indexes, DiskReadWindow[] windows, int irqs)
        {
            Parent = parent;
            Leaf = leaf;
            Device = device;
            ImageSha256 = hash;
            LeafStartIndex = first;
            Auxiliary = Array.AsReadOnly(auxiliary);
            IoRecordIndices = Array.AsReadOnly(indexes);
            ReadWindows = Array.AsReadOnly(windows);
            InstructionIrqs = irqs;
        }
    }
}
