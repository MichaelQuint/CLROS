using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockRequestType : uint
{
    Read = 0,
    Write = 1
}

public enum VirtioBlockDeviceStatus : byte
{
    Ok = 0,
    IoError = 1,
    Unsupported = 2
}

public enum VirtioBlockDescriptorAccess
{
    DeviceReads = 0,
    DeviceWrites = 1
}

public enum VirtioBlockRequestPlanStatus
{
    Success = 0,
    UnsupportedRequestType = 1,
    InvalidSectorCount = 2,
    DataByteCountOverflow = 3,
    SectorRangeOverflow = 4,
    HeaderDestinationTooSmall = 5,
    WriteRejectedReadOnlyMode = 6
}

public enum VirtioBlockQueueReadinessStatus
{
    Ready = 0,
    MissingReadOnlyFeature = 1,
    QueueTooSmall = 2,
    RequestPlanFailed = 3,
    WriteRejectedReadOnlyMode = 4
}

public static class VirtioBlockFeatureBits
{
    public const int ReadOnlyBit = 5;
    public const ulong ReadOnly = 1UL << ReadOnlyBit;
}

public readonly struct VirtioBlockDescriptorMetadata
{
    public const ushort NextFlag = 1;
    public const ushort DeviceWriteFlag = 2;

    public VirtioBlockDescriptorMetadata(
        ushort index,
        uint length,
        VirtioBlockDescriptorAccess access,
        bool hasNext,
        ushort nextIndex)
    {
        Index = index;
        Length = length;
        Access = access;
        HasNext = hasNext;
        NextIndex = nextIndex;
    }

    public ushort Index { get; }

    public uint Length { get; }

    public VirtioBlockDescriptorAccess Access { get; }

    public bool HasNext { get; }

    public ushort NextIndex { get; }

    public bool DeviceWrites => Access == VirtioBlockDescriptorAccess.DeviceWrites;

    public bool DeviceReads => Access == VirtioBlockDescriptorAccess.DeviceReads;

    public ushort Flags
    {
        get
        {
            ushort flags = 0;

            if (HasNext)
            {
                flags |= NextFlag;
            }

            if (DeviceWrites)
            {
                flags |= DeviceWriteFlag;
            }

            return flags;
        }
    }
}

public readonly struct VirtioBlockRequestPlan
{
    public const uint SectorSizeBytes = 512;
    public const uint RequestHeaderLength = 16;
    public const uint StatusLength = 1;
    public const ushort HeaderDescriptorIndex = 0;
    public const ushort DataDescriptorIndex = 1;
    public const ushort StatusDescriptorIndex = 2;
    public const ushort RequiredDescriptorCount = 3;

    private VirtioBlockRequestPlan(
        VirtioBlockRequestType requestType,
        ulong sector,
        uint sectorCount,
        uint dataByteCount,
        VirtioBlockDescriptorMetadata headerDescriptor,
        VirtioBlockDescriptorMetadata dataDescriptor,
        VirtioBlockDescriptorMetadata statusDescriptor)
    {
        RequestType = requestType;
        Sector = sector;
        SectorCount = sectorCount;
        DataByteCount = dataByteCount;
        HeaderDescriptor = headerDescriptor;
        DataDescriptor = dataDescriptor;
        StatusDescriptor = statusDescriptor;
    }

    public VirtioBlockRequestType RequestType { get; }

    public ulong Sector { get; }

    public uint SectorCount { get; }

    public uint DataByteCount { get; }

    public VirtioBlockDescriptorMetadata HeaderDescriptor { get; }

    public VirtioBlockDescriptorMetadata DataDescriptor { get; }

    public VirtioBlockDescriptorMetadata StatusDescriptor { get; }

    public static VirtioBlockRequestPlanResult Create(
        VirtioBlockRequestType requestType,
        ulong sector,
        uint sectorCount,
        bool readOnlyMode)
    {
        if (requestType != VirtioBlockRequestType.Read &&
            requestType != VirtioBlockRequestType.Write)
        {
            return VirtioBlockRequestPlanResult.Fail(
                VirtioBlockRequestPlanStatus.UnsupportedRequestType);
        }

        if (readOnlyMode && requestType == VirtioBlockRequestType.Write)
        {
            return VirtioBlockRequestPlanResult.Fail(
                VirtioBlockRequestPlanStatus.WriteRejectedReadOnlyMode);
        }

        if (sectorCount == 0)
        {
            return VirtioBlockRequestPlanResult.Fail(
                VirtioBlockRequestPlanStatus.InvalidSectorCount);
        }

        if (sector > ulong.MaxValue - sectorCount)
        {
            return VirtioBlockRequestPlanResult.Fail(
                VirtioBlockRequestPlanStatus.SectorRangeOverflow);
        }

        ulong dataBytes = (ulong)sectorCount * SectorSizeBytes;
        if (dataBytes > uint.MaxValue)
        {
            return VirtioBlockRequestPlanResult.Fail(
                VirtioBlockRequestPlanStatus.DataByteCountOverflow);
        }

        VirtioBlockDescriptorMetadata headerDescriptor = new VirtioBlockDescriptorMetadata(
            HeaderDescriptorIndex,
            RequestHeaderLength,
            VirtioBlockDescriptorAccess.DeviceReads,
            hasNext: true,
            DataDescriptorIndex);
        VirtioBlockDescriptorMetadata dataDescriptor = new VirtioBlockDescriptorMetadata(
            DataDescriptorIndex,
            (uint)dataBytes,
            requestType == VirtioBlockRequestType.Read
                ? VirtioBlockDescriptorAccess.DeviceWrites
                : VirtioBlockDescriptorAccess.DeviceReads,
            hasNext: true,
            StatusDescriptorIndex);
        VirtioBlockDescriptorMetadata statusDescriptor = new VirtioBlockDescriptorMetadata(
            StatusDescriptorIndex,
            StatusLength,
            VirtioBlockDescriptorAccess.DeviceWrites,
            hasNext: false,
            nextIndex: 0);

        return VirtioBlockRequestPlanResult.Success(new VirtioBlockRequestPlan(
            requestType,
            sector,
            sectorCount,
            (uint)dataBytes,
            headerDescriptor,
            dataDescriptor,
            statusDescriptor));
    }

    public VirtioBlockRequestPlanStatus WriteHeader(Span<byte> destination)
    {
        if (destination.Length < RequestHeaderLength)
        {
            return VirtioBlockRequestPlanStatus.HeaderDestinationTooSmall;
        }

        EndianPrimitives.WriteUInt32LittleEndian(
            destination.Slice(0, 4),
            (uint)RequestType);
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(4, 4), 0);
        EndianPrimitives.WriteUInt64LittleEndian(destination.Slice(8, 8), Sector);
        return VirtioBlockRequestPlanStatus.Success;
    }
}

