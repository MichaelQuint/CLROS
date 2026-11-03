using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static partial class X64BootIdentityMappingPlanTests
{
    private readonly struct NoStdLibBuildResult
    {
        public NoStdLibBuildResult(int exitCode, bool timedOut, string output, byte[] assembly)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
            Assembly = assembly;
        }

        public int ExitCode { get; }
        public bool TimedOut { get; }
        public string Output { get; }
        public byte[] Assembly { get; }
        public byte[] AssemblyBytes => Assembly;
    }
}
