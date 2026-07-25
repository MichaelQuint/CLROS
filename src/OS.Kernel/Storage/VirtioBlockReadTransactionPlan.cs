namespace OS.Storage;

public enum VirtioBlockReadTransactionPlanStatus
{
    Success = 0,
    InvalidSectorGeometry = 1,
    InvalidSectorRange = 2,
    DataByteCountOverflow = 3,
    DestinationTooSmall = 4,
    QueueTooSmall = 5,
    RequestHeaderDestinationTooSmall = 6,
    StatusDestinationTooSmall = 7,
    RequestPlanFailed = 8
}

public readonly struct VirtioBlockReadTransactionPlan
{
    public const uint RequiredSectorSizeBytes = VirtioBlockRequestPlan.SectorSizeBytes;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsMmio = false;
    public const bool PerformsInterrupts = false;
    public const bool PerformsDiskRead = false;

    private VirtioBlockReadTransactionPlan(
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ulong lastSectorInclusive,
        uint dataByteCount,
        ushort queueSize,
        VirtioBlockRequestPlan requestPlan)
    {
        SectorSizeBytes = sectorSizeBytes;
        DeviceSectorCount = deviceSectorCount;
        FirstSector = firstSector;
        SectorCount = sectorCount;
        LastSectorInclusive = lastSectorInclusive;
        DataByteCount = dataByteCount;
        QueueSize = queueSize;
        RequestPlan = requestPlan;
    }

    public uint SectorSizeBytes { get; }

    public ulong DeviceSectorCount { get; }

    public ulong FirstSector { get; }

    public uint SectorCount { get; }

    public ulong LastSectorInclusive { get; }

    public uint DataByteCount { get; }

    public ushort QueueSize { get; }

    public VirtioBlockRequestPlan RequestPlan { get; }

    public static VirtioBlockReadTransactionPlanResult Create(
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        Span<byte> requestHeader,
        Span<byte> destination,
        Span<byte> status)
    {
        if (sectorSizeBytes != RequiredSectorSizeBytes || deviceSectorCount == 0)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.InvalidSectorGeometry);
        }

        if (queueSize < VirtioBlockRequestPlan.RequiredDescriptorCount)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.QueueTooSmall);
        }

        if (sectorCount == 0 || firstSector >= deviceSectorCount)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.InvalidSectorRange);
        }

        ulong remainingSectors = deviceSectorCount - firstSector;
        if (sectorCount > remainingSectors)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.InvalidSectorRange);
        }

        ulong dataByteCount = (ulong)sectorCount * sectorSizeBytes;
        if (dataByteCount > uint.MaxValue)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.DataByteCountOverflow);
        }

        if (dataByteCount > (ulong)destination.Length)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.DestinationTooSmall);
        }

        if (requestHeader.Length < VirtioBlockRequestPlan.RequestHeaderLength)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.RequestHeaderDestinationTooSmall);
        }

        if (status.Length < VirtioBlockRequestPlan.StatusLength)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.StatusDestinationTooSmall);
        }

        VirtioBlockRequestPlanResult requestPlan = VirtioBlockRequestPlan.Create(
            VirtioBlockRequestType.Read,
            firstSector,
            sectorCount,
            readOnlyMode: true);
        if (!requestPlan.Succeeded)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.RequestPlanFailed,
                requestPlan.Status);
        }

        VirtioBlockRequestPlanStatus headerStatus = requestPlan.Plan.WriteHeader(requestHeader);
        if (headerStatus != VirtioBlockRequestPlanStatus.Success)
        {
            return VirtioBlockReadTransactionPlanResult.Fail(
                VirtioBlockReadTransactionPlanStatus.RequestHeaderDestinationTooSmall,
                headerStatus);
        }

        ulong lastSectorInclusive = firstSector + sectorCount - 1UL;
        return VirtioBlockReadTransactionPlanResult.Success(new VirtioBlockReadTransactionPlan(
            sectorSizeBytes,
            deviceSectorCount,
            firstSector,
            sectorCount,
            lastSectorInclusive,
            (uint)dataByteCount,
            queueSize,
            requestPlan.Plan));
    }
}

public readonly struct VirtioBlockReadTransactionPlanResult
{
    private VirtioBlockReadTransactionPlanResult(
        VirtioBlockReadTransactionPlanStatus status,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtioBlockReadTransactionPlan plan)
    {
        IsInitialized = true;
        Status = status;
        RequestPlanStatus = requestPlanStatus;
        Plan = plan;
    }

    public bool IsInitialized { get; }

    public VirtioBlockReadTransactionPlanStatus Status { get; }

    public VirtioBlockRequestPlanStatus RequestPlanStatus { get; }

    public VirtioBlockReadTransactionPlan Plan { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockReadTransactionPlanStatus.Success;

    public static VirtioBlockReadTransactionPlanResult Success(
        VirtioBlockReadTransactionPlan plan)
    {
        return new VirtioBlockReadTransactionPlanResult(
            VirtioBlockReadTransactionPlanStatus.Success,
            VirtioBlockRequestPlanStatus.Success,
            plan);
    }

    public static VirtioBlockReadTransactionPlanResult Fail(
        VirtioBlockReadTransactionPlanStatus status)
    {
        return new VirtioBlockReadTransactionPlanResult(
            status,
            VirtioBlockRequestPlanStatus.Success,
            default);
    }

    public static VirtioBlockReadTransactionPlanResult Fail(
        VirtioBlockReadTransactionPlanStatus status,
        VirtioBlockRequestPlanStatus requestPlanStatus)
    {
        return new VirtioBlockReadTransactionPlanResult(
            status,
            requestPlanStatus,
            default);
    }
}
