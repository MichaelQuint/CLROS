namespace OS.Boot;

public readonly struct BootResult
{
    private BootResult(bool succeeded, string reason)
    {
        Succeeded = succeeded;
        Reason = reason;
    }

    public bool Succeeded { get; }

    public string Reason { get; }

    public static BootResult Pass()
    {
        return new BootResult(true, string.Empty);
    }

    public static BootResult Fail(string reason)
    {
        return new BootResult(false, reason);
    }
}
