namespace OS.Tools.QemuRunner;
public readonly struct AppExitMarkerResult
{
    public AppExitMarkerResult(int exitCode, string status)
    {
        ExitCode = exitCode;
        Status = status;
    }

    public int ExitCode { get; }
    public string Status { get; }
}
