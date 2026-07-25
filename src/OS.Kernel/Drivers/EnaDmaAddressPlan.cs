namespace OS.Drivers;

public enum EnaDmaAddressPlanStatus
{
    Success = 0,
    InvalidAddressWidth = 1,
    ZeroLength = 2,
    InvalidAlignment = 3,
    UnalignedBaseAddress = 4,
    AddressOverflow = 5,
    AddressOutOfRange = 6,
    QueueLayoutRejected = 7,
    QueueMemoryTooSmall = 8
}

public enum EnaDmaRegionKind
{
    None = 0,
    Generic = 1,
    ContiguousAdminQueueMemory = 2,
    AdminSubmissionQueue = 3,
    AdminCompletionQueue = 4,
    TxRing = 5,
    RxRing = 6,
    PacketBuffer = 7
}

public readonly struct EnaDmaPhysicalRegion
{
    public EnaDmaPhysicalRegion(
        EnaDmaRegionKind kind,
        ulong basePhysicalAddress,
        ulong lengthBytes,
        uint alignment)
    {
        Kind = kind;
        BasePhysicalAddress = basePhysicalAddress;
        LengthBytes = lengthBytes;
        Alignment = alignment;
    }

    public EnaDmaRegionKind Kind { get; }

    public ulong BasePhysicalAddress { get; }

    public ulong LengthBytes { get; }

    public uint Alignment { get; }

    public bool IsBaseAligned =>
        Alignment != 0 && (BasePhysicalAddress % Alignment) == 0;
}

