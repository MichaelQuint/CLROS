using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

internal static partial class HostTestRunner
{
    internal sealed record FixtureResult(string Id, string Lane, int ExitCode, bool TimedOut, double Seconds, string Log, int Started, int Passed, int Failed, string? Error)
    {
        public bool Reused
        {
            get; init;
        }
        public string Origin { get; init; } = "";
        public int ProcessId
        {
            get; init;
        }
    }
}
