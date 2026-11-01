namespace OS.Pal;
public readonly struct OSHostSocketPollResult
{
    public readonly OSHostStatus Status;
    public readonly uint ObservedEvents;
    public readonly bool IsInitialized;
    public OSHostSocketPollResult(OSHostStatus status, uint observedEvents, bool isInitialized = true)
    {
        Status = status;
        ObservedEvents = observedEvents;
        IsInitialized = isInitialized;
    }

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;
}
