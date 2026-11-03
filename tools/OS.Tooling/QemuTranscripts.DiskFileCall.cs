using System.Globalization;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private readonly record struct DiskFileCall(ulong Number, ulong A0, ulong A1, ulong A2, ulong A3, long Value, bool Pending, bool Request, uint OutputBytes);
}
