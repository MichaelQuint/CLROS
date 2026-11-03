namespace OS.Drivers;

public enum VirtioNetTransmitDescriptorOwnershipStatus
{
    Success = 0,
    TransmitPublishFailed = 1,
    CompletionPlanFailed = 2,
    StateTableTooSmall = 3,
    DescriptorAlreadyInFlight = 4,
    DescriptorNotInFlight = 5,
    DuplicateCompletion = 6
}
