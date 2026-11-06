using OS.Std.Binary;

namespace OS.Storage;

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

    public VirtioBlockRequestType RequestType
    {
        get;
    }

    public ulong Sector
    {
        get;
    }

    public uint SectorCount
    {
        get;
    }

    public uint DataByteCount
    {
        get;
    }

    public VirtioBlockDescriptorMetadata HeaderDescriptor
    {
        get;
    }

    public VirtioBlockDescriptorMetadata DataDescriptor
    {
        get;
    }

    public VirtioBlockDescriptorMetadata StatusDescriptor
    {
        get;
    }

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
