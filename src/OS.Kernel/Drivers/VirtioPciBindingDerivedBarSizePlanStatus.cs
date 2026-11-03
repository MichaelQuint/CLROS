namespace OS.Drivers;

public enum VirtioPciBindingDerivedBarSizePlanStatus
{
    Success = 0,
    HeaderParseFailed = 1,
    BarDecodeFailed = 2,
    DerivedSizeFailed = 3,
    SizeDestinationTooSmall = 4,
    BindingFailed = 5
}
