using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record CollectionSignal(ulong Identity, int Parked, bool Preempted);
}
