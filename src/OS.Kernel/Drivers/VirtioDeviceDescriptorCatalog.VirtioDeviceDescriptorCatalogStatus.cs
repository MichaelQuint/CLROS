namespace OS.Drivers;

using OS.Storage;

public enum VirtioDeviceDescriptorCatalogStatus
{
    Success = 0,
    MissingDeviceConfiguration = 1,
    UnsupportedDeviceKind = 2,
    ConfigParseFailed = 3,
    InitializationProfileFailed = 4,
    DestinationTooSmall = 5,
    BindingPlanFailed = 6
}
