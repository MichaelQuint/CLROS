using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;

internal static unsafe partial class M01LivePagingIntegrationTests
{
    private readonly struct NoStdLibBuildResult
    {
        public NoStdLibBuildResult(int exitCode, bool timedOut, string output)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
        }

        public int ExitCode { get; }
        public bool TimedOut { get; }
        public string Output { get; }
    }
}
