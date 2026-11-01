namespace OS.Boot;
public enum UefiExitBootServicesAttemptOutcome
{
    ExitSucceeded = 0,
    MapKeyRejected = 1,
    FirmwareError = 2,
    ExitCallSkipped = 3
}