public readonly struct EnaDmaRegionValidationResult
{
    private EnaDmaRegionValidationResult(
        bool isInitialized,
        EnaDmaAddressPlanStatus status,
        uint physicalAddressWidth,
        ulong maximumAddress,
        ulong endExclusive,
        EnaDmaPhysicalRegion region)
    {
        IsInitialized = isInitialized;
        Status = status;
        PhysicalAddressWidth = physicalAddressWidth;
        MaximumAddress = maximumAddress;
        EndExclusive = endExclusive;
        Region = region;
    }

    public EnaDmaAddressPlanStatus Status { get; }

    public uint PhysicalAddressWidth { get; }

    public ulong MaximumAddress { get; }

    public ulong EndExclusive { get; }

    public EnaDmaPhysicalRegion Region { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == EnaDmaAddressPlanStatus.Success;

    internal static EnaDmaRegionValidationResult Create(
        EnaDmaAddressPlanStatus status,
        uint physicalAddressWidth,
        ulong maximumAddress,
        ulong endExclusive,
        EnaDmaPhysicalRegion region)
    {
        return new EnaDmaRegionValidationResult(
            isInitialized: true,
            status,
            physicalAddressWidth,
            maximumAddress,
            endExclusive,
            region);
    }
}

public readonly struct EnaAdminQueueDmaMemoryPlan
{
    internal EnaAdminQueueDmaMemoryPlan(
        EnaDmaPhysicalRegion contiguousRegion,
        EnaDmaPhysicalRegion adminSubmissionQueue,
        EnaDmaPhysicalRegion adminCompletionQueue,
        EnaAdminQueueLayoutPlan queueLayout)
    {
        ContiguousRegion = contiguousRegion;
        AdminSubmissionQueue = adminSubmissionQueue;
        AdminCompletionQueue = adminCompletionQueue;
        QueueLayout = queueLayout;
    }

    public EnaDmaPhysicalRegion ContiguousRegion { get; }

    public EnaDmaPhysicalRegion AdminSubmissionQueue { get; }

    public EnaDmaPhysicalRegion AdminCompletionQueue { get; }

    public EnaAdminQueueLayoutPlan QueueLayout { get; }
}

public readonly struct EnaAdminQueueDmaMemoryPlanResult
{
    private EnaAdminQueueDmaMemoryPlanResult(
        bool isInitialized,
        EnaDmaAddressPlanStatus status,
        EnaAdminQueueLayoutPlanStatus queueLayoutStatus,
        EnaDmaRegionKind failingRegionKind,
        uint physicalAddressWidth,
        ulong requiredLengthBytes,
        ulong providedLengthBytes,
        ulong maximumAddress,
        EnaAdminQueueDmaMemoryPlan plan)
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

    internal static EnaAdminQueueDmaMemoryPlanResult Success(
        EnaDeviceAttributes deviceAttributes,
        ulong maximumAddress,
        EnaAdminQueueDmaMemoryPlan plan)
    {
        return new EnaAdminQueueDmaMemoryPlanResult(
            isInitialized: true,
            EnaDmaAddressPlanStatus.Success,
            EnaAdminQueueLayoutPlanStatus.Success,
            EnaDmaRegionKind.None,
            deviceAttributes.PhysicalAddressWidth,
            plan.QueueLayout.TotalRequiredBytes,
            plan.ContiguousRegion.LengthBytes,
            maximumAddress,
            plan);
    }

    internal static EnaAdminQueueDmaMemoryPlanResult Fail(
        EnaDmaAddressPlanStatus status,
        EnaAdminQueueLayoutPlanStatus queueLayoutStatus,
        EnaDmaRegionKind failingRegionKind,
        uint physicalAddressWidth,
        ulong requiredLengthBytes,
        ulong providedLengthBytes,
        ulong maximumAddress)
    {
        return new EnaAdminQueueDmaMemoryPlanResult(
            isInitialized: true,
            status,
            queueLayoutStatus,
            failingRegionKind,
            physicalAddressWidth,
            requiredLengthBytes,
            providedLengthBytes,
            maximumAddress,
            default);
    }
}

public static class EnaDmaAddressPlan
{
    public const bool AllocatesDmaMemory = false;
    public const bool MapsPages = false;
    public const bool ProgramsIommu = false;
    public const bool WritesMmioRegisters = false;
    public const bool RingsDoorbells = false;
    public const bool ConfiguresInterrupts = false;
    public const bool PerformsPacketIo = false;

    public static EnaDmaRegionValidationResult ValidateRegion(
        EnaDeviceAttributes deviceAttributes,
        EnaDmaPhysicalRegion region)
    {
        if (!TryGetMaximumAddress(
            deviceAttributes.PhysicalAddressWidth,
            out ulong maximumAddress))
        {
            return EnaDmaRegionValidationResult.Create(
                EnaDmaAddressPlanStatus.InvalidAddressWidth,
                deviceAttributes.PhysicalAddressWidth,
                maximumAddress,
                endExclusive: 0,
                region);
        }

        if (region.LengthBytes == 0)
        {
            return EnaDmaRegionValidationResult.Create(
                EnaDmaAddressPlanStatus.ZeroLength,
                deviceAttributes.PhysicalAddressWidth,
                maximumAddress,
                endExclusive: region.BasePhysicalAddress,
                region);
        }

        if (!IsPowerOfTwo(region.Alignment))
        {
            return EnaDmaRegionValidationResult.Create(
                EnaDmaAddressPlanStatus.InvalidAlignment,
                deviceAttributes.PhysicalAddressWidth,
                maximumAddress,
                endExclusive: 0,
                region);
        }

        if (!region.IsBaseAligned)
        {
            return EnaDmaRegionValidationResult.Create(
                EnaDmaAddressPlanStatus.UnalignedBaseAddress,
                deviceAttributes.PhysicalAddressWidth,
                maximumAddress,
                endExclusive: 0,
                region);
        }

        if (region.BasePhysicalAddress > ulong.MaxValue - region.LengthBytes)
        {
            return EnaDmaRegionValidationResult.Create(
                EnaDmaAddressPlanStatus.AddressOverflow,
                deviceAttributes.PhysicalAddressWidth,
                maximumAddress,
                endExclusive: 0,
                region);
        }

        ulong endExclusive = region.BasePhysicalAddress + region.LengthBytes;
        ulong lastAddress = endExclusive - 1UL;
        if (region.BasePhysicalAddress > maximumAddress || lastAddress > maximumAddress)
        {
            return EnaDmaRegionValidationResult.Create(
                EnaDmaAddressPlanStatus.AddressOutOfRange,
                deviceAttributes.PhysicalAddressWidth,
                maximumAddress,
                endExclusive,
                region);
        }

        return EnaDmaRegionValidationResult.Create(
            EnaDmaAddressPlanStatus.Success,
            deviceAttributes.PhysicalAddressWidth,
            maximumAddress,
            endExclusive,
            region);
    }

    public static EnaAdminQueueDmaMemoryPlanResult CreateAdminQueueMemoryPlan(
        EnaDeviceAttributes deviceAttributes,
        EnaAdminQueueLayoutPlanResult queueLayoutResult,
        ulong basePhysicalAddress,
        ulong lengthBytes)
    {
        if (!TryGetMaximumAddress(
            deviceAttributes.PhysicalAddressWidth,
            out ulong maximumAddress))
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(
                EnaDmaAddressPlanStatus.InvalidAddressWidth,
                queueLayoutResult.Status,
                EnaDmaRegionKind.ContiguousAdminQueueMemory,
                deviceAttributes.PhysicalAddressWidth,
                queueLayoutResult.TotalRequiredBytes,
                lengthBytes,
                maximumAddress);
        }

        if (!queueLayoutResult.Succeeded)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(
                EnaDmaAddressPlanStatus.QueueLayoutRejected,
                queueLayoutResult.Status,
                EnaDmaRegionKind.ContiguousAdminQueueMemory,
                deviceAttributes.PhysicalAddressWidth,
                queueLayoutResult.TotalRequiredBytes,
                lengthBytes,
                maximumAddress);
        }

        EnaAdminQueueLayoutPlan queueLayout = queueLayoutResult.Plan;
        if (lengthBytes < queueLayout.TotalRequiredBytes)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(
                EnaDmaAddressPlanStatus.QueueMemoryTooSmall,
                queueLayoutResult.Status,
                EnaDmaRegionKind.ContiguousAdminQueueMemory,
                deviceAttributes.PhysicalAddressWidth,
                queueLayout.TotalRequiredBytes,
                lengthBytes,
                maximumAddress);
        }

        EnaDmaPhysicalRegion contiguousRegion = new EnaDmaPhysicalRegion(
            EnaDmaRegionKind.ContiguousAdminQueueMemory,
            basePhysicalAddress,
            lengthBytes,
            queueLayout.BaseAlignment);
        EnaDmaRegionValidationResult contiguousValidation =
            ValidateRegion(deviceAttributes, contiguousRegion);
        if (!contiguousValidation.Succeeded)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(
                contiguousValidation.Status,
                queueLayoutResult.Status,
                EnaDmaRegionKind.ContiguousAdminQueueMemory,
                deviceAttributes.PhysicalAddressWidth,
                queueLayout.TotalRequiredBytes,
                lengthBytes,
                maximumAddress);
        }

        if (!TryAdd(
            basePhysicalAddress,
            queueLayout.AdminQueue.Offset,
            out ulong adminQueueBase))
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(
                EnaDmaAddressPlanStatus.AddressOverflow,
                queueLayoutResult.Status,
                EnaDmaRegionKind.AdminSubmissionQueue,
                deviceAttributes.PhysicalAddressWidth,
                queueLayout.AdminQueue.Length,
                queueLayout.AdminQueue.Length,
                maximumAddress);
        }

        EnaDmaPhysicalRegion adminQueueRegion = new EnaDmaPhysicalRegion(
            EnaDmaRegionKind.AdminSubmissionQueue,
            adminQueueBase,
            queueLayout.AdminQueue.Length,
            queueLayout.AdminQueue.Alignment);
        EnaDmaRegionValidationResult adminQueueValidation =
            ValidateRegion(deviceAttributes, adminQueueRegion);
        if (!adminQueueValidation.Succeeded)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(
                adminQueueValidation.Status,
                queueLayoutResult.Status,
                EnaDmaRegionKind.AdminSubmissionQueue,
                deviceAttributes.PhysicalAddressWidth,
                queueLayout.AdminQueue.Length,
                queueLayout.AdminQueue.Length,
                maximumAddress);
        }

        if (!TryAdd(
            basePhysicalAddress,
            queueLayout.AdminCompletionQueue.Offset,
            out ulong adminCompletionQueueBase))
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(
                EnaDmaAddressPlanStatus.AddressOverflow,
                queueLayoutResult.Status,
                EnaDmaRegionKind.AdminCompletionQueue,
                deviceAttributes.PhysicalAddressWidth,
                queueLayout.AdminCompletionQueue.Length,
                queueLayout.AdminCompletionQueue.Length,
                maximumAddress);
        }

        EnaDmaPhysicalRegion adminCompletionQueueRegion = new EnaDmaPhysicalRegion(
            EnaDmaRegionKind.AdminCompletionQueue,
            adminCompletionQueueBase,
            queueLayout.AdminCompletionQueue.Length,
            queueLayout.AdminCompletionQueue.Alignment);
        EnaDmaRegionValidationResult adminCompletionQueueValidation =
            ValidateRegion(deviceAttributes, adminCompletionQueueRegion);
        if (!adminCompletionQueueValidation.Succeeded)
        {
            return EnaAdminQueueDmaMemoryPlanResult.Fail(
                adminCompletionQueueValidation.Status,
                queueLayoutResult.Status,
                EnaDmaRegionKind.AdminCompletionQueue,
                deviceAttributes.PhysicalAddressWidth,
                queueLayout.AdminCompletionQueue.Length,
                queueLayout.AdminCompletionQueue.Length,
                maximumAddress);
        }

        EnaAdminQueueDmaMemoryPlan plan = new EnaAdminQueueDmaMemoryPlan(
            contiguousRegion,
            adminQueueRegion,
            adminCompletionQueueRegion,
            queueLayout);
        return EnaAdminQueueDmaMemoryPlanResult.Success(
            deviceAttributes,
            maximumAddress,
            plan);
    }

    private static bool TryGetMaximumAddress(
        uint physicalAddressWidth,
        out ulong maximumAddress)
    {
        if (physicalAddressWidth < EnaDeviceAttributesParser.MinimumAddressWidth ||
            physicalAddressWidth > EnaDeviceAttributesParser.MaximumAddressWidth)
        {
            maximumAddress = 0;
            return false;
        }

        maximumAddress = physicalAddressWidth == 64
            ? ulong.MaxValue
            : (1UL << (int)physicalAddressWidth) - 1UL;
        return true;
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool TryAdd(
        ulong left,
        uint right,
        out ulong result)
    {
        if (left > ulong.MaxValue - right)
        {
            result = 0;
            return false;
        }

        result = left + right;
        return true;
    }
}
