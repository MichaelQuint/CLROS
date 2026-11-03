using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Manifest;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied committed owner/topology values at one actual memory
    /// return, plus still-owned GC backing replayed with the existing reviewed
    /// arena identity. Pool/backed/free/largest-run count4KiB pages; runs
    /// and regions count intervals, tables count4KiB tables, other byte/count
    /// fields retain their explicit units.
    /// No native physical allocation, caller image or guest authority follows.</summary>

    public readonly record struct ServiceQuotaMemorySnapshot(int RecordIndex, ulong Identity, ulong Ordinal, ulong Number, ulong ReservedBytes, ulong PoolPages, ulong BackedPages, ulong FreePages, ulong FreeRuns, ulong LargestFreeRunPages, ulong Regions, ulong RegionLimit, ulong Tables, ulong TableLimit, ulong GcBackedBytes);
}
