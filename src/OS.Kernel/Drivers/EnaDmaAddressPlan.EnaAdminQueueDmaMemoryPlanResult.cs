namespace OS.Drivers;
public readonly struct EnaAdminQueueDmaMemoryPlanResult
{
    private EnaAdminQueueDmaMemoryPlanResult(bool isInitialized, EnaDmaAddressPlanStatus status, EnaAdminQueueLayoutPlanStatus queueLayoutStatus, EnaDmaRegionKind failingRegionKind, uint physicalAddressWidth, ulong requiredLengthBytes, ulong providedLengthBytes, ulong maximumAddress, EnaAdminQueueDmaMemoryPlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        QueueLayoutStatus = queueLayoutStatus;
        FailingRegionKind = failingRegionKind;
        PhysicalAddressWidth = physicalAddressWidth;
        RequiredLengthBytes = requiredLengthBytes;
        ProvidedLengthBytes = providedLengthBytes;
        MaximumAddress = maximumAddress;
        Plan = plan;
    }

    public EnaDmaAddressPlanStatus Status { get; }
    public EnaAdminQueueLayoutPlanStatus QueueLayoutStatus { get; }
    public EnaDmaRegionKind FailingRegionKind { get; }
    public uint PhysicalAddressWidth { get; }
    public ulong RequiredLengthBytes { get; }
    public ulong ProvidedLengthBytes { get; }
    public ulong MaximumAddress { get; }
    public EnaAdminQueueDmaMemoryPlan Plan { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == EnaDmaAddressPlanStatus.Success;

    internal static EnaAdminQueueDmaMemoryPlanResult Success(EnaDeviceAttributes deviceAttributes, ulong maximumAddress, EnaAdminQueueDmaMemoryPlan plan)
    {
        return new EnaAdminQueueDmaMemoryPlanResult(isInitialized: true, EnaDmaAddressPlanStatus.Success, EnaAdminQueueLayoutPlanStatus.Success, EnaDmaRegionKind.None, deviceAttributes.PhysicalAddressWidth, plan.QueueLayout.TotalRequiredBytes, plan.ContiguousRegion.LengthBytes, maximumAddress, plan);
    }

    internal static EnaAdminQueueDmaMemoryPlanResult Fail(EnaDmaAddressPlanStatus status, EnaAdminQueueLayoutPlanStatus queueLayoutStatus, EnaDmaRegionKind failingRegionKind, uint physicalAddressWidth, ulong requiredLengthBytes, ulong providedLengthBytes, ulong maximumAddress)
    {
        return new EnaAdminQueueDmaMemoryPlanResult(isInitialized: true, status, queueLayoutStatus, failingRegionKind, physicalAddressWidth, requiredLengthBytes, providedLengthBytes, maximumAddress, default);
    }
}
