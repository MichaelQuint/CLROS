namespace OS.Drivers;

public readonly struct VirtioDeviceInitializationPlanResult
{
    private VirtioDeviceInitializationPlanResult(VirtioDeviceInitializationPlanStatus status, ulong deviceFeatures, ulong requiredFeatures, ulong optionalFeatures, ulong selectedFeatures, ulong missingRequiredFeatures, ushort availableQueueCount, ushort requiredQueueCount, VirtioDeviceStatusWriteSequence statusWrites, bool isInitialized)
    {
        Status = status;
        DeviceFeatures = deviceFeatures;
        RequiredFeatures = requiredFeatures;
        OptionalFeatures = optionalFeatures;
        SelectedFeatures = selectedFeatures;
        MissingRequiredFeatures = missingRequiredFeatures;
        AvailableQueueCount = availableQueueCount;
        RequiredQueueCount = requiredQueueCount;
        StatusWrites = statusWrites;
        IsInitialized = isInitialized;
    }

    public VirtioDeviceInitializationPlanStatus Status
    {
        get;
    }
    public ulong DeviceFeatures
    {
        get;
    }
    public ulong RequiredFeatures
    {
        get;
    }
    public ulong OptionalFeatures
    {
        get;
    }
    public ulong SelectedFeatures
    {
        get;
    }
    public ulong MissingRequiredFeatures
    {
        get;
    }
    public ushort AvailableQueueCount
    {
        get;
    }
    public ushort RequiredQueueCount
    {
        get;
    }
    public VirtioDeviceStatusWriteSequence StatusWrites
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioDeviceInitializationPlanStatus.Success;

    public static VirtioDeviceInitializationPlanResult Create(VirtioDeviceInitializationPlanStatus status, ulong deviceFeatures, ulong requiredFeatures, ulong optionalFeatures, ulong selectedFeatures, ulong missingRequiredFeatures, ushort availableQueueCount, ushort requiredQueueCount, VirtioDeviceStatusWriteSequence statusWrites)
    {
        return new VirtioDeviceInitializationPlanResult(status, deviceFeatures, requiredFeatures, optionalFeatures, selectedFeatures, missingRequiredFeatures, availableQueueCount, requiredQueueCount, statusWrites, isInitialized: true);
    }
}
