namespace OS.Drivers;

[Flags]
public enum VirtioDeviceStatus : byte
{
    None = 0,
    Acknowledge = 1,
    Driver = 2,
    DriverOk = 4,
    FeaturesOk = 8,
    Failed = 128
}

public enum VirtioDeviceInitializationPlanStatus
{
    Success = 0,
    MissingRequiredFeatures = 1,
    FeaturesOkRejected = 2,
    InvalidRequiredQueueCount = 3,
    InsufficientQueueCount = 4
}

public readonly struct VirtioDeviceStatusWriteSequence
{
    private readonly VirtioDeviceStatus _status0;
    private readonly VirtioDeviceStatus _status1;
    private readonly VirtioDeviceStatus _status2;
    private readonly VirtioDeviceStatus _status3;

    public VirtioDeviceStatusWriteSequence(
        byte count,
        VirtioDeviceStatus status0,
        VirtioDeviceStatus status1,
        VirtioDeviceStatus status2,
        VirtioDeviceStatus status3)
    {
        Count = count;
        _status0 = status0;
        _status1 = status1;
        _status2 = status2;
        _status3 = status3;
    }

    public byte Count { get; }

    public bool TryGetStatus(
        byte index,
        out VirtioDeviceStatus status)
    {
        if (index >= Count)
        {
            status = VirtioDeviceStatus.None;
            return false;
        }

        switch (index)
        {
            case 0:
                status = _status0;
                return true;

            case 1:
                status = _status1;
                return true;

            case 2:
                status = _status2;
                return true;

            case 3:
                status = _status3;
                return true;

            default:
                status = VirtioDeviceStatus.None;
                return false;
        }
    }
}

