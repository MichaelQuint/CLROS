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

public readonly struct VirtioNetDriverQueueBringUpMetadata
{
    internal VirtioNetDriverQueueBringUpMetadata(
        int sourceIndex,
        VirtioPciBindingMetadata binding,
        MacAddress macAddress,
        bool hasDeviceStatus,
        bool linkUp,
        bool hasAnnounce,
        ulong selectedFeatures,
        ulong supportedFeatureMask,
        ushort availableQueueCount,
        ushort rxQueueIndex,
        ushort txQueueIndex,
        ushort rxQueueSize,
        ushort txQueueSize,
        ushort rxDescriptorCount,
        ushort txDescriptorCount,
        uint requiredRxQueueMemoryBytes,
        uint requiredTxQueueMemoryBytes,
        uint availableRxQueueMemoryBytes,
        uint availableTxQueueMemoryBytes,
        uint requiredRxBufferBytes,
        uint requiredTxBufferBytes,
        uint availableRxBufferBytes,
        uint availableTxBufferBytes,
        uint totalRequiredQueueMemoryBytes,
        uint totalAvailableQueueMemoryBytes,
        uint totalRequiredBufferBytes,
        uint totalAvailableBufferBytes,
        VirtioNetQueuePlan queuePlan)
    {
        SourceIndex = sourceIndex;
        Binding = binding;
        MacAddress = macAddress;
        HasDeviceStatus = hasDeviceStatus;
        LinkUp = linkUp;
        HasAnnounce = hasAnnounce;
        SelectedFeatures = selectedFeatures;
        SupportedFeatureMask = supportedFeatureMask;
        AvailableQueueCount = availableQueueCount;
        RxQueueIndex = rxQueueIndex;
        TxQueueIndex = txQueueIndex;
        RxQueueSize = rxQueueSize;
        TxQueueSize = txQueueSize;
        RxDescriptorCount = rxDescriptorCount;
        TxDescriptorCount = txDescriptorCount;
        RequiredRxQueueMemoryBytes = requiredRxQueueMemoryBytes;
        RequiredTxQueueMemoryBytes = requiredTxQueueMemoryBytes;
        AvailableRxQueueMemoryBytes = availableRxQueueMemoryBytes;
        AvailableTxQueueMemoryBytes = availableTxQueueMemoryBytes;
        RequiredRxBufferBytes = requiredRxBufferBytes;
        RequiredTxBufferBytes = requiredTxBufferBytes;
        AvailableRxBufferBytes = availableRxBufferBytes;
        AvailableTxBufferBytes = availableTxBufferBytes;
        TotalRequiredQueueMemoryBytes = totalRequiredQueueMemoryBytes;
        TotalAvailableQueueMemoryBytes = totalAvailableQueueMemoryBytes;
        TotalRequiredBufferBytes = totalRequiredBufferBytes;
        TotalAvailableBufferBytes = totalAvailableBufferBytes;
        QueuePlan = queuePlan;
    }

    public int SourceIndex { get; }

    public VirtioPciBindingMetadata Binding { get; }

    public MacAddress MacAddress { get; }

    public bool HasDeviceStatus { get; }

    public bool LinkUp { get; }

    public bool HasAnnounce { get; }

    public ulong SelectedFeatures { get; }

    public ulong SupportedFeatureMask { get; }

    public ushort AvailableQueueCount { get; }

    public ushort RxQueueIndex { get; }

    public ushort TxQueueIndex { get; }

    public ushort RxQueueSize { get; }

    public ushort TxQueueSize { get; }

    public ushort RxDescriptorCount { get; }

    public ushort TxDescriptorCount { get; }

    public uint RequiredRxQueueMemoryBytes { get; }

    public uint RequiredTxQueueMemoryBytes { get; }

    public uint AvailableRxQueueMemoryBytes { get; }

    public uint AvailableTxQueueMemoryBytes { get; }

    public uint RequiredRxBufferBytes { get; }

    public uint RequiredTxBufferBytes { get; }

    public uint AvailableRxBufferBytes { get; }

    public uint AvailableTxBufferBytes { get; }

    public uint TotalRequiredQueueMemoryBytes { get; }

    public uint TotalAvailableQueueMemoryBytes { get; }

    public uint TotalRequiredBufferBytes { get; }

    public uint TotalAvailableBufferBytes { get; }

    public VirtioNetQueuePlan QueuePlan { get; }

    public bool ReceivePublishReady =>
        RxDescriptorCount == QueuePlan.RxBuffers.RequiredDescriptorCount &&
        AvailableRxQueueMemoryBytes >= RequiredRxQueueMemoryBytes &&
        AvailableRxBufferBytes >= RequiredRxBufferBytes;

    public bool TransmitPublishReady =>
        TxDescriptorCount == QueuePlan.TxFrame.RequiredDescriptorCount &&
        AvailableTxQueueMemoryBytes >= RequiredTxQueueMemoryBytes &&
        AvailableTxBufferBytes >= RequiredTxBufferBytes;

    public bool PublishReady => ReceivePublishReady && TransmitPublishReady;
}

