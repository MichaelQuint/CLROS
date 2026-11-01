namespace OS.Kernel.Diagnostics;
public readonly struct PanicContext
{
    public PanicContext(string? subsystem, string? reason, int? vector)
    {
        Subsystem = subsystem;
        Reason = reason;
        Vector = vector;
    }

    public string? Subsystem { get; }
    public string? Reason { get; }
    public int? Vector { get; }
}
