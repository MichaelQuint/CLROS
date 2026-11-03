namespace OS.Drivers;

public enum VirtioPciBindingCatalogStatus
{
    Success = 0,
    MissingConfigurationSource = 1,
    MissingBarSizeSource = 2,
    BindingFailed = 3,
    DestinationTooSmall = 4
}
