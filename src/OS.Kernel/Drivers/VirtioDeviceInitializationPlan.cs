namespace OS.Drivers;

public static class VirtioDeviceInitializationPlan
{
    public const VirtioDeviceStatus AcknowledgeStatus = VirtioDeviceStatus.Acknowledge;
    public const VirtioDeviceStatus DriverStatus = VirtioDeviceStatus.Acknowledge | VirtioDeviceStatus.Driver;
    public const VirtioDeviceStatus FeaturesOkStatus = VirtioDeviceStatus.Acknowledge | VirtioDeviceStatus.Driver | VirtioDeviceStatus.FeaturesOk;
    public const VirtioDeviceStatus DriverOkStatus = VirtioDeviceStatus.Acknowledge | VirtioDeviceStatus.Driver | VirtioDeviceStatus.FeaturesOk | VirtioDeviceStatus.DriverOk;
    public const VirtioDeviceStatus FailedBeforeFeaturesOkStatus = VirtioDeviceStatus.Acknowledge | VirtioDeviceStatus.Driver | VirtioDeviceStatus.Failed;
    public const VirtioDeviceStatus FailedAfterFeaturesOkStatus = VirtioDeviceStatus.Acknowledge | VirtioDeviceStatus.Driver | VirtioDeviceStatus.FeaturesOk | VirtioDeviceStatus.Failed;
    public static VirtioDeviceInitializationPlanResult Create(ulong deviceFeatures, ulong requiredFeatures, ulong optionalFeatures, ushort availableQueueCount, ushort requiredQueueCount, bool featuresOkAccepted)
    {
        ulong selectedFeatures = SelectFeatures(deviceFeatures, requiredFeatures, optionalFeatures);
        ulong missingRequiredFeatures = requiredFeatures & ~deviceFeatures;
        if (missingRequiredFeatures != 0)
        {
            return CreateResult(VirtioDeviceInitializationPlanStatus.MissingRequiredFeatures, deviceFeatures, requiredFeatures, optionalFeatures, selectedFeatures, missingRequiredFeatures, availableQueueCount, requiredQueueCount, CreateFailureBeforeFeaturesOkSequence());
        }

        if (!featuresOkAccepted)
        {
            return CreateResult(VirtioDeviceInitializationPlanStatus.FeaturesOkRejected, deviceFeatures, requiredFeatures, optionalFeatures, selectedFeatures, missingRequiredFeatures, availableQueueCount, requiredQueueCount, CreateFeaturesOkRejectedSequence());
        }

        if (requiredQueueCount == 0)
        {
            return CreateResult(VirtioDeviceInitializationPlanStatus.InvalidRequiredQueueCount, deviceFeatures, requiredFeatures, optionalFeatures, selectedFeatures, missingRequiredFeatures, availableQueueCount, requiredQueueCount, CreateFailureAfterFeaturesOkSequence());
        }

        if (availableQueueCount < requiredQueueCount)
        {
            return CreateResult(VirtioDeviceInitializationPlanStatus.InsufficientQueueCount, deviceFeatures, requiredFeatures, optionalFeatures, selectedFeatures, missingRequiredFeatures, availableQueueCount, requiredQueueCount, CreateFailureAfterFeaturesOkSequence());
        }

        return CreateResult(VirtioDeviceInitializationPlanStatus.Success, deviceFeatures, requiredFeatures, optionalFeatures, selectedFeatures, missingRequiredFeatures, availableQueueCount, requiredQueueCount, CreateSuccessSequence());
    }

    private static ulong SelectFeatures(ulong deviceFeatures, ulong requiredFeatures, ulong optionalFeatures)
    {
        return deviceFeatures & (requiredFeatures | optionalFeatures);
    }

    private static VirtioDeviceInitializationPlanResult CreateResult(VirtioDeviceInitializationPlanStatus status, ulong deviceFeatures, ulong requiredFeatures, ulong optionalFeatures, ulong selectedFeatures, ulong missingRequiredFeatures, ushort availableQueueCount, ushort requiredQueueCount, VirtioDeviceStatusWriteSequence statusWrites)
    {
        return VirtioDeviceInitializationPlanResult.Create(status, deviceFeatures, requiredFeatures, optionalFeatures, selectedFeatures, missingRequiredFeatures, availableQueueCount, requiredQueueCount, statusWrites);
    }

    private static VirtioDeviceStatusWriteSequence CreateSuccessSequence()
    {
        return new VirtioDeviceStatusWriteSequence(count: 4, AcknowledgeStatus, DriverStatus, FeaturesOkStatus, DriverOkStatus);
    }

    private static VirtioDeviceStatusWriteSequence CreateFailureBeforeFeaturesOkSequence()
    {
        return new VirtioDeviceStatusWriteSequence(count: 3, AcknowledgeStatus, DriverStatus, FailedBeforeFeaturesOkStatus, VirtioDeviceStatus.None);
    }

    private static VirtioDeviceStatusWriteSequence CreateFeaturesOkRejectedSequence()
    {
        return new VirtioDeviceStatusWriteSequence(count: 4, AcknowledgeStatus, DriverStatus, FeaturesOkStatus, FailedBeforeFeaturesOkStatus);
    }

    private static VirtioDeviceStatusWriteSequence CreateFailureAfterFeaturesOkSequence()
    {
        return new VirtioDeviceStatusWriteSequence(count: 4, AcknowledgeStatus, DriverStatus, FeaturesOkStatus, FailedAfterFeaturesOkStatus);
    }
}