public readonly struct VirtioNetDriverQueueBringUpPlanResult
{
    private VirtioNetDriverQueueBringUpPlanResult(
        VirtioNetDriverQueueBringUpPlanStatus status,
        VirtioPciDeviceKind deviceKind,
        VirtioPciBindingPlanStatus bindingPlanStatus,
        VirtioNetDeviceConfigStatus networkConfigurationStatus,
        VirtioDeviceInitializationProfileStatus initializationProfileStatus,
        VirtioDeviceInitializationPlanStatus initializationPlanStatus,
        VirtioNetQueuePlanStatus queuePlanStatus,
        VirtqueueSplitRingPlanStatus rxQueueStatus,
        VirtqueueSplitRingPlanStatus txQueueStatus,
        ulong selectedFeatures,
        ulong unsupportedSelectedFeatures,
        uint requiredRxQueueMemoryBytes,
        uint requiredTxQueueMemoryBytes,
        uint availableRxQueueMemoryBytes,
        uint availableTxQueueMemoryBytes,
        uint requiredRxBufferBytes,
        uint requiredTxBufferBytes,
        uint availableRxBufferBytes,
        uint availableTxBufferBytes,
        VirtioNetDriverQueueBringUpMetadata plan,
        bool isInitialized)
    {
        Status = status;
        DeviceKind = deviceKind;
        BindingPlanStatus = bindingPlanStatus;
        NetworkConfigurationStatus = networkConfigurationStatus;
        InitializationProfileStatus = initializationProfileStatus;
        InitializationPlanStatus = initializationPlanStatus;
        QueuePlanStatus = queuePlanStatus;
        RxQueueStatus = rxQueueStatus;
        TxQueueStatus = txQueueStatus;
        SelectedFeatures = selectedFeatures;
        UnsupportedSelectedFeatures = unsupportedSelectedFeatures;
        RequiredRxQueueMemoryBytes = requiredRxQueueMemoryBytes;
        RequiredTxQueueMemoryBytes = requiredTxQueueMemoryBytes;
        AvailableRxQueueMemoryBytes = availableRxQueueMemoryBytes;
        AvailableTxQueueMemoryBytes = availableTxQueueMemoryBytes;
        RequiredRxBufferBytes = requiredRxBufferBytes;
        RequiredTxBufferBytes = requiredTxBufferBytes;
        AvailableRxBufferBytes = availableRxBufferBytes;
        AvailableTxBufferBytes = availableTxBufferBytes;
        Plan = plan;
        IsInitialized = isInitialized;
    }

    public VirtioNetDriverQueueBringUpPlanStatus Status { get; }

    public VirtioPciDeviceKind DeviceKind { get; }

    public VirtioPciBindingPlanStatus BindingPlanStatus { get; }

    public VirtioNetDeviceConfigStatus NetworkConfigurationStatus { get; }

    public VirtioDeviceInitializationProfileStatus InitializationProfileStatus { get; }

    public VirtioDeviceInitializationPlanStatus InitializationPlanStatus { get; }

    public VirtioNetQueuePlanStatus QueuePlanStatus { get; }

    public VirtqueueSplitRingPlanStatus RxQueueStatus { get; }

    public VirtqueueSplitRingPlanStatus TxQueueStatus { get; }

    public ulong SelectedFeatures { get; }

    public ulong UnsupportedSelectedFeatures { get; }

    public uint RequiredRxQueueMemoryBytes { get; }

    public uint RequiredTxQueueMemoryBytes { get; }

    public uint AvailableRxQueueMemoryBytes { get; }

    public uint AvailableTxQueueMemoryBytes { get; }

    public uint RequiredRxBufferBytes { get; }

    public uint RequiredTxBufferBytes { get; }

    public uint AvailableRxBufferBytes { get; }

    public uint AvailableTxBufferBytes { get; }

    public VirtioNetDriverQueueBringUpMetadata Plan { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioNetDriverQueueBringUpPlanStatus.Success;

    internal static VirtioNetDriverQueueBringUpPlanResult Success(
        VirtioDeviceDescriptor descriptor,
        VirtioNetQueuePlanResult queuePlan,
        uint availableRxQueueMemoryBytes,
        uint availableTxQueueMemoryBytes,
        uint availableRxBufferBytes,
        uint availableTxBufferBytes,
        uint totalRequiredQueueMemoryBytes,
        uint totalAvailableQueueMemoryBytes,
        uint totalAvailableBufferBytes)
    {
        VirtioNetDeviceConfig networkConfig = descriptor.NetworkDeviceConfiguration;
        VirtioNetQueuePlan plan = queuePlan.Plan;
        ulong selectedFeatures =
            descriptor.InitializationProfile.InitializationPlan.SelectedFeatures;

        VirtioNetDriverQueueBringUpMetadata bringUpPlan =
            new VirtioNetDriverQueueBringUpMetadata(
                descriptor.SourceIndex,
                descriptor.Binding,
                networkConfig.MacAddress,
                networkConfig.HasStatus,
                networkConfig.IsLinkUp,
                networkConfig.HasAnnounce,
                selectedFeatures,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                descriptor.InitializationProfile.AvailableQueueCount,
                VirtioNetQueuePlan.ReceiveQueueIndex,
                VirtioNetQueuePlan.TransmitQueueIndex,
                plan.RxQueue.QueueSize,
                plan.TxQueue.QueueSize,
                plan.RxBuffers.RequiredDescriptorCount,
                plan.TxFrame.RequiredDescriptorCount,
                plan.RxQueue.RequiredByteLength,
                plan.TxQueue.RequiredByteLength,
                availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes,
                plan.RxBuffers.RequiredBufferBytes,
                plan.TxFrame.RequiredBufferBytes,
                availableRxBufferBytes,
                availableTxBufferBytes,
                totalRequiredQueueMemoryBytes,
                totalAvailableQueueMemoryBytes,
                plan.TotalRequiredBufferBytes,
                totalAvailableBufferBytes,
                plan);

        return new VirtioNetDriverQueueBringUpPlanResult(
            VirtioNetDriverQueueBringUpPlanStatus.Success,
            descriptor.DeviceKind,
            descriptor.BindingPlan.Status,
            descriptor.NetworkConfiguration.Status,
            descriptor.InitializationProfile.Status,
            descriptor.InitializationProfile.InitializationPlan.Status,
            queuePlan.Status,
            queuePlan.RxQueueStatus,
            queuePlan.TxQueueStatus,
            selectedFeatures,
            unsupportedSelectedFeatures: 0,
            plan.RxQueue.RequiredByteLength,
            plan.TxQueue.RequiredByteLength,
            availableRxQueueMemoryBytes,
            availableTxQueueMemoryBytes,
            plan.RxBuffers.RequiredBufferBytes,
            plan.TxFrame.RequiredBufferBytes,
            availableRxBufferBytes,
            availableTxBufferBytes,
            bringUpPlan,
            isInitialized: true);
    }

    internal static VirtioNetDriverQueueBringUpPlanResult Fail(
        VirtioNetDriverQueueBringUpPlanStatus status,
        VirtioDeviceDescriptor descriptor,
        VirtioNetQueuePlanResult queuePlan = default,
        ulong unsupportedSelectedFeatures = 0,
        uint requiredRxQueueMemoryBytes = 0,
        uint requiredTxQueueMemoryBytes = 0,
        uint availableRxQueueMemoryBytes = 0,
        uint availableTxQueueMemoryBytes = 0,
        uint requiredRxBufferBytes = 0,
        uint requiredTxBufferBytes = 0,
        uint availableRxBufferBytes = 0,
        uint availableTxBufferBytes = 0)
    {
        VirtioDeviceInitializationPlanStatus initializationPlanStatus =
            descriptor.InitializationProfile.HasInitializationPlan
                ? descriptor.InitializationProfile.InitializationPlan.Status
                : default;
        ulong selectedFeatures = descriptor.InitializationProfile.HasInitializationPlan
            ? descriptor.InitializationProfile.InitializationPlan.SelectedFeatures
            : 0;

        return new VirtioNetDriverQueueBringUpPlanResult(
            status,
            descriptor.DeviceKind,
            descriptor.BindingPlan.Status,
            descriptor.NetworkConfiguration.Status,
            descriptor.InitializationProfile.Status,
            initializationPlanStatus,
            queuePlan.Status,
            queuePlan.RxQueueStatus,
            queuePlan.TxQueueStatus,
            selectedFeatures,
            unsupportedSelectedFeatures,
            requiredRxQueueMemoryBytes,
            requiredTxQueueMemoryBytes,
            availableRxQueueMemoryBytes,
            availableTxQueueMemoryBytes,
            requiredRxBufferBytes,
            requiredTxBufferBytes,
            availableRxBufferBytes,
            availableTxBufferBytes,
            default,
            isInitialized: true);
    }
}

public static class VirtioNetDriverQueueBringUpPlan
{
    public const bool WritesMmioRegisters = false;
    public const bool SelectsQueuesInDevice = false;
    public const bool PublishesDescriptors = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool SendsOrReceivesPackets = false;

    public static VirtioNetDriverQueueBringUpPlanResult Create(
        VirtioDeviceDescriptor descriptor,
        ushort rxQueueSize,
        ushort txQueueSize,
        ushort rxBufferCount,
        uint frameBufferBytes,
        uint availableRxQueueMemoryBytes,
        uint availableTxQueueMemoryBytes,
        uint availableRxBufferBytes,
        uint availableTxBufferBytes)
    {
        if (!descriptor.BindingPlan.Succeeded)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.BindingPlanFailed,
                descriptor,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        if (descriptor.DeviceKind != VirtioPciDeviceKind.Network ||
            descriptor.Binding.DeviceKind != VirtioPciDeviceKind.Network)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.DescriptorNotNetwork,
                descriptor,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        if (!descriptor.HasNetworkConfiguration)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.MissingNetworkConfiguration,
                descriptor,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        VirtioDeviceInitializationProfileResult profile = descriptor.InitializationProfile;
        if (!profile.Succeeded || !profile.HasInitializationPlan)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.InitializationProfileFailed,
                descriptor,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        if (profile.DeviceKind != VirtioPciDeviceKind.Network ||
            profile.RequiredQueueCount != VirtioDeviceInitializationProfile.NetworkRequiredQueueCount ||
            profile.RequiredFeatures != VirtioDeviceInitializationProfile.NetworkRequiredFeatureMask ||
            profile.OptionalFeatures != VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask ||
            !profile.InitializationPlan.Succeeded)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.FeatureProfileMismatch,
                descriptor,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        ulong unsupportedSelectedFeatures =
            profile.InitializationPlan.SelectedFeatures &
            ~VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask;
        if (unsupportedSelectedFeatures != 0)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.UnsupportedSelectedFeatures,
                descriptor,
                unsupportedSelectedFeatures: unsupportedSelectedFeatures,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        VirtioNetQueuePlanResult queuePlan = VirtioNetQueuePlan.Create(
            profile.AvailableQueueCount,
            rxQueueSize,
            txQueueSize,
            rxBufferCount,
            frameBufferBytes);
        if (!queuePlan.Succeeded)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.QueuePlanFailed,
                descriptor,
                queuePlan,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        VirtioNetQueuePlan plan = queuePlan.Plan;
        uint requiredRxQueueMemoryBytes = plan.RxQueue.RequiredByteLength;
        uint requiredTxQueueMemoryBytes = plan.TxQueue.RequiredByteLength;
        uint requiredRxBufferBytes = plan.RxBuffers.RequiredBufferBytes;
        uint requiredTxBufferBytes = plan.TxFrame.RequiredBufferBytes;

        if (!TryAdd(
                requiredRxQueueMemoryBytes,
                requiredTxQueueMemoryBytes,
                out uint totalRequiredQueueMemoryBytes) ||
            !TryAdd(
                availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes,
                out uint totalAvailableQueueMemoryBytes) ||
            !TryAdd(
                availableRxBufferBytes,
                availableTxBufferBytes,
                out uint totalAvailableBufferBytes))
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.ByteCountOverflow,
                descriptor,
                queuePlan,
                requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes,
                requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                requiredRxBufferBytes: requiredRxBufferBytes,
                requiredTxBufferBytes: requiredTxBufferBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        if (availableRxQueueMemoryBytes < requiredRxQueueMemoryBytes)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.RxQueueMemoryTooSmall,
                descriptor,
                queuePlan,
                requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes,
                requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                requiredRxBufferBytes: requiredRxBufferBytes,
                requiredTxBufferBytes: requiredTxBufferBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        if (availableTxQueueMemoryBytes < requiredTxQueueMemoryBytes)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.TxQueueMemoryTooSmall,
                descriptor,
                queuePlan,
                requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes,
                requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                requiredRxBufferBytes: requiredRxBufferBytes,
                requiredTxBufferBytes: requiredTxBufferBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        if (availableRxBufferBytes < requiredRxBufferBytes)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.RxBufferCapacityTooSmall,
                descriptor,
                queuePlan,
                requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes,
                requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                requiredRxBufferBytes: requiredRxBufferBytes,
                requiredTxBufferBytes: requiredTxBufferBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        if (availableTxBufferBytes < requiredTxBufferBytes)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(
                VirtioNetDriverQueueBringUpPlanStatus.TxBufferCapacityTooSmall,
                descriptor,
                queuePlan,
                requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes,
                requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes,
                availableRxQueueMemoryBytes: availableRxQueueMemoryBytes,
                availableTxQueueMemoryBytes: availableTxQueueMemoryBytes,
                requiredRxBufferBytes: requiredRxBufferBytes,
                requiredTxBufferBytes: requiredTxBufferBytes,
                availableRxBufferBytes: availableRxBufferBytes,
                availableTxBufferBytes: availableTxBufferBytes);
        }

        return VirtioNetDriverQueueBringUpPlanResult.Success(
            descriptor,
            queuePlan,
            availableRxQueueMemoryBytes,
            availableTxQueueMemoryBytes,
            availableRxBufferBytes,
            availableTxBufferBytes,
            totalRequiredQueueMemoryBytes,
            totalAvailableQueueMemoryBytes,
            totalAvailableBufferBytes);
    }

    private static bool TryAdd(
        uint left,
        uint right,
        out uint result)
    {
        if (left > uint.MaxValue - right)
        {
            result = 0;
            return false;
        }

        result = left + right;
        return true;
    }
}
