using System.Diagnostics;

internal static partial class HostToolProcess
{
    internal readonly record struct ToolResult(int ExitCode, string Output, string Error);
}
