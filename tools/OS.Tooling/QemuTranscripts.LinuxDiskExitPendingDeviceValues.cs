using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied stage/device association only. Every IO row retains its
    /// original position and every committed sector is compared with the supplied
    /// image. Firmware framing, image receipts, native/source provenance, thread
    /// replay, packets and the original process owner remain separate obligations.</summary>

    public sealed class LinuxDiskExitPendingDeviceValues
    {
        public LinuxDiskExitPendingValues Stages { get; }
        public NativeDiskReadObservations.Inspection Device { get; }
        public int BindingRecordIndex { get; }
        public IReadOnlyList<int> IoRecordIndices { get; }
        public bool CheckedOriginalExecution => false;
        public bool CheckedNativeStopOrUserMemory => false;

        internal LinuxDiskExitPendingDeviceValues(LinuxDiskExitPendingValues stages, NativeDiskReadObservations.Inspection device, int binding, int[] indexes)
        {
            Stages = stages;
            Device = device;
            BindingRecordIndex = binding;
            IoRecordIndices = Array.AsReadOnly(indexes);
        }
    }
}
