using OS.Std.Binary;

namespace OS.Drivers;

public enum VmbusSyntheticDeviceClass
{
    Unknown = 0,
    Network = 1,
    Storage = 2
}

public enum VmbusOfferPlanStatus
{
    Success = 0,
    MalformedLength = 1,
    UnsupportedMessageType = 2,
    ZeroInterfaceTypeGuid = 3,
    ZeroInstanceGuid = 4,
    UnsupportedInterfaceTypeGuid = 5,
    ZeroChannelId = 6,
    MetadataTooLong = 7,
    MetadataContainsInvalidAscii = 8,
    DestinationTooSmall = 9,
    OfferParseFailed = 10,
    DuplicateChannelId = 11
}

public readonly struct VmbusGuid
{
    public VmbusGuid(
        uint data1,
        ushort data2,
        ushort data3,
        byte data4A,
        byte data4B,
        byte data4C,
        byte data4D,
        byte data4E,
        byte data4F,
        byte data4G,
        byte data4H)
    {
        Data1 = data1;
        Data2 = data2;
        Data3 = data3;
        Data4A = data4A;
        Data4B = data4B;
        Data4C = data4C;
        Data4D = data4D;
        Data4E = data4E;
        Data4F = data4F;
        Data4G = data4G;
        Data4H = data4H;
    }

    public uint Data1 { get; }

    public ushort Data2 { get; }

    public ushort Data3 { get; }

    public byte Data4A { get; }

    public byte Data4B { get; }

    public byte Data4C { get; }

    public byte Data4D { get; }

    public byte Data4E { get; }

    public byte Data4F { get; }

    public byte Data4G { get; }

    public byte Data4H { get; }

    public bool IsZero =>
        Data1 == 0 &&
        Data2 == 0 &&
        Data3 == 0 &&
        Data4A == 0 &&
        Data4B == 0 &&
        Data4C == 0 &&
        Data4D == 0 &&
        Data4E == 0 &&
        Data4F == 0 &&
        Data4G == 0 &&
        Data4H == 0;

    public bool Equals(VmbusGuid other)
    {
        return Data1 == other.Data1 &&
            Data2 == other.Data2 &&
            Data3 == other.Data3 &&
            Data4A == other.Data4A &&
            Data4B == other.Data4B &&
            Data4C == other.Data4C &&
            Data4D == other.Data4D &&
            Data4E == other.Data4E &&
            Data4F == other.Data4F &&
            Data4G == other.Data4G &&
            Data4H == other.Data4H;
    }
}

public readonly struct VmbusOfferDescriptor
{
    public VmbusOfferDescriptor(
        uint messageType,
        VmbusGuid interfaceTypeGuid,
        VmbusGuid instanceGuid,
        VmbusSyntheticDeviceClass deviceClass,
        ushort subChannelIndex,
        uint childRelationId,
        byte monitorId,
        bool hasMonitorNotificationFacility,
        uint connectionId,
        int metadataLength)
    {
        MessageType = messageType;
        InterfaceTypeGuid = interfaceTypeGuid;
        InstanceGuid = instanceGuid;
        DeviceClass = deviceClass;
        SubChannelIndex = subChannelIndex;
        ChildRelationId = childRelationId;
        MonitorId = monitorId;
        HasMonitorNotificationFacility = hasMonitorNotificationFacility;
        ConnectionId = connectionId;
        MetadataLength = metadataLength;
    }

    public uint MessageType { get; }

    public VmbusGuid InterfaceTypeGuid { get; }

    public VmbusGuid InstanceGuid { get; }

    public VmbusSyntheticDeviceClass DeviceClass { get; }

    public ushort SubChannelIndex { get; }

    public uint ChildRelationId { get; }

    public byte MonitorId { get; }

    public bool HasMonitorNotificationFacility { get; }

    public uint ConnectionId { get; }

    public int MetadataLength { get; }
}

