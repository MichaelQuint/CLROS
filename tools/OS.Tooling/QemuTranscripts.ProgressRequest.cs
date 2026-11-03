using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;
using Race = OS.Tooling.LinuxDiskRaceProofFixture;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed class ProgressRequest(LinuxDiskRaceWindow window, LinuxDiskRaceCall call, bool metadata, ulong offset, uint bytes, ulong[] sectors)
    {
        internal readonly LinuxDiskRaceWindow Window = window;
        internal readonly LinuxDiskRaceCall Call = call;
        internal readonly bool Metadata = metadata;
        internal readonly ulong Offset = offset;
        internal readonly uint Bytes = bytes;
        internal readonly ulong[] Sectors = sectors;
        internal readonly List<ulong> Observed = [], Generations = [];
    }
}
