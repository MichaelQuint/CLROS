namespace OS.Drivers;

using OS.Net;

public static class VirtioNetDriverQueueBringUpPlan
{
    public const bool WritesMmioRegisters = false;
    public const bool SelectsQueuesInDevice = false;
    public const bool PublishesDescriptors = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool SendsOrReceivesPackets = false;
    public static VirtioNetDriverQueueBringUpPlanResult Create(VirtioDeviceDescriptor descriptor, ushort rxQueueSize, ushort txQueueSize, ushort rxBufferCount, uint frameBufferBytes, uint availableRxQueueMemoryBytes, uint availableTxQueueMemoryBytes, uint availableRxBufferBytes, uint availableTxBufferBytes)
    {
        if (!descriptor.BindingPlan.Succeeded)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.BindingPlanFailed, descriptor, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        if (descriptor.DeviceKind != VirtioPciDeviceKind.Network || descriptor.Binding.DeviceKind != VirtioPciDeviceKind.Network)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.DescriptorNotNetwork, descriptor, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        if (!descriptor.HasNetworkConfiguration)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.MissingNetworkConfiguration, descriptor, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        VirtioDeviceInitializationProfileResult profile = descriptor.InitializationProfile;
        if (!profile.Succeeded || !profile.HasInitializationPlan)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.InitializationProfileFailed, descriptor, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        if (profile.DeviceKind != VirtioPciDeviceKind.Network || profile.RequiredQueueCount != VirtioDeviceInitializationProfile.NetworkRequiredQueueCount || profile.RequiredFeatures != VirtioDeviceInitializationProfile.NetworkRequiredFeatureMask || profile.OptionalFeatures != VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask || !profile.InitializationPlan.Succeeded)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.FeatureProfileMismatch, descriptor, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        ulong unsupportedSelectedFeatures = profile.InitializationPlan.SelectedFeatures & ~VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask;
        if (unsupportedSelectedFeatures != 0)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.UnsupportedSelectedFeatures, descriptor, unsupportedSelectedFeatures: unsupportedSelectedFeatures, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        VirtioNetQueuePlanResult queuePlan = VirtioNetQueuePlan.Create(profile.AvailableQueueCount, rxQueueSize, txQueueSize, rxBufferCount, frameBufferBytes);
        if (!queuePlan.Succeeded)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.QueuePlanFailed, descriptor, queuePlan, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        VirtioNetQueuePlan plan = queuePlan.Plan;
        uint requiredRxQueueMemoryBytes = plan.RxQueue.RequiredByteLength;
        uint requiredTxQueueMemoryBytes = plan.TxQueue.RequiredByteLength;
        uint requiredRxBufferBytes = plan.RxBuffers.RequiredBufferBytes;
        uint requiredTxBufferBytes = plan.TxFrame.RequiredBufferBytes;
        if (!TryAdd(requiredRxQueueMemoryBytes, requiredTxQueueMemoryBytes, out uint totalRequiredQueueMemoryBytes) || !TryAdd(availableRxQueueMemoryBytes, availableTxQueueMemoryBytes, out uint totalAvailableQueueMemoryBytes) || !TryAdd(availableRxBufferBytes, availableTxBufferBytes, out uint totalAvailableBufferBytes))
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.ByteCountOverflow, descriptor, queuePlan, requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes, requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, requiredRxBufferBytes: requiredRxBufferBytes, requiredTxBufferBytes: requiredTxBufferBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        if (availableRxQueueMemoryBytes < requiredRxQueueMemoryBytes)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.RxQueueMemoryTooSmall, descriptor, queuePlan, requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes, requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, requiredRxBufferBytes: requiredRxBufferBytes, requiredTxBufferBytes: requiredTxBufferBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        if (availableTxQueueMemoryBytes < requiredTxQueueMemoryBytes)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.TxQueueMemoryTooSmall, descriptor, queuePlan, requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes, requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, requiredRxBufferBytes: requiredRxBufferBytes, requiredTxBufferBytes: requiredTxBufferBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        if (availableRxBufferBytes < requiredRxBufferBytes)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.RxBufferCapacityTooSmall, descriptor, queuePlan, requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes, requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, requiredRxBufferBytes: requiredRxBufferBytes, requiredTxBufferBytes: requiredTxBufferBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        if (availableTxBufferBytes < requiredTxBufferBytes)
        {
            return VirtioNetDriverQueueBringUpPlanResult.Fail(VirtioNetDriverQueueBringUpPlanStatus.TxBufferCapacityTooSmall, descriptor, queuePlan, requiredRxQueueMemoryBytes: requiredRxQueueMemoryBytes, requiredTxQueueMemoryBytes: requiredTxQueueMemoryBytes, availableRxQueueMemoryBytes: availableRxQueueMemoryBytes, availableTxQueueMemoryBytes: availableTxQueueMemoryBytes, requiredRxBufferBytes: requiredRxBufferBytes, requiredTxBufferBytes: requiredTxBufferBytes, availableRxBufferBytes: availableRxBufferBytes, availableTxBufferBytes: availableTxBufferBytes);
        }

        return VirtioNetDriverQueueBringUpPlanResult.Success(descriptor, queuePlan, availableRxQueueMemoryBytes, availableTxQueueMemoryBytes, availableRxBufferBytes, availableTxBufferBytes, totalRequiredQueueMemoryBytes, totalAvailableQueueMemoryBytes, totalAvailableBufferBytes);
    }

    private static bool TryAdd(uint left, uint right, out uint result)
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
