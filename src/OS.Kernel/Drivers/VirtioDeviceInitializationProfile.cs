namespace OS.Drivers;
public static class VirtioDeviceInitializationProfile
{
    public const ulong NoRequiredFeatureMask = 0;
    public const ushort BlockRequiredQueueCount = 1;
    public const ushort NetworkRequiredQueueCount = 2;
    public const ulong BlockRequiredFeatureMask = NoRequiredFeatureMask;
    public const int BlockReadOnlyFeatureBit = 5;
    public const ulong BlockReadOnlyFeatureMask = 1UL << BlockReadOnlyFeatureBit;
    public const ulong BlockOptionalFeatureMask = BlockReadOnlyFeatureMask;
    public const ulong NetworkRequiredFeatureMask = NoRequiredFeatureMask;
    public const int NetworkMacFeatureBit = 5;
    public const int NetworkStatusFeatureBit = 16;
    public const ulong NetworkMacFeatureMask = 1UL << NetworkMacFeatureBit;
    public const ulong NetworkStatusFeatureMask = 1UL << NetworkStatusFeatureBit;
    public const ulong NetworkOptionalFeatureMask = NetworkMacFeatureMask | NetworkStatusFeatureMask;
    public static VirtioDeviceInitializationProfileResult Create(VirtioPciDeviceKind deviceKind, ulong deviceFeatures, ushort availableQueueCount, bool featuresOkAccepted)
    {
        if (!TryGetProfile(deviceKind, out ulong requiredFeatures, out ulong optionalFeatures, out ushort requiredQueueCount))
        {
            return VirtioDeviceInitializationProfileResult.Create(VirtioDeviceInitializationProfileStatus.UnknownDeviceKind, deviceKind, deviceFeatures, requiredFeatures: 0, optionalFeatures: 0, availableQueueCount, requiredQueueCount: 0, hasInitializationPlan: false, default);
        }

        VirtioDeviceInitializationPlanResult initializationPlan = VirtioDeviceInitializationPlan.Create(deviceFeatures, requiredFeatures, optionalFeatures, availableQueueCount, requiredQueueCount, featuresOkAccepted);
        VirtioDeviceInitializationProfileStatus status = initializationPlan.Succeeded ? VirtioDeviceInitializationProfileStatus.Success : VirtioDeviceInitializationProfileStatus.InitializationPlanFailed;
        return VirtioDeviceInitializationProfileResult.Create(status, deviceKind, deviceFeatures, requiredFeatures, optionalFeatures, availableQueueCount, requiredQueueCount, hasInitializationPlan: true, initializationPlan);
    }

    private static bool TryGetProfile(VirtioPciDeviceKind deviceKind, out ulong requiredFeatures, out ulong optionalFeatures, out ushort requiredQueueCount)
    {
        switch (deviceKind)
        {
            case VirtioPciDeviceKind.Block:
                requiredFeatures = BlockRequiredFeatureMask;
                optionalFeatures = BlockOptionalFeatureMask;
                requiredQueueCount = BlockRequiredQueueCount;
                return true;
            case VirtioPciDeviceKind.Network:
                requiredFeatures = NetworkRequiredFeatureMask;
                optionalFeatures = NetworkOptionalFeatureMask;
                requiredQueueCount = NetworkRequiredQueueCount;
                return true;
            default:
                requiredFeatures = 0;
                optionalFeatures = 0;
                requiredQueueCount = 0;
                return false;
        }
    }
}
