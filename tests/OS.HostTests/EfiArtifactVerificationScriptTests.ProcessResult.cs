using System.Diagnostics;
using OS.Tooling;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static partial class EfiArtifactVerificationScriptTests
{
    internal readonly struct ProcessResult(int exitCode, string output, bool timedOut, long elapsedMilliseconds)
    {
        public int ExitCode { get; } = exitCode;
        public string Output { get; } = output;
        public bool TimedOut { get; } = timedOut;
        public long ElapsedMilliseconds { get; } = elapsedMilliseconds;
    }
}
