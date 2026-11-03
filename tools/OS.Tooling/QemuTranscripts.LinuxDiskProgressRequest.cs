using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied request identity and independently extracted sector
    /// coverage. Device generations belong to this entire call/wait window;
    /// the immutable values confer no execution or live ownership authority.</summary>

    public readonly record struct LinuxDiskProgressRequest(ulong Identity, ulong Ordinal, ulong Sequence, ulong RequestGeneration, string Role, ulong Offset, uint Bytes, IReadOnlyList<ulong> Sectors, IReadOnlyList<ulong> DeviceGenerations);
}
