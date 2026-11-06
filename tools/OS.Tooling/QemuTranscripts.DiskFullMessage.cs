using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed record DiskFullMessage(StaticHttpMessage Message, int ResponseOffset);
}
