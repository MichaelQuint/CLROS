using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotRuntimePayload
{
    private sealed record Command(string Executable, string[] Arguments, string Log, int ExitCode);
}
