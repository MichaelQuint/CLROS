using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied callbacks and actual denied/EIO results at the
    /// first joined EIO. Later rejected calls are still validated. Borrowed text
    /// grants no execution, package, runner, native or HTTP authority.</summary>

    public readonly record struct WebLogQuotaReport(uint CopiedRecords, uint CopiedBytes, uint StdoutRecords, uint StderrRecords, uint DeniedCalls, uint EioCalls);
}
