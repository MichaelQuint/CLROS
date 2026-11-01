namespace OS.Pal;
public readonly struct OSHostThreadJoinResult
{
    public readonly OSHostStatus Status;
    public readonly int ExitCode;
    public readonly bool IsInitialized;
    public OSHostThreadJoinResult(OSHostStatus status, int exitCode, bool isInitialized = true)
    {
        Status = status;
        ExitCode = exitCode;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostThreadJoinResult FromStatus(OSHostStatus status)
    {
        return new OSHostThreadJoinResult(status, exitCode: 0);
    }
}
