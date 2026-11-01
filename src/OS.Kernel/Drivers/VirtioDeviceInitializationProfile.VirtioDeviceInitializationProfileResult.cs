namespace OS.Drivers;
public readonly struct VirtioDeviceInitializationProfileResult
{
    private VirtioDeviceInitializationProfileResult(VirtioDeviceInitializationProfileStatus status, VirtioPciDeviceKind deviceKind, ulong deviceFeatures, ulong requiredFeatures, ulong optionalFeatures, ushort availableQueueCount, ushort requiredQueueCount, bool hasInitializationPlan, VirtioDeviceInitializationPlanResult initializationPlan, bool isInitialized)
    {
        Status = status;
        DeviceKind = deviceKind;
        DeviceFeatures = deviceFeatures;
        RequiredFeatures = requiredFeatures;
        OptionalFeatures = optionalFeatures;
        AvailableQueueCount = availableQueueCount;
        RequiredQueueCount = requiredQueueCount;
        HasInitializationPlan = hasInitializationPlan;
        InitializationPlan = initializationPlan;
        IsInitialized = isInitialized;
    }

    public VirtioDeviceInitializationProfileStatus Status { get; }
    public VirtioPciDeviceKind DeviceKind { get; }
    public ulong DeviceFeatures { get; }
    public ulong RequiredFeatures { get; }
    public ulong OptionalFeatures { get; }
    public ushort AvailableQueueCount { get; }
    public ushort RequiredQueueCount { get; }
    public bool HasInitializationPlan { get; }
    public VirtioDeviceInitializationPlanResult InitializationPlan { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioDeviceInitializationProfileStatus.Success;

    internal static VirtioDeviceInitializationProfileResult Create(VirtioDeviceInitializationProfileStatus status, VirtioPciDeviceKind deviceKind, ulong deviceFeatures, ulong requiredFeatures, ulong optionalFeatures, ushort availableQueueCount, ushort requiredQueueCount, bool hasInitializationPlan, VirtioDeviceInitializationPlanResult initializationPlan)
    {
        return new VirtioDeviceInitializationProfileResult(status, deviceKind, deviceFeatures, requiredFeatures, optionalFeatures, availableQueueCount, requiredQueueCount, hasInitializationPlan, initializationPlan, isInitialized: true);
    }
}
