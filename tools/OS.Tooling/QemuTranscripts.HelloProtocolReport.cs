using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Value-only protocol accounting, without execution or input authority.</summary>

    public readonly record struct HelloProtocolReport(int Calls, int Irqs, int Drains, int Transfers);
}
