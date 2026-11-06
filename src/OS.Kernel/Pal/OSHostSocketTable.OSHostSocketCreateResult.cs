namespace OS.Pal;

public readonly struct OSHostSocketCreateResult
{
    public readonly OSHostStatus Status;
    public readonly OSHostSocketHandle Socket;
    public OSHostSocketCreateResult(OSHostStatus status, OSHostSocketHandle socket)
    {
        Status = status;
        Socket = socket;
    }

    public bool Succeeded => Status == OSHostStatus.Ok && Socket.IsValid;

    public static OSHostSocketCreateResult FromStatus(OSHostStatus status)
    {
        return new OSHostSocketCreateResult(status, default);
    }
}
