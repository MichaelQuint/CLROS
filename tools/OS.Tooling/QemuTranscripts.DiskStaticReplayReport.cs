using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied complete scheduler/file replay facts. Sector rows retain
    /// their original positions for the separate device and read-window joins.
    /// This value inspection grants no original native, process, packet, HTTP
    /// or guest authority; a later inspection cannot rescue a failed original.</summary>

    public sealed record DiskStaticReplayReport(StaticFileReplayReport Static, IReadOnlyList<NativeDiskStaticReadWindows.ReadWindow> Reads, IReadOnlyList<DiskStaticOperation> Operations, IReadOnlyList<DiskStaticSocketWrite> SocketWrites, IReadOnlyList<LinuxDiskAuxiliary> DeviceRows, ulong Owner, ulong Epoch, ulong BootstrapCompleted, ulong BootstrapCopied, int ReadinessRecordIndex)
    {
        public bool FullProfile { get; init; }
        public bool Truncated { get; init; }
        public IReadOnlyList<DiskStaticFault> Faults { get; init; } = Array.Empty<DiskStaticFault>();
        public bool CheckedDeviceBytesOrHttp => false;
        public bool GrantsOriginalExecutionAuthority => false;
    }
}
