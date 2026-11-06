using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

internal static partial class QemuRunnerTests
{
    private readonly struct ProcessResult
    {
        public ProcessResult(int exitCode, string output)
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
