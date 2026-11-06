using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public readonly record struct LinuxDiskRaceCall(int RecordIndex, ulong Identity, ulong Ordinal, Race.CallSpec Spec);
}
