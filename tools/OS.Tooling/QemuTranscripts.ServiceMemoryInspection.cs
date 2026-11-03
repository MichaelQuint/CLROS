using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied post-dispatch backing observations. Ordinary peak excludes
    /// deliberate pressure and includes its starting backing. Conservative bounds
    /// remove only still-owned backing in the pinned GC arena; all other backing
    /// remains non-GC and can overestimate demand. Native failures
    /// describe the two probe-shaped mmap requests. A physical shortfall does
    /// not identify the sole failing guard; other causes can coexist.
    /// Values confer no allocation, native-review or guest execution authority.</summary>

    public readonly record struct ServiceMemoryInspection(int OperationSnapshots, ulong OrdinaryPeakBackedBytes, ulong MaximumRegionCount, int NativePhysicalShortfalls, int NativeUnclassifiedFailures, ulong ConservativePoolBytes, ulong ConservativeEntitlementBytes, bool ConservativeHeadroomFits, ulong OrdinaryNonGcUpperBoundBytes = 0, bool NativeReserveFits = false, ServiceGcBackingInspection? GcArena = null);
}
