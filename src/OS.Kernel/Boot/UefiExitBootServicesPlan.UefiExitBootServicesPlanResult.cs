namespace OS.Boot;
public readonly struct UefiExitBootServicesPlanResult
{
    private UefiExitBootServicesPlanResult(UefiExitBootServicesPlanStatus status, EfiBootHandoffStatus handoffStatus, bool shouldRetry, uint attemptIndex, uint maximumRetryCount)
    {
        Status = status;
        HandoffStatus = handoffStatus;
        ShouldRetry = shouldRetry;
        AttemptIndex = attemptIndex;
        MaximumRetryCount = maximumRetryCount;
    }

    public UefiExitBootServicesPlanStatus Status { get; }
    public EfiBootHandoffStatus HandoffStatus { get; }
    public bool Succeeded => Status == UefiExitBootServicesPlanStatus.Success;
    public bool ShouldRetry { get; }
    public uint AttemptIndex { get; }
    public uint MaximumRetryCount { get; }

    public static UefiExitBootServicesPlanResult Create(UefiExitBootServicesPlanStatus status, EfiBootHandoffStatus handoffStatus, bool shouldRetry, uint attemptIndex, uint maximumRetryCount)
    {
        return new UefiExitBootServicesPlanResult(status, handoffStatus, shouldRetry, attemptIndex, maximumRetryCount);
    }
}
