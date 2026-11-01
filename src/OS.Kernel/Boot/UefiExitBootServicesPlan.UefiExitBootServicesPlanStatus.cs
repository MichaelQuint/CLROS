namespace OS.Boot;
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
