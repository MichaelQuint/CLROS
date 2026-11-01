namespace OS.Drivers;
public enum VirtioDeviceInitializationPlanStatus
{
    Success = 0,
    MissingRequiredFeatures = 1,
    FeaturesOkRejected = 2,
    InvalidRequiredQueueCount = 3,
    InsufficientQueueCount = 4
}
