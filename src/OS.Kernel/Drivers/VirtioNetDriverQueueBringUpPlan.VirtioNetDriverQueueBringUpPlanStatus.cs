namespace OS.Drivers;

using OS.Net;

public enum VirtioNetDriverQueueBringUpPlanStatus
{
    Success = 0,
    BindingPlanFailed = 1,
    DescriptorNotNetwork = 2,
    MissingNetworkConfiguration = 3,
    InitializationProfileFailed = 4,
    FeatureProfileMismatch = 5,
    UnsupportedSelectedFeatures = 6,
    QueuePlanFailed = 7,
    RxQueueMemoryTooSmall = 8,
    TxQueueMemoryTooSmall = 9,
    RxBufferCapacityTooSmall = 10,
    TxBufferCapacityTooSmall = 11,
    ByteCountOverflow = 12
}
