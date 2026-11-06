using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockDescriptorOwnershipStatus
{
    Success = 0,
    PublishFailed = 1,
    CompletionFailed = 2,
    StateTableTooSmall = 3,
    DescriptorAlreadyInFlight = 4,
    DescriptorNotInFlight = 5,
    DuplicateCompletion = 6
}
