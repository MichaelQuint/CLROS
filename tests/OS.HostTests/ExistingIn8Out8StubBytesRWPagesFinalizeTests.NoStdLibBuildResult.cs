using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static unsafe partial class ExistingIn8Out8StubBytesRWPagesFinalizeTests
{
    private readonly struct NoStdLibBuildResult
    {
        internal NoStdLibBuildResult(int exitCode, bool timedOut, string output)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
        }

        internal int ExitCode
        {
            get;
        }
        internal bool TimedOut
        {
            get;
        }
        internal string Output
        {
            get;
        }
    }
}
