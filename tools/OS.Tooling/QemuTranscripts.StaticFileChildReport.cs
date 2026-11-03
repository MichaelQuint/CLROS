using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied lifetime of the configured emulator/capture child. A
    /// wrapper ID is not the underlying emulator ID; original capture authority
    /// remains separately mandatory. No guest execution authority is granted.</summary>

    public readonly record struct StaticFileChildReport(int ProcessId, long LifetimeMilliseconds);
}
