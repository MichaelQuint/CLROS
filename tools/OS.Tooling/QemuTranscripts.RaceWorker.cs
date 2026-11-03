using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class RaceWorker(IReadOnlyList<Race.CallSpec> plan, ulong identity, ulong stack, ulong fs)
    {
        internal readonly IReadOnlyList<Race.CallSpec> Plan = plan;
        internal readonly ulong Identity = identity, Stack = stack, Fs = fs;
        internal bool Created, Exited, Reaped;
        internal int Calls, Ordinal;
        internal ulong Sequence;
        internal RacePending? Pending;
    }
}
