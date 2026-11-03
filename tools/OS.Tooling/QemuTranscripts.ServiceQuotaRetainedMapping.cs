using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>One still-retained original1MiB mapping and exact successful CALL
    /// identity. CallRecordIndex addresses original normalized rows.</summary>

    public readonly record struct ServiceQuotaRetainedMapping(int Index, ulong Address, ulong Bytes, int CallRecordIndex, ulong Identity, ulong Ordinal);
}
