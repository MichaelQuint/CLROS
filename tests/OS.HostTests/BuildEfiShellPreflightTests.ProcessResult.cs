using OS.Tooling;
using OS.Tooling.Native;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal static partial class BuildEfiShellPreflightTests
{
    private readonly struct ProcessResult(int exitCode, string output, string logPath)
    {
        public int ExitCode { get; } = exitCode;
        public string Output { get; } = output;
        public string LogPath { get; } = logPath;
    }
}
