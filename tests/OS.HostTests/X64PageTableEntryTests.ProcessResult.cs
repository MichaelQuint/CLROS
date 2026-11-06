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
using OS.Kernel.Memory;

internal static partial class X64PageTableEntryTests
{
    private readonly struct ProcessResult
    {
        public ProcessResult(int exitCode, bool timedOut, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public int ExitCode
        {
            get;
        }
        public bool TimedOut
        {
            get;
        }
        public string StandardOutput
        {
            get;
        }
        public string StandardError
        {
            get;
        }
        public string Output => StandardOutput + Environment.NewLine + StandardError;
    }
}
