namespace OS.Drivers;
public enum VirtioNegotiationStatus : byte
{
    Uninitialized,
    Success,
    InvalidAccess,
    ReadFailed,
    WriteFailed,
    ResetTimeout,
    StatusRejected,
    UnsupportedFeatures,
    MissingVersion1,
    FeaturesRejected,
    DeviceFailed
}