public readonly struct VirtioBlockRequestPlanResult
{
    private VirtioBlockRequestPlanResult(
        VirtioBlockRequestPlanStatus status,
        VirtioBlockRequestPlan plan)
    {
        IsInitialized = true;
        Status = status;
        Plan = plan;
    }

    public bool IsInitialized { get; }

    public VirtioBlockRequestPlanStatus Status { get; }

    public VirtioBlockRequestPlan Plan { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockRequestPlanStatus.Success;

    public static VirtioBlockRequestPlanResult Success(VirtioBlockRequestPlan plan)
    {
        return new VirtioBlockRequestPlanResult(
            VirtioBlockRequestPlanStatus.Success,
            plan);
    }

    public static VirtioBlockRequestPlanResult Fail(VirtioBlockRequestPlanStatus status)
    {
        return new VirtioBlockRequestPlanResult(status, default);
    }
}

public readonly struct VirtioBlockQueueReadinessResult
{
    private VirtioBlockQueueReadinessResult(
        VirtioBlockQueueReadinessStatus status,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtioBlockRequestPlan requestPlan,
        ushort queueSize)
    {
        IsInitialized = true;
        Status = status;
        RequestPlanStatus = requestPlanStatus;
        RequestPlan = requestPlan;
        QueueSize = queueSize;
    }

    public bool IsInitialized { get; }

    public VirtioBlockQueueReadinessStatus Status { get; }

    public VirtioBlockRequestPlanStatus RequestPlanStatus { get; }

    public VirtioBlockRequestPlan RequestPlan { get; }

    public ushort QueueSize { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockQueueReadinessStatus.Ready;

    public static VirtioBlockQueueReadinessResult Ready(
        VirtioBlockRequestPlan requestPlan,
        ushort queueSize)
    {
        return new VirtioBlockQueueReadinessResult(
            VirtioBlockQueueReadinessStatus.Ready,
            VirtioBlockRequestPlanStatus.Success,
            requestPlan,
            queueSize);
    }

    public static VirtioBlockQueueReadinessResult Fail(
        VirtioBlockQueueReadinessStatus status,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        ushort queueSize)
    {
        return new VirtioBlockQueueReadinessResult(
            status,
            requestPlanStatus,
            default,
            queueSize);
    }
}

public static class VirtioBlockQueueReadiness
{
    public static VirtioBlockQueueReadinessResult ValidateReadOnlyQueue(
        ulong negotiatedFeatures,
        ushort queueSize,
        VirtioBlockRequestPlanResult requestPlan)
    {
        if ((negotiatedFeatures & VirtioBlockFeatureBits.ReadOnly) == 0)
        {
            return VirtioBlockQueueReadinessResult.Fail(
                VirtioBlockQueueReadinessStatus.MissingReadOnlyFeature,
                requestPlan.Status,
                queueSize);
        }

        if (queueSize < VirtioBlockRequestPlan.RequiredDescriptorCount)
        {
            return VirtioBlockQueueReadinessResult.Fail(
                VirtioBlockQueueReadinessStatus.QueueTooSmall,
                requestPlan.Status,
                queueSize);
        }

        if (!requestPlan.Succeeded)
        {
            return VirtioBlockQueueReadinessResult.Fail(
                VirtioBlockQueueReadinessStatus.RequestPlanFailed,
                requestPlan.Status,
                queueSize);
        }

        if (requestPlan.Plan.RequestType == VirtioBlockRequestType.Write)
        {
            return VirtioBlockQueueReadinessResult.Fail(
                VirtioBlockQueueReadinessStatus.WriteRejectedReadOnlyMode,
                VirtioBlockRequestPlanStatus.WriteRejectedReadOnlyMode,
                queueSize);
        }

        return VirtioBlockQueueReadinessResult.Ready(requestPlan.Plan, queueSize);
    }
}