public readonly struct VirtioDeviceInitializationPlanResult
{
    private VirtioDeviceInitializationPlanResult(
        VirtioDeviceInitializationPlanStatus status,
        ulong deviceFeatures,
        ulong requiredFeatures,
        ulong optionalFeatures,
        ulong selectedFeatures,
        ulong missingRequiredFeatures,
        ushort availableQueueCount,
        ushort requiredQueueCount,
        VirtioDeviceStatusWriteSequence statusWrites,
        bool isInitialized)
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

    public VirtioDeviceInitializationPlanStatus Status { get; }

    public ulong DeviceFeatures { get; }

    public ulong RequiredFeatures { get; }

    public ulong OptionalFeatures { get; }

    public ulong SelectedFeatures { get; }

    public ulong MissingRequiredFeatures { get; }

    public ushort AvailableQueueCount { get; }

    public ushort RequiredQueueCount { get; }

    public VirtioDeviceStatusWriteSequence StatusWrites { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioDeviceInitializationPlanStatus.Success;

    public static VirtioDeviceInitializationPlanResult Create(
        VirtioDeviceInitializationPlanStatus status,
        ulong deviceFeatures,
        ulong requiredFeatures,
        ulong optionalFeatures,
        ulong selectedFeatures,
        ulong missingRequiredFeatures,
        ushort availableQueueCount,
        ushort requiredQueueCount,
        VirtioDeviceStatusWriteSequence statusWrites)
    {
        return new VirtioDeviceInitializationPlanResult(
            status,
            deviceFeatures,
            requiredFeatures,
            optionalFeatures,
            selectedFeatures,
            missingRequiredFeatures,
            availableQueueCount,
            requiredQueueCount,
            statusWrites,
            isInitialized: true);
    }
}

public static class VirtioDeviceInitializationPlan
{
    public const VirtioDeviceStatus AcknowledgeStatus =
        VirtioDeviceStatus.Acknowledge;

    public const VirtioDeviceStatus DriverStatus =
        VirtioDeviceStatus.Acknowledge | VirtioDeviceStatus.Driver;

    public const VirtioDeviceStatus FeaturesOkStatus =
        VirtioDeviceStatus.Acknowledge |
        VirtioDeviceStatus.Driver |
        VirtioDeviceStatus.FeaturesOk;

    public const VirtioDeviceStatus DriverOkStatus =
        VirtioDeviceStatus.Acknowledge |
        VirtioDeviceStatus.Driver |
        VirtioDeviceStatus.FeaturesOk |
        VirtioDeviceStatus.DriverOk;

    public const VirtioDeviceStatus FailedBeforeFeaturesOkStatus =
        VirtioDeviceStatus.Acknowledge |
        VirtioDeviceStatus.Driver |
        VirtioDeviceStatus.Failed;

    public const VirtioDeviceStatus FailedAfterFeaturesOkStatus =
        VirtioDeviceStatus.Acknowledge |
        VirtioDeviceStatus.Driver |
        VirtioDeviceStatus.FeaturesOk |
        VirtioDeviceStatus.Failed;

    public static VirtioDeviceInitializationPlanResult Create(
        ulong deviceFeatures,
        ulong requiredFeatures,
        ulong optionalFeatures,
        ushort availableQueueCount,
        ushort requiredQueueCount,
        bool featuresOkAccepted)
    {
        ulong selectedFeatures = SelectFeatures(
            deviceFeatures,
            requiredFeatures,
            optionalFeatures);
        ulong missingRequiredFeatures = requiredFeatures & ~deviceFeatures;

        if (missingRequiredFeatures != 0)
        {
            return CreateResult(
                VirtioDeviceInitializationPlanStatus.MissingRequiredFeatures,
                deviceFeatures,
                requiredFeatures,
                optionalFeatures,
                selectedFeatures,
                missingRequiredFeatures,
                availableQueueCount,
                requiredQueueCount,
                CreateFailureBeforeFeaturesOkSequence());
        }

        if (!featuresOkAccepted)
        {
            return CreateResult(
                VirtioDeviceInitializationPlanStatus.FeaturesOkRejected,
                deviceFeatures,
                requiredFeatures,
                optionalFeatures,
                selectedFeatures,
                missingRequiredFeatures,
                availableQueueCount,
                requiredQueueCount,
                CreateFeaturesOkRejectedSequence());
        }

        if (requiredQueueCount == 0)
        {
            return CreateResult(
                VirtioDeviceInitializationPlanStatus.InvalidRequiredQueueCount,
                deviceFeatures,
                requiredFeatures,
                optionalFeatures,
                selectedFeatures,
                missingRequiredFeatures,
                availableQueueCount,
                requiredQueueCount,
                CreateFailureAfterFeaturesOkSequence());
        }

        if (availableQueueCount < requiredQueueCount)
        {
            return CreateResult(
                VirtioDeviceInitializationPlanStatus.InsufficientQueueCount,
                deviceFeatures,
                requiredFeatures,
                optionalFeatures,
                selectedFeatures,
                missingRequiredFeatures,
                availableQueueCount,
                requiredQueueCount,
                CreateFailureAfterFeaturesOkSequence());
        }

        return CreateResult(
            VirtioDeviceInitializationPlanStatus.Success,
            deviceFeatures,
            requiredFeatures,
            optionalFeatures,
            selectedFeatures,
            missingRequiredFeatures,
            availableQueueCount,
            requiredQueueCount,
            CreateSuccessSequence());
    }

    private static ulong SelectFeatures(
        ulong deviceFeatures,
        ulong requiredFeatures,
        ulong optionalFeatures)
    {
        return deviceFeatures & (requiredFeatures | optionalFeatures);
    }

    private static VirtioDeviceInitializationPlanResult CreateResult(
        VirtioDeviceInitializationPlanStatus status,
        ulong deviceFeatures,
        ulong requiredFeatures,
        ulong optionalFeatures,
        ulong selectedFeatures,
        ulong missingRequiredFeatures,
        ushort availableQueueCount,
        ushort requiredQueueCount,
        VirtioDeviceStatusWriteSequence statusWrites)
    {
        return VirtioDeviceInitializationPlanResult.Create(
            status,
            deviceFeatures,
            requiredFeatures,
            optionalFeatures,
            selectedFeatures,
            missingRequiredFeatures,
            availableQueueCount,
            requiredQueueCount,
            statusWrites);
    }

    private static VirtioDeviceStatusWriteSequence CreateSuccessSequence()
    {
        return new VirtioDeviceStatusWriteSequence(
            count: 4,
            AcknowledgeStatus,
            DriverStatus,
            FeaturesOkStatus,
            DriverOkStatus);
    }

    private static VirtioDeviceStatusWriteSequence CreateFailureBeforeFeaturesOkSequence()
    {
        return new VirtioDeviceStatusWriteSequence(
            count: 3,
            AcknowledgeStatus,
            DriverStatus,
            FailedBeforeFeaturesOkStatus,
            VirtioDeviceStatus.None);
    }

    private static VirtioDeviceStatusWriteSequence CreateFeaturesOkRejectedSequence()
    {
        return new VirtioDeviceStatusWriteSequence(
            count: 4,
            AcknowledgeStatus,
            DriverStatus,
            FeaturesOkStatus,
            FailedBeforeFeaturesOkStatus);
    }

    private static VirtioDeviceStatusWriteSequence CreateFailureAfterFeaturesOkSequence()
    {
        return new VirtioDeviceStatusWriteSequence(
            count: 4,
            AcknowledgeStatus,
            DriverStatus,
            FeaturesOkStatus,
            FailedAfterFeaturesOkStatus);
    }
}
