using System.Diagnostics;
using System.Runtime.CompilerServices;
using OS.Kernel.Build;

internal static partial class ManifestToolTests
{
#if OS_PROFILE_SINGLE_MINIMAL_API


#elif OS_PROFILE_MULTI_MINIMAL_API








#else
#error ManifestToolTests require one OS workload profile symbol.
#endif
    private sealed class ToolResult
    {
        public ToolResult(int exitCode, string output)
        {
            this.ExitCode = exitCode;
            this.Output = output;
        }

        public int ExitCode { get; }
        public string Output { get; }
    }
}
