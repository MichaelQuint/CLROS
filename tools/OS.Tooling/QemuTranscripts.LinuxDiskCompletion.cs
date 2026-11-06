using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public readonly record struct LinuxDiskCompletion(int CallRecordIndex, int CompletionRecordIndex, ulong Ordinal, ulong Sequence, ulong RequestGeneration, uint OutputBytes, long Value);
}
