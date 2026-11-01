namespace OS.Boot;
public static class UefiExitBootServicesPlan
{
    public const uint DefaultMaximumRetryCount = 3;
    public static UefiExitBootServicesPlanResult Evaluate(BootHandoffRecord handoffRecord, bool freshMemoryMapCaptured, UefiExitBootServicesAttemptOutcome attemptOutcome, uint attemptIndex, uint maximumRetryCount)
    {
        EfiBootHandoffStatus handoffStatus = EfiBootHandoffValidator.Validate(handoffRecord);
        if (handoffStatus != EfiBootHandoffStatus.Success)
        {
            return FailForHandoff(handoffStatus, attemptIndex, maximumRetryCount);
        }

        if (!freshMemoryMapCaptured)
        {
            return UefiExitBootServicesPlanResult.Create(UefiExitBootServicesPlanStatus.FreshMemoryMapRequired, handoffStatus, shouldRetry: false, attemptIndex, maximumRetryCount);
        }

        if (attemptOutcome == UefiExitBootServicesAttemptOutcome.ExitSucceeded)
        {
            return UefiExitBootServicesPlanResult.Create(UefiExitBootServicesPlanStatus.Success, handoffStatus, shouldRetry: false, attemptIndex, maximumRetryCount);
        }

        if (attemptOutcome == UefiExitBootServicesAttemptOutcome.ExitCallSkipped)
        {
            return UefiExitBootServicesPlanResult.Create(UefiExitBootServicesPlanStatus.ExitCallSkipped, handoffStatus, shouldRetry: false, attemptIndex, maximumRetryCount);
        }

        if (attemptOutcome == UefiExitBootServicesAttemptOutcome.MapKeyRejected)
        {
            bool shouldRetry = attemptIndex < maximumRetryCount;
            return UefiExitBootServicesPlanResult.Create(shouldRetry ? UefiExitBootServicesPlanStatus.RetryRequired : UefiExitBootServicesPlanStatus.RetryExhausted, handoffStatus, shouldRetry, attemptIndex, maximumRetryCount);
        }

        return UefiExitBootServicesPlanResult.Create(UefiExitBootServicesPlanStatus.FirmwareExitFailed, handoffStatus, shouldRetry: false, attemptIndex, maximumRetryCount);
    }

    public static UefiExitBootServicesPlanResult Evaluate(BootHandoffRecord handoffRecord, bool freshMemoryMapCaptured, UefiExitBootServicesAttemptOutcome attemptOutcome, uint attemptIndex)
    {
        return Evaluate(handoffRecord, freshMemoryMapCaptured, attemptOutcome, attemptIndex, DefaultMaximumRetryCount);
    }

    private static UefiExitBootServicesPlanResult FailForHandoff(EfiBootHandoffStatus handoffStatus, uint attemptIndex, uint maximumRetryCount)
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
        return UefiExitBootServicesPlanResult.Create(status, handoffStatus, shouldRetry: false, attemptIndex, maximumRetryCount);
    }
}
