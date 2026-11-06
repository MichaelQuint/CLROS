using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static partial class SourceFirewallTests
{
    private readonly struct ProcessResult(int exitCode, string output)
    {
        public int ExitCode { get; } = exitCode;
        public string Output { get; } = output;
    }
}
