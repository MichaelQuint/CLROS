namespace OS.Drivers;
public enum VirtioFeatureStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    UnsupportedRequested = 2,
    MissingVersion1 = 3,
    InvalidStatus = 4,
    FeaturesRejected = 5,
    DeviceFailed = 6,
    MissingReceiveBuffers = 7
}
