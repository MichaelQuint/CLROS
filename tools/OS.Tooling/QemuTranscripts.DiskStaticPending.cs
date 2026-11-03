using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class DiskStaticPending(Dictionary<string, ulong> call, LinuxCallWaitObservation witness, uint outputPrefix, uint pathPrefix)
    {
        internal readonly Dictionary<string, ulong> Call = new(call);
        internal readonly LinuxCallWaitObservation Witness = witness;
        internal readonly int BeginIndex = witness.RecordIndex;
        internal readonly uint OutputPrefix = outputPrefix, PathPrefix = pathPrefix;
        internal ulong Request;
        internal uint Winner;
        internal bool Observed;
        internal int WakeIndex;
        internal uint CapturedDescriptorGeneration;
    }
}
