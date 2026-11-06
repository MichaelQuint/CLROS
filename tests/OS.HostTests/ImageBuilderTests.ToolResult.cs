using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Manifest;
using OS.Storage;

internal static partial class ImageBuilderTests
{
    private readonly struct ToolResult
    {
        public ToolResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        public int ExitCode
        {
            get;
        }
        public string Output
        {
            get;
        }
    }
}
