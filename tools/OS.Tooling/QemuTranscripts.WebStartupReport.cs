using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only startup/writer observations. No execution, listening,
    /// package, scheduler, native or external HTTP authority follows from this report.</summary>

    public readonly record struct WebStartupReport(ulong WriterIdentity, ulong BindIdentity, int StartupWriteRecords);
}
