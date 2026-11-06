namespace OS.Drivers;

using OS.Net;

public readonly struct VirtioNetDriverQueueBringUpPlanResult
{
    private VirtioNetDriverQueueBringUpPlanResult(VirtioNetDriverQueueBringUpPlanStatus status, VirtioPciDeviceKind deviceKind, VirtioPciBindingPlanStatus bindingPlanStatus, VirtioNetDeviceConfigStatus networkConfigurationStatus, VirtioDeviceInitializationProfileStatus initializationProfileStatus, VirtioDeviceInitializationPlanStatus initializationPlanStatus, VirtioNetQueuePlanStatus queuePlanStatus, VirtqueueSplitRingPlanStatus rxQueueStatus, VirtqueueSplitRingPlanStatus txQueueStatus, ulong selectedFeatures, ulong unsupportedSelectedFeatures, uint requiredRxQueueMemoryBytes, uint requiredTxQueueMemoryBytes, uint availableRxQueueMemoryBytes, uint availableTxQueueMemoryBytes, uint requiredRxBufferBytes, uint requiredTxBufferBytes, uint availableRxBufferBytes, uint availableTxBufferBytes, VirtioNetDriverQueueBringUpMetadata plan, bool isInitialized)
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

    public VirtioNetDriverQueueBringUpPlanStatus Status
    {
        get;
    }
    public VirtioPciDeviceKind DeviceKind
    {
        get;
    }
    public VirtioPciBindingPlanStatus BindingPlanStatus
    {
        get;
    }
    public VirtioNetDeviceConfigStatus NetworkConfigurationStatus
    {
        get;
    }
    public VirtioDeviceInitializationProfileStatus InitializationProfileStatus
    {
        get;
    }
    public VirtioDeviceInitializationPlanStatus InitializationPlanStatus
    {
        get;
    }
    public VirtioNetQueuePlanStatus QueuePlanStatus
    {
        get;
    }
    public VirtqueueSplitRingPlanStatus RxQueueStatus
    {
        get;
    }
    public VirtqueueSplitRingPlanStatus TxQueueStatus
    {
        get;
    }
    public ulong SelectedFeatures
    {
        get;
    }
    public ulong UnsupportedSelectedFeatures
    {
        get;
    }
    public uint RequiredRxQueueMemoryBytes
    {
        get;
    }
    public uint RequiredTxQueueMemoryBytes
    {
        get;
    }
    public uint AvailableRxQueueMemoryBytes
    {
        get;
    }
    public uint AvailableTxQueueMemoryBytes
    {
        get;
    }
    public uint RequiredRxBufferBytes
    {
        get;
    }
    public uint RequiredTxBufferBytes
    {
        get;
    }
    public uint AvailableRxBufferBytes
    {
        get;
    }
    public uint AvailableTxBufferBytes
    {
        get;
    }
    public VirtioNetDriverQueueBringUpMetadata Plan
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetDriverQueueBringUpPlanStatus.Success;

    internal static VirtioNetDriverQueueBringUpPlanResult Success(VirtioDeviceDescriptor descriptor, VirtioNetQueuePlanResult queuePlan, uint availableRxQueueMemoryBytes, uint availableTxQueueMemoryBytes, uint availableRxBufferBytes, uint availableTxBufferBytes, uint totalRequiredQueueMemoryBytes, uint totalAvailableQueueMemoryBytes, uint totalAvailableBufferBytes)
    {
        VirtioNetDeviceConfig networkConfig = descriptor.NetworkDeviceConfiguration;
        VirtioNetQueuePlan plan = queuePlan.Plan;
        ulong selectedFeatures = descriptor.InitializationProfile.InitializationPlan.SelectedFeatures;
        VirtioNetDriverQueueBringUpMetadata bringUpPlan = new VirtioNetDriverQueueBringUpMetadata(descriptor.SourceIndex, descriptor.Binding, networkConfig.MacAddress, networkConfig.HasStatus, networkConfig.IsLinkUp, networkConfig.HasAnnounce, selectedFeatures, VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask, descriptor.InitializationProfile.AvailableQueueCount, VirtioNetQueuePlan.ReceiveQueueIndex, VirtioNetQueuePlan.TransmitQueueIndex, plan.RxQueue.QueueSize, plan.TxQueue.QueueSize, plan.RxBuffers.RequiredDescriptorCount, plan.TxFrame.RequiredDescriptorCount, plan.RxQueue.RequiredByteLength, plan.TxQueue.RequiredByteLength, availableRxQueueMemoryBytes, availableTxQueueMemoryBytes, plan.RxBuffers.RequiredBufferBytes, plan.TxFrame.RequiredBufferBytes, availableRxBufferBytes, availableTxBufferBytes, totalRequiredQueueMemoryBytes, totalAvailableQueueMemoryBytes, plan.TotalRequiredBufferBytes, totalAvailableBufferBytes, plan);
        return new VirtioNetDriverQueueBringUpPlanResult(VirtioNetDriverQueueBringUpPlanStatus.Success, descriptor.DeviceKind, descriptor.BindingPlan.Status, descriptor.NetworkConfiguration.Status, descriptor.InitializationProfile.Status, descriptor.InitializationProfile.InitializationPlan.Status, queuePlan.Status, queuePlan.RxQueueStatus, queuePlan.TxQueueStatus, selectedFeatures, unsupportedSelectedFeatures: 0, plan.RxQueue.RequiredByteLength, plan.TxQueue.RequiredByteLength, availableRxQueueMemoryBytes, availableTxQueueMemoryBytes, plan.RxBuffers.RequiredBufferBytes, plan.TxFrame.RequiredBufferBytes, availableRxBufferBytes, availableTxBufferBytes, bringUpPlan, isInitialized: true);
    }

    internal static VirtioNetDriverQueueBringUpPlanResult Fail(VirtioNetDriverQueueBringUpPlanStatus status, VirtioDeviceDescriptor descriptor, VirtioNetQueuePlanResult queuePlan = default, ulong unsupportedSelectedFeatures = 0, uint requiredRxQueueMemoryBytes = 0, uint requiredTxQueueMemoryBytes = 0, uint availableRxQueueMemoryBytes = 0, uint availableTxQueueMemoryBytes = 0, uint requiredRxBufferBytes = 0, uint requiredTxBufferBytes = 0, uint availableRxBufferBytes = 0, uint availableTxBufferBytes = 0)
    {
        VirtioDeviceInitializationPlanStatus initializationPlanStatus = descriptor.InitializationProfile.HasInitializationPlan ? descriptor.InitializationProfile.InitializationPlan.Status : default;
        ulong selectedFeatures = descriptor.InitializationProfile.HasInitializationPlan ? descriptor.InitializationProfile.InitializationPlan.SelectedFeatures : 0;
        return new VirtioNetDriverQueueBringUpPlanResult(status, descriptor.DeviceKind, descriptor.BindingPlan.Status, descriptor.NetworkConfiguration.Status, descriptor.InitializationProfile.Status, initializationPlanStatus, queuePlan.Status, queuePlan.RxQueueStatus, queuePlan.TxQueueStatus, selectedFeatures, unsupportedSelectedFeatures, requiredRxQueueMemoryBytes, requiredTxQueueMemoryBytes, availableRxQueueMemoryBytes, availableTxQueueMemoryBytes, requiredRxBufferBytes, requiredTxBufferBytes, availableRxBufferBytes, availableTxBufferBytes, default, isInitialized: true);
    }
}
