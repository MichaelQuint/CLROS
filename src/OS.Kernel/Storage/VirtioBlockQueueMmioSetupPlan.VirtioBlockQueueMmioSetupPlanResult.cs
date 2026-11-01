using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueMmioSetupPlanResult
{
    private VirtioBlockQueueMmioSetupPlanResult(
        bool isInitialized,
        VirtioBlockQueueMmioSetupPlanStatus status,
        VirtioPciDeviceKind deviceKind,
        VirtioPciBindingPlanStatus bindingStatus,
        VirtioBlockDeviceConfigStatus blockConfigurationStatus,
        VirtioDeviceInitializationProfileStatus initializationProfileStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioBlockQueueMmioSetupAddressKind failingAddressKind,
        ulong failingPhysicalAddress,
        ushort plannedQueueSize,
        ushort deviceMaximumQueueSize,
        int requiredCommandCount,
        int availableCommandCount,
        int commandCount,
        VirtioBlockQueueMmioSetupPlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        DeviceKind = deviceKind;
        BindingStatus = bindingStatus;
        BlockConfigurationStatus = blockConfigurationStatus;
        InitializationProfileStatus = initializationProfileStatus;
        QueuePlanStatus = queuePlanStatus;
        FailingAddressKind = failingAddressKind;
        FailingPhysicalAddress = failingPhysicalAddress;
        PlannedQueueSize = plannedQueueSize;
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        RequiredCommandCount = requiredCommandCount;
        AvailableCommandCount = availableCommandCount;
        CommandCount = commandCount;
        Plan = plan;
    }

    public bool IsInitialized { get; }

    public VirtioBlockQueueMmioSetupPlanStatus Status { get; }

    public VirtioPciDeviceKind DeviceKind { get; }

    public VirtioPciBindingPlanStatus BindingStatus { get; }

    public VirtioBlockDeviceConfigStatus BlockConfigurationStatus { get; }

    public VirtioDeviceInitializationProfileStatus InitializationProfileStatus { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public VirtioBlockQueueMmioSetupAddressKind FailingAddressKind { get; }

    public ulong FailingPhysicalAddress { get; }

    public ushort PlannedQueueSize { get; }

    public ushort DeviceMaximumQueueSize { get; }

    public int RequiredCommandCount { get; }

    public int AvailableCommandCount { get; }

    public int CommandCount { get; }

    public VirtioBlockQueueMmioSetupPlan Plan { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockQueueMmioSetupPlanStatus.Success;

    internal static VirtioBlockQueueMmioSetupPlanResult Success(
        VirtioDeviceDescriptor descriptor,
        VirtioBlockQueueMmioSetupPlan plan,
        int availableCommandCount)
    {
        return new VirtioBlockQueueMmioSetupPlanResult(
            isInitialized: true,
            VirtioBlockQueueMmioSetupPlanStatus.Success,
            descriptor.DeviceKind,
            descriptor.BindingPlan.Status,
            descriptor.BlockConfiguration.Status,
            descriptor.InitializationProfile.Status,
            VirtqueueSplitRingPlanStatus.Success,
            VirtioBlockQueueMmioSetupAddressKind.None,
            failingPhysicalAddress: 0,
            plannedQueueSize: plan.PlannedQueueSize,
            deviceMaximumQueueSize: plan.DeviceMaximumQueueSize,
            VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount,
            availableCommandCount,
            plan.CommandCount,
            plan);
    }

    internal static VirtioBlockQueueMmioSetupPlanResult Fail(
        VirtioBlockQueueMmioSetupPlanStatus status,
        VirtioDeviceDescriptor descriptor,
        VirtqueueSplitRingPlanStatus queuePlanStatus = VirtqueueSplitRingPlanStatus.Success,
        VirtioBlockQueueMmioSetupAddressKind failingAddressKind = VirtioBlockQueueMmioSetupAddressKind.None,
        ulong failingPhysicalAddress = 0,
        ushort plannedQueueSize = 0,
        ushort deviceMaximumQueueSize = 0,
        int availableCommandCount = 0)
    {
        return new VirtioBlockQueueMmioSetupPlanResult(
            isInitialized: true,
            status,
            descriptor.DeviceKind,
            descriptor.BindingPlan.Status,
            descriptor.BlockConfiguration.Status,
            descriptor.InitializationProfile.Status,
            queuePlanStatus,
            failingAddressKind,
            failingPhysicalAddress,
            plannedQueueSize,
            deviceMaximumQueueSize,
            VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount,
            availableCommandCount,
            commandCount: 0,
            default);
    }
}
