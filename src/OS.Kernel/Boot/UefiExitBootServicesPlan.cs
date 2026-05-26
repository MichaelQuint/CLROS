namespace OS.Boot;

public enum UefiExitBootServicesAttemptOutcome
{
    ExitSucceeded = 0,
    MapKeyRejected = 1,
    FirmwareError = 2,
    ExitCallSkipped = 3
}

public enum UefiExitBootServicesPlanStatus
{
    Success = 0,
    FreshMemoryMapRequired = 1,
    RetryRequired = 2,
    RetryExhausted = 3,
    MissingOwnedMemoryMap = 4,
    InvalidDescriptorMetadata = 5,
    InvalidHandoff = 6,
    FirmwareExitFailed = 7,
    ExitCallSkipped = 8
}

public readonly struct UefiExitBootServicesPlanResult
{
    private UefiExitBootServicesPlanResult(
        UefiExitBootServicesPlanStatus status,
        EfiBootHandoffStatus handoffStatus,
        bool shouldRetry,
        uint attemptIndex,
        uint maximumRetryCount)
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

    public static UefiExitBootServicesPlanResult Create(
        UefiExitBootServicesPlanStatus status,
        EfiBootHandoffStatus handoffStatus,
        bool shouldRetry,
        uint attemptIndex,
        uint maximumRetryCount)
    {
        return new UefiExitBootServicesPlanResult(
            status,
            handoffStatus,
            shouldRetry,
            attemptIndex,
            maximumRetryCount);
    }
}

public static class UefiExitBootServicesPlan
{
    public const uint DefaultMaximumRetryCount = 3;

    public static UefiExitBootServicesPlanResult Evaluate(
        BootHandoffRecord handoffRecord,
        bool freshMemoryMapCaptured,
        UefiExitBootServicesAttemptOutcome attemptOutcome,
        uint attemptIndex,
        uint maximumRetryCount)
    {
        EfiBootHandoffStatus handoffStatus = EfiBootHandoffValidator.Validate(handoffRecord);
        if (handoffStatus != EfiBootHandoffStatus.Success)
        {
            return FailForHandoff(handoffStatus, attemptIndex, maximumRetryCount);
        }

        if (!freshMemoryMapCaptured)
        {
            return UefiExitBootServicesPlanResult.Create(
                UefiExitBootServicesPlanStatus.FreshMemoryMapRequired,
                handoffStatus,
                shouldRetry: false,
                attemptIndex,
                maximumRetryCount);
        }

        if (attemptOutcome == UefiExitBootServicesAttemptOutcome.ExitSucceeded)
        {
            return UefiExitBootServicesPlanResult.Create(
                UefiExitBootServicesPlanStatus.Success,
                handoffStatus,
                shouldRetry: false,
                attemptIndex,
                maximumRetryCount);
        }

        if (attemptOutcome == UefiExitBootServicesAttemptOutcome.ExitCallSkipped)
        {
            return UefiExitBootServicesPlanResult.Create(
                UefiExitBootServicesPlanStatus.ExitCallSkipped,
                handoffStatus,
                shouldRetry: false,
                attemptIndex,
                maximumRetryCount);
        }

        if (attemptOutcome == UefiExitBootServicesAttemptOutcome.MapKeyRejected)
        {
            bool shouldRetry = attemptIndex < maximumRetryCount;
            return UefiExitBootServicesPlanResult.Create(
                shouldRetry
                    ? UefiExitBootServicesPlanStatus.RetryRequired
                    : UefiExitBootServicesPlanStatus.RetryExhausted,
                handoffStatus,
                shouldRetry,
                attemptIndex,
                maximumRetryCount);
        }

        return UefiExitBootServicesPlanResult.Create(
            UefiExitBootServicesPlanStatus.FirmwareExitFailed,
            handoffStatus,
            shouldRetry: false,
            attemptIndex,
            maximumRetryCount);
    }

    public static UefiExitBootServicesPlanResult Evaluate(
        BootHandoffRecord handoffRecord,
        bool freshMemoryMapCaptured,
        UefiExitBootServicesAttemptOutcome attemptOutcome,
        uint attemptIndex)
    {
        return Evaluate(
            handoffRecord,
            freshMemoryMapCaptured,
            attemptOutcome,
            attemptIndex,
            DefaultMaximumRetryCount);
    }

    private static UefiExitBootServicesPlanResult FailForHandoff(
        EfiBootHandoffStatus handoffStatus,
        uint attemptIndex,
        uint maximumRetryCount)
    {
        UefiExitBootServicesPlanStatus status = handoffStatus switch
        {
            EfiBootHandoffStatus.MissingMemoryMap => UefiExitBootServicesPlanStatus.MissingOwnedMemoryMap,
            EfiBootHandoffStatus.MemoryMapNotOwned => UefiExitBootServicesPlanStatus.MissingOwnedMemoryMap,
            EfiBootHandoffStatus.MissingSourceMemoryMap => UefiExitBootServicesPlanStatus.MissingOwnedMemoryMap,
            EfiBootHandoffStatus.InvalidDescriptorSize => UefiExitBootServicesPlanStatus.InvalidDescriptorMetadata,
            EfiBootHandoffStatus.InvalidDescriptorVersion => UefiExitBootServicesPlanStatus.InvalidDescriptorMetadata,
            EfiBootHandoffStatus.InvalidDescriptorCount => UefiExitBootServicesPlanStatus.InvalidDescriptorMetadata,
            EfiBootHandoffStatus.DescriptorSizeOverflow => UefiExitBootServicesPlanStatus.InvalidDescriptorMetadata,
            _ => UefiExitBootServicesPlanStatus.InvalidHandoff
        };

        return UefiExitBootServicesPlanResult.Create(
            status,
            handoffStatus,
            shouldRetry: false,
            attemptIndex,
            maximumRetryCount);
    }
}