public readonly struct VmbusOfferParseResult
{
    private VmbusOfferParseResult(
        bool isInitialized,
        VmbusOfferPlanStatus status,
        VmbusOfferDescriptor descriptor,
        int expectedLength,
        int actualLength)
    {
        IsInitialized = isInitialized;
        Status = status;
        Descriptor = descriptor;
        ExpectedLength = expectedLength;
        ActualLength = actualLength;
    }

    public VmbusOfferPlanStatus Status { get; }

    public VmbusOfferDescriptor Descriptor { get; }

    public int ExpectedLength { get; }

    public int ActualLength { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VmbusOfferPlanStatus.Success;

    public static VmbusOfferParseResult Success(VmbusOfferDescriptor descriptor)
    {
        return new VmbusOfferParseResult(
            isInitialized: true,
            VmbusOfferPlanStatus.Success,
            descriptor,
            VmbusOfferPlan.OfferMessageLength,
            VmbusOfferPlan.OfferMessageLength);
    }

    public static VmbusOfferParseResult Fail(
        VmbusOfferPlanStatus status,
        int actualLength)
    {
        return new VmbusOfferParseResult(
            isInitialized: true,
            status,
            default,
            VmbusOfferPlan.OfferMessageLength,
            actualLength);
    }
}

public readonly struct VmbusOfferCatalogParseResult
{
    private VmbusOfferCatalogParseResult(
        bool isInitialized,
        VmbusOfferPlanStatus status,
        int offerCount,
        int parsedOfferCount,
        int requiredOfferCapacity,
        int failingOfferIndex,
        VmbusOfferPlanStatus failingOfferStatus,
        uint duplicateChannelId)
    {
        IsInitialized = isInitialized;
        Status = status;
        OfferCount = offerCount;
        ParsedOfferCount = parsedOfferCount;
        RequiredOfferCapacity = requiredOfferCapacity;
        FailingOfferIndex = failingOfferIndex;
        FailingOfferStatus = failingOfferStatus;
        DuplicateChannelId = duplicateChannelId;
    }

    public const int NoFailingOfferIndex = -1;

    public VmbusOfferPlanStatus Status { get; }

    public int OfferCount { get; }

    public int ParsedOfferCount { get; }

    public int RequiredOfferCapacity { get; }

    public int FailingOfferIndex { get; }

    public VmbusOfferPlanStatus FailingOfferStatus { get; }

    public uint DuplicateChannelId { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VmbusOfferPlanStatus.Success;

    public static VmbusOfferCatalogParseResult Success(
        int offerCount,
        int parsedOfferCount)
    {
        return new VmbusOfferCatalogParseResult(
            isInitialized: true,
            VmbusOfferPlanStatus.Success,
            offerCount,
            parsedOfferCount,
            parsedOfferCount,
            NoFailingOfferIndex,
            VmbusOfferPlanStatus.Success,
            duplicateChannelId: 0);
    }

    public static VmbusOfferCatalogParseResult Fail(
        VmbusOfferPlanStatus status,
        int offerCount,
        int parsedOfferCount,
        int requiredOfferCapacity,
        int failingOfferIndex,
        VmbusOfferPlanStatus failingOfferStatus,
        uint duplicateChannelId)
    {
        return new VmbusOfferCatalogParseResult(
            isInitialized: true,
            status,
            offerCount,
            parsedOfferCount,
            requiredOfferCapacity,
            failingOfferIndex,
            failingOfferStatus,
            duplicateChannelId);
    }
}

public static class VmbusOfferPlan
{
    public const uint OfferChannelMessageType = 1;
    public const int OfferMessageLength = 192;
    public const int UserDefinedDataLength = 120;
    public const int MaximumFixtureMetadataLength = 64;
    public const byte HasMonitorNotificationFacilityFlag = 0x01;

    public static VmbusGuid NetVscInterfaceTypeGuid =>
        new VmbusGuid(
            0xF8615163,
            0xDF3E,
            0x46C5,
            0x91,
            0x3F,
            0xF2,
            0xD2,
            0xF9,
            0x65,
            0xED,
            0x0E);

    public static VmbusGuid StorVscInterfaceTypeGuid =>
        new VmbusGuid(
            0xBA6163D9,
            0x04A1,
            0x4D29,
            0xB6,
            0x05,
            0x72,
            0xE2,
            0xFF,
            0xB1,
            0xDC,
            0x7F);

    public static VmbusOfferParseResult ParseOffer(ReadOnlySpan<byte> source)
    {
        if (source.Length != OfferMessageLength)
        {
            return VmbusOfferParseResult.Fail(
                VmbusOfferPlanStatus.MalformedLength,
                source.Length);
        }

        uint messageType = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(0, 4));
        if (messageType != OfferChannelMessageType)
        {
            return VmbusOfferParseResult.Fail(
                VmbusOfferPlanStatus.UnsupportedMessageType,
                source.Length);
        }

        VmbusGuid interfaceTypeGuid = ReadGuid(source.Slice(4, 16));
        if (interfaceTypeGuid.IsZero)
        {
            return VmbusOfferParseResult.Fail(
                VmbusOfferPlanStatus.ZeroInterfaceTypeGuid,
                source.Length);
        }

        VmbusGuid instanceGuid = ReadGuid(source.Slice(20, 16));
        if (instanceGuid.IsZero)
        {
            return VmbusOfferParseResult.Fail(
                VmbusOfferPlanStatus.ZeroInstanceGuid,
                source.Length);
        }

        VmbusSyntheticDeviceClass deviceClass = ClassifyInterfaceType(interfaceTypeGuid);
        if (deviceClass == VmbusSyntheticDeviceClass.Unknown)
        {
            return VmbusOfferParseResult.Fail(
                VmbusOfferPlanStatus.UnsupportedInterfaceTypeGuid,
                source.Length);
        }

        VmbusOfferPlanStatus metadataStatus = GetFixtureMetadataLength(
            source.Slice(56, UserDefinedDataLength),
            out int metadataLength);
        if (metadataStatus != VmbusOfferPlanStatus.Success)
        {
            return VmbusOfferParseResult.Fail(metadataStatus, source.Length);
        }

        ushort subChannelIndex = EndianPrimitives.ReadUInt16LittleEndian(source.Slice(176, 2));
        uint childRelationId = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(180, 4));
        if (childRelationId == 0)
        {
            return VmbusOfferParseResult.Fail(
                VmbusOfferPlanStatus.ZeroChannelId,
                source.Length);
        }

        byte monitorId = source[184];
        byte flags1 = source[185];
        uint connectionId = EndianPrimitives.ReadUInt32LittleEndian(source.Slice(188, 4));

        VmbusOfferDescriptor descriptor = new VmbusOfferDescriptor(
            messageType,
            interfaceTypeGuid,
            instanceGuid,
            deviceClass,
            subChannelIndex,
            childRelationId,
            monitorId,
            (flags1 & HasMonitorNotificationFacilityFlag) != 0,
            connectionId,
            metadataLength);
        return VmbusOfferParseResult.Success(descriptor);
    }

    public static VmbusOfferCatalogParseResult ParseCatalog(
        ReadOnlySpan<byte> source,
        Span<VmbusOfferDescriptor> destination)
    {
        if ((source.Length % OfferMessageLength) != 0)
        {
            return VmbusOfferCatalogParseResult.Fail(
                VmbusOfferPlanStatus.MalformedLength,
                offerCount: 0,
                parsedOfferCount: 0,
                requiredOfferCapacity: 0,
                failingOfferIndex: VmbusOfferCatalogParseResult.NoFailingOfferIndex,
                failingOfferStatus: VmbusOfferPlanStatus.MalformedLength,
                duplicateChannelId: 0);
        }

        int offerCount = source.Length / OfferMessageLength;
        if (destination.Length < offerCount)
        {
            return VmbusOfferCatalogParseResult.Fail(
                VmbusOfferPlanStatus.DestinationTooSmall,
                offerCount,
                parsedOfferCount: 0,
                requiredOfferCapacity: offerCount,
                failingOfferIndex: destination.Length,
                failingOfferStatus: VmbusOfferPlanStatus.DestinationTooSmall,
                duplicateChannelId: 0);
        }

        int parsedOfferCount = 0;
        for (int index = 0; index < offerCount; index++)
        {
            VmbusOfferParseResult parseResult = ParseOffer(
                source.Slice(index * OfferMessageLength, OfferMessageLength));
            if (!parseResult.Succeeded)
            {
                return VmbusOfferCatalogParseResult.Fail(
                    VmbusOfferPlanStatus.OfferParseFailed,
                    offerCount,
                    parsedOfferCount,
                    requiredOfferCapacity: offerCount,
                    failingOfferIndex: index,
                    failingOfferStatus: parseResult.Status,
                    duplicateChannelId: 0);
            }

            uint childRelationId = parseResult.Descriptor.ChildRelationId;
            if (ContainsChannelId(destination[..parsedOfferCount], childRelationId))
            {
                return VmbusOfferCatalogParseResult.Fail(
                    VmbusOfferPlanStatus.DuplicateChannelId,
                    offerCount,
                    parsedOfferCount,
                    requiredOfferCapacity: offerCount,
                    failingOfferIndex: index,
                    failingOfferStatus: VmbusOfferPlanStatus.DuplicateChannelId,
                    duplicateChannelId: childRelationId);
            }

            destination[parsedOfferCount] = parseResult.Descriptor;
            parsedOfferCount++;
        }

        return VmbusOfferCatalogParseResult.Success(offerCount, parsedOfferCount);
    }

    public static VmbusSyntheticDeviceClass ClassifyInterfaceType(VmbusGuid interfaceTypeGuid)
    {
        if (interfaceTypeGuid.Equals(NetVscInterfaceTypeGuid))
        {
            return VmbusSyntheticDeviceClass.Network;
        }

        if (interfaceTypeGuid.Equals(StorVscInterfaceTypeGuid))
        {
            return VmbusSyntheticDeviceClass.Storage;
        }

        return VmbusSyntheticDeviceClass.Unknown;
    }

    public static bool IsSupportedInterfaceType(VmbusGuid interfaceTypeGuid)
    {
        return ClassifyInterfaceType(interfaceTypeGuid) != VmbusSyntheticDeviceClass.Unknown;
    }

    private static VmbusGuid ReadGuid(ReadOnlySpan<byte> source)
    {
        return new VmbusGuid(
            EndianPrimitives.ReadUInt32LittleEndian(source[..4]),
            EndianPrimitives.ReadUInt16LittleEndian(source.Slice(4, 2)),
            EndianPrimitives.ReadUInt16LittleEndian(source.Slice(6, 2)),
            source[8],
            source[9],
            source[10],
            source[11],
            source[12],
            source[13],
            source[14],
            source[15]);
    }

    private static VmbusOfferPlanStatus GetFixtureMetadataLength(
        ReadOnlySpan<byte> source,
        out int metadataLength)
    {
        metadataLength = 0;

        for (int index = 0; index < source.Length; index++)
        {
            byte value = source[index];
            if (value == 0)
            {
                metadataLength = index;
                return VmbusOfferPlanStatus.Success;
            }

            if (index >= MaximumFixtureMetadataLength)
            {
                return VmbusOfferPlanStatus.MetadataTooLong;
            }

            if (value < 0x20 || value > 0x7E)
            {
                return VmbusOfferPlanStatus.MetadataContainsInvalidAscii;
            }
        }

        return VmbusOfferPlanStatus.MetadataTooLong;
    }

    private static bool ContainsChannelId(
        ReadOnlySpan<VmbusOfferDescriptor> offers,
        uint childRelationId)
    {
        for (int index = 0; index < offers.Length; index++)
        {
            if (offers[index].ChildRelationId == childRelationId)
            {
                return true;
            }
        }

        return false;
    }
}
