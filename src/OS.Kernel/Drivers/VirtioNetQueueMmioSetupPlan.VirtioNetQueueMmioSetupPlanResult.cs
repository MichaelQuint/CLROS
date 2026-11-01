using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VirtioNetQueueMmioSetupPlanResult
{
    private VirtioNetQueueMmioSetupPlanResult(bool isInitialized, VirtioNetQueueMmioSetupPlanStatus status, VirtioNetDriverQueueBringUpPlanStatus bringUpStatus, ushort failingQueueIndex, VirtioNetQueueMmioSetupAddressKind failingAddressKind, ulong failingPhysicalAddress, ushort plannedQueueSize, ushort deviceMaximumQueueSize, ulong unsupportedFeatureMask, int requiredCommandCount, int availableCommandCount, int commandCount, VirtioNetQueueMmioSetupPlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        BringUpStatus = bringUpStatus;
        FailingQueueIndex = failingQueueIndex;
        FailingAddressKind = failingAddressKind;
        FailingPhysicalAddress = failingPhysicalAddress;
        PlannedQueueSize = plannedQueueSize;
        DeviceMaximumQueueSize = deviceMaximumQueueSize;
        UnsupportedFeatureMask = unsupportedFeatureMask;
        RequiredCommandCount = requiredCommandCount;
        AvailableCommandCount = availableCommandCount;
        CommandCount = commandCount;
        Plan = plan;
    }

    public VirtioNetQueueMmioSetupPlanStatus Status { get; }
    public VirtioNetDriverQueueBringUpPlanStatus BringUpStatus { get; }
    public ushort FailingQueueIndex { get; }
    public VirtioNetQueueMmioSetupAddressKind FailingAddressKind { get; }
    public ulong FailingPhysicalAddress { get; }
    public ushort PlannedQueueSize { get; }
    public ushort DeviceMaximumQueueSize { get; }
    public ulong UnsupportedFeatureMask { get; }
    public int RequiredCommandCount { get; }
    public int AvailableCommandCount { get; }
    public int CommandCount { get; }
    public VirtioNetQueueMmioSetupPlan Plan { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueueMmioSetupPlanStatus.Success;

    internal static VirtioNetQueueMmioSetupPlanResult Success(VirtioNetQueueMmioSetupPlan plan, VirtioNetDriverQueueBringUpPlanStatus bringUpStatus, int availableCommandCount)
    {
        return new VirtioNetQueueMmioSetupPlanResult(isInitialized: true, VirtioNetQueueMmioSetupPlanStatus.Success, bringUpStatus, failingQueueIndex: 0, VirtioNetQueueMmioSetupAddressKind.None, failingPhysicalAddress: 0, plannedQueueSize: 0, deviceMaximumQueueSize: 0, unsupportedFeatureMask: 0, VirtioNetQueueMmioSetupPlanner.RequiredCommandCount, availableCommandCount, plan.CommandCount, plan);
    }

    internal static VirtioNetQueueMmioSetupPlanResult Fail(VirtioNetQueueMmioSetupPlanStatus status, VirtioNetDriverQueueBringUpPlanStatus bringUpStatus, ushort failingQueueIndex = 0, VirtioNetQueueMmioSetupAddressKind failingAddressKind = VirtioNetQueueMmioSetupAddressKind.None, ulong failingPhysicalAddress = 0, ushort plannedQueueSize = 0, ushort deviceMaximumQueueSize = 0, ulong unsupportedFeatureMask = 0, int availableCommandCount = 0)
    {
        return new VirtioNetQueueMmioSetupPlanResult(isInitialized: true, status, bringUpStatus, failingQueueIndex, failingAddressKind, failingPhysicalAddress, plannedQueueSize, deviceMaximumQueueSize, unsupportedFeatureMask, VirtioNetQueueMmioSetupPlanner.RequiredCommandCount, availableCommandCount, commandCount: 0, default);
    }
}
