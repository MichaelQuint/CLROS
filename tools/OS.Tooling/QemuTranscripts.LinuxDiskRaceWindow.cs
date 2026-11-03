using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    public readonly record struct LinuxDiskRaceWindow(int CallRecordIndex, int CompletionRecordIndex, ulong Identity, ulong Ordinal, ulong Sequence, ulong RequestGeneration, uint Source, long Value, uint OutputBytes, uint Completion = 1);
}
