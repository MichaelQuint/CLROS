namespace OS.Pal;
public readonly struct OSHostEventCreateResult
{
    public readonly OSHostStatus Status;
    public readonly OSHostHandle Waitable;
    public OSHostEventCreateResult(OSHostStatus status, OSHostHandle waitable)
    {
        Status = status;
        Waitable = waitable;
    }

    public bool Succeeded => Status == OSHostStatus.Ok && Waitable.IsValid;

    public static OSHostEventCreateResult FromStatus(OSHostStatus status)
    {
        return new OSHostEventCreateResult(status, default);
    }
}
