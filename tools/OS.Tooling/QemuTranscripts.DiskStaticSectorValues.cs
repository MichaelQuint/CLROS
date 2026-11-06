using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public sealed record DiskStaticSectorValues(NativeDiskReadObservations.Inspection Device, NativeDiskStaticReadWindows.Inspection Data, IReadOnlyList<WebRootDiskInventory.MetadataSector> Metadata)
    {
        public bool GrantsOriginalNativeProcessOrHttpAuthority => false;
        public bool ReconstructsFileSystemCache => false;
    }
}
