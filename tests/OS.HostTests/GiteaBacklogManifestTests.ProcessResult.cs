using OS.Tooling;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class GiteaBacklogManifestTests
{
    private readonly struct ProcessResult(int exitCode, string output)
    {
        internal int ExitCode { get; } = exitCode;
        internal string Output { get; } = output;
    }
}
