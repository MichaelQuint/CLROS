using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Kernel.Build;

internal static partial class KestrelSmokePackageContractTests
{
    private sealed class ToolResult
    {
        public ToolResult(int exitCode, string output)
        {
            this.ExitCode = exitCode;
            this.Output = output;
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
