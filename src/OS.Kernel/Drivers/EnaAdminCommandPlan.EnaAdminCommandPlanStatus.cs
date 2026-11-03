namespace OS.Drivers;

public enum EnaAdminCommandPlanStatus
{
    Success = 0,
    CommandIdOutOfRange = 1,
    UnsupportedFeature = 2,
    TimeoutBudgetTooSmall = 3,
    SubmissionBufferTooSmall = 4,
    CompletionBufferTooSmall = 5,
    DescriptorWriteFailed = 6,
    CompletionCommandIdMismatch = 7,
    CompletionPhaseMismatch = 8,
    CompletionStatusNotSuccess = 9,
    DeviceAttributesRejected = 10
}
