using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Derived accounting only, without execution or input authority.</summary>

    public readonly record struct ThreadingProtocolReport(int Calls, int Clones, int Reaped, int Irqs, int Drains, int Ticks, int Transfers, int SignalReturns);
}
