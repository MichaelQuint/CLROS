namespace OS.Drivers;

public enum VirtioPciBindingPlanStatus
{
    Success = 0,
    HeaderParseFailed = 1,
    CapabilityParseFailed = 2,
    DiscoveryFailed = 3,
    BarDecodeFailed = 4,
    ApertureValidationFailed = 5
}
