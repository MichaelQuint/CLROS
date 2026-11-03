namespace OS.Kernel.Compatibility.LinuxX64;

public readonly struct LinuxSyscallResult
{
    public readonly LinuxSyscallAction Action;
    public readonly long Value;
    public readonly uint ExitCode;
    /// <summary>Result from a separately admitted service. Blocked/ThreadExited
    /// require native cleanup before the scheduler parks/reaps its kernel worker.
    /// Yielded is exclusively syscall24: the native owner must capture and
    /// enqueue the current worker through the real FIFO before resuming it.
    /// RestoreUser is exclusively a validated syscall15 return; the native owner
    /// must install its complete saved GPR/FX image, not just Frame/Value.</summary>

    public LinuxSyscallResult(LinuxSyscallAction action, long value = 0, uint exitCode = 0)
    {
        Action = action;
        Value = value;
        ExitCode = exitCode;
    }
}
