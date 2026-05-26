using OS.Kernel.Platform;

namespace OS.Boot;

public static class UefiExitBootServicesAttemptMapper
{
    public static UefiExitBootServicesAttemptOutcome ToAttemptOutcome(
        PlatformFirmwareExitBootServicesResult firmwareResult)
    {
        return firmwareResult.Status switch
        {
            PlatformFirmwareExitBootServicesStatus.Success =>
                UefiExitBootServicesAttemptOutcome.ExitSucceeded,
            PlatformFirmwareExitBootServicesStatus.MapKeyRejected =>
                UefiExitBootServicesAttemptOutcome.MapKeyRejected,
            _ => UefiExitBootServicesAttemptOutcome.FirmwareError
        };
    }
}
