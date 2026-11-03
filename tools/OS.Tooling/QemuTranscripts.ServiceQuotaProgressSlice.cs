using System.Text;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>One clipped stderr marker span in a genuine original fd2 write
    /// and its following successful write/writev CALL. Offsets/counts are bytes
    /// in reassembled fd2; row indices address original normalized serial rows.</summary>

    public readonly record struct ServiceQuotaProgressSlice(int WriteRecordIndex, int CallRecordIndex, ulong Identity, ulong Ordinal, int ByteOffset, int ByteCount);
}
