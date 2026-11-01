using OS.Pal;
using OS.Std.Text;

namespace OS.Kernel.Runtime;
public readonly struct NativeAppExitStatusRecord
{
    public NativeAppExitStatusRecord(int exitCode, OSHostStatus status, bool launcherFailure)
    {
        ExitCode = exitCode;
        Status = status;
        LauncherFailure = launcherFailure;
    }

    public int ExitCode { get; }
    public OSHostStatus Status { get; }
    public bool LauncherFailure { get; }
}
