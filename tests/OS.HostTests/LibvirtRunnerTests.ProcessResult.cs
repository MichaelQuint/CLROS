using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static partial class LibvirtRunnerTests
{
    private readonly struct ProcessResult
    {
        public ProcessResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        public int ExitCode
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
    }
}
