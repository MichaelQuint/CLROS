using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.Text;
using System.Runtime.ExceptionServices;
using OS.Tooling.Native;

namespace OS.Tooling;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool OutputLimitExceeded, bool Cancelled, TimeSpan Duration)
{
    public bool TerminationConfirmed { get; init; } = true;
    /// <summary>Stderr went to a caller-owned inherited handle rather than the
    /// text collector. Empty StandardError therefore grants no stderr proof.</summary>

    public bool StandardErrorExternallyOwned
    {
        get; init;
    }
    public string FailureReason { get; init; } = "";
    public bool Succeeded => ExitCode == 0 && !TimedOut && !OutputLimitExceeded && !Cancelled && TerminationConfirmed && FailureReason.Length == 0;
}
