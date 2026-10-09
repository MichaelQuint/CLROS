using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;

internal static class VmbusOfferPlanTests
{

    [HostTest("drivers.vmbus.offer parses storage offer", HostLane.Fast, 0)]
    private static bool TestParsesStorageOffer()
    {
        byte[] offer = CreateOffer(
            VmbusOfferPlan.StorVscInterfaceTypeGuid,
            CreateInstanceGuid(0x11),
            childRelationId: 42,
            monitorId: 7,
            subChannelIndex: 0,
            connectionId: 0x01020304,
            metadata: [(byte)'s', (byte)'t', (byte)'o', (byte)'r', (byte)'v', (byte)'s', (byte)'c']);

        VmbusOfferParseResult result = VmbusOfferPlan.ParseOffer(offer);

        return HostCheck.Result(VmbusOfferPlan.OfferChannelMessageType == 1
            && VmbusOfferPlan.OfferMessageLength == 192
            && VmbusOfferPlan.UserDefinedDataLength == 120
            && VmbusOfferPlan.MaximumFixtureMetadataLength == 64
            && VmbusOfferPlan.ClassifyInterfaceType(VmbusOfferPlan.StorVscInterfaceTypeGuid)
                == VmbusSyntheticDeviceClass.Storage
            && result.IsInitialized
            && result.Succeeded
            && result.Status == VmbusOfferPlanStatus.Success
            && result.Descriptor.MessageType == VmbusOfferPlan.OfferChannelMessageType
            && result.Descriptor.DeviceClass == VmbusSyntheticDeviceClass.Storage
            && result.Descriptor.InterfaceTypeGuid.Equals(VmbusOfferPlan.StorVscInterfaceTypeGuid)
            && result.Descriptor.InstanceGuid.Equals(CreateInstanceGuid(0x11))
            && result.Descriptor.ChildRelationId == 42
            && result.Descriptor.MonitorId == 7
            && result.Descriptor.SubChannelIndex == 0
            && result.Descriptor.HasMonitorNotificationFacility
            && result.Descriptor.ConnectionId == 0x01020304
            && result.Descriptor.MetadataLength == 7);
    }

    [HostTest("drivers.vmbus.offer parses network offer", HostLane.Fast, 1)]
    private static bool TestParsesNetworkOffer()
    {
        byte[] networkOffer = CreateOffer(
            VmbusOfferPlan.NetVscInterfaceTypeGuid,
            CreateInstanceGuid(0x22),
            childRelationId: 77,
            monitorId: 9,
            subChannelIndex: 2,
            connectionId: 0xAABBCCDD,
            metadata: [(byte)'n', (byte)'e', (byte)'t', (byte)'v', (byte)'s', (byte)'c']);
        byte[] storageOffer = CreateOffer(
            VmbusOfferPlan.StorVscInterfaceTypeGuid,
            CreateInstanceGuid(0x33),
            childRelationId: 78,
            monitorId: 10,
            subChannelIndex: 0,
            connectionId: 0x11223344);
        byte[] catalog = new byte[VmbusOfferPlan.OfferMessageLength * 2];
        networkOffer.CopyTo(catalog.AsSpan(0, VmbusOfferPlan.OfferMessageLength));
        storageOffer.CopyTo(catalog.AsSpan(VmbusOfferPlan.OfferMessageLength));
        VmbusOfferDescriptor[] descriptors = new VmbusOfferDescriptor[2];

        VmbusOfferParseResult single = VmbusOfferPlan.ParseOffer(networkOffer);
        VmbusOfferCatalogParseResult catalogResult = VmbusOfferPlan.ParseCatalog(catalog, descriptors);

        return HostCheck.Result(VmbusOfferPlan.ClassifyInterfaceType(VmbusOfferPlan.NetVscInterfaceTypeGuid)
                == VmbusSyntheticDeviceClass.Network
            && VmbusOfferPlan.IsSupportedInterfaceType(VmbusOfferPlan.NetVscInterfaceTypeGuid)
            && single.Succeeded
            && single.Descriptor.DeviceClass == VmbusSyntheticDeviceClass.Network
            && single.Descriptor.ChildRelationId == 77
            && single.Descriptor.MonitorId == 9
            && single.Descriptor.SubChannelIndex == 2
            && single.Descriptor.ConnectionId == 0xAABBCCDD
            && single.Descriptor.MetadataLength == 6
            && catalogResult.Succeeded
            && catalogResult.IsInitialized
            && catalogResult.OfferCount == 2
            && catalogResult.ParsedOfferCount == 2
            && descriptors[0].DeviceClass == VmbusSyntheticDeviceClass.Network
            && descriptors[0].ChildRelationId == 77
            && descriptors[1].DeviceClass == VmbusSyntheticDeviceClass.Storage
            && descriptors[1].ChildRelationId == 78);
    }

    [HostTest("drivers.vmbus.offer rejects malformed offer", HostLane.Fast, 2)]
    private static bool TestRejectsMalformedOffer()
    {
        byte[] validOffer = CreateOffer(
            VmbusOfferPlan.NetVscInterfaceTypeGuid,
            CreateInstanceGuid(0x44),
            childRelationId: 10,
            monitorId: 1,
            subChannelIndex: 0,
            connectionId: 2);
        byte[] wrongMessageType = (byte[])validOffer.Clone();
        EndianPrimitives.WriteUInt32LittleEndian(wrongMessageType.AsSpan(0, 4), 99);
        byte[] zeroInterfaceGuid = (byte[])validOffer.Clone();
        zeroInterfaceGuid.AsSpan(4, 16).Clear();
        byte[] zeroInstanceGuid = (byte[])validOffer.Clone();
        zeroInstanceGuid.AsSpan(20, 16).Clear();
        byte[] zeroChannelId = (byte[])validOffer.Clone();
        EndianPrimitives.WriteUInt32LittleEndian(zeroChannelId.AsSpan(180, 4), 0);

        VmbusOfferParseResult shortResult =
            VmbusOfferPlan.ParseOffer(validOffer.AsSpan(0, VmbusOfferPlan.OfferMessageLength - 1));
        VmbusOfferParseResult wrongTypeResult = VmbusOfferPlan.ParseOffer(wrongMessageType);
        VmbusOfferParseResult zeroInterfaceResult = VmbusOfferPlan.ParseOffer(zeroInterfaceGuid);
        VmbusOfferParseResult zeroInstanceResult = VmbusOfferPlan.ParseOffer(zeroInstanceGuid);
        VmbusOfferParseResult zeroChannelResult = VmbusOfferPlan.ParseOffer(zeroChannelId);

        return HostCheck.Result(!shortResult.Succeeded
            && shortResult.Status == VmbusOfferPlanStatus.MalformedLength
            && shortResult.ExpectedLength == VmbusOfferPlan.OfferMessageLength
            && shortResult.ActualLength == VmbusOfferPlan.OfferMessageLength - 1
            && !wrongTypeResult.Succeeded
            && wrongTypeResult.Status == VmbusOfferPlanStatus.UnsupportedMessageType
            && !zeroInterfaceResult.Succeeded
            && zeroInterfaceResult.Status == VmbusOfferPlanStatus.ZeroInterfaceTypeGuid
            && !zeroInstanceResult.Succeeded
            && zeroInstanceResult.Status == VmbusOfferPlanStatus.ZeroInstanceGuid
            && !zeroChannelResult.Succeeded
            && zeroChannelResult.Status == VmbusOfferPlanStatus.ZeroChannelId);
    }

    [HostTest("drivers.vmbus.offer rejects unsupported class", HostLane.Fast, 3)]
    private static bool TestRejectsUnsupportedClass()
    {
        byte[] unknownOffer = CreateOffer(
            CreateUnknownGuid(),
            CreateInstanceGuid(0x55),
            childRelationId: 12,
            monitorId: 1,
            subChannelIndex: 0,
            connectionId: 2);

        VmbusOfferParseResult result = VmbusOfferPlan.ParseOffer(unknownOffer);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VmbusOfferPlanStatus.UnsupportedInterfaceTypeGuid
            && VmbusOfferPlan.ClassifyInterfaceType(CreateUnknownGuid()) == VmbusSyntheticDeviceClass.Unknown
            && !VmbusOfferPlan.IsSupportedInterfaceType(CreateUnknownGuid()));
    }

    [HostTest("drivers.vmbus.offer rejects duplicate channel ids", HostLane.Fast, 4)]
    private static bool TestRejectsDuplicateChannelIds()
    {
        byte[] firstOffer = CreateOffer(
            VmbusOfferPlan.NetVscInterfaceTypeGuid,
            CreateInstanceGuid(0x66),
            childRelationId: 90,
            monitorId: 1,
            subChannelIndex: 0,
            connectionId: 2);
        byte[] duplicateOffer = CreateOffer(
            VmbusOfferPlan.StorVscInterfaceTypeGuid,
            CreateInstanceGuid(0x77),
            childRelationId: 90,
            monitorId: 2,
            subChannelIndex: 0,
            connectionId: 3);
        byte[] catalog = new byte[VmbusOfferPlan.OfferMessageLength * 2];
        firstOffer.CopyTo(catalog.AsSpan(0, VmbusOfferPlan.OfferMessageLength));
        duplicateOffer.CopyTo(catalog.AsSpan(VmbusOfferPlan.OfferMessageLength));
        VmbusOfferDescriptor[] descriptors = new VmbusOfferDescriptor[2];
        VmbusOfferDescriptor[] shortDestination = new VmbusOfferDescriptor[1];

        VmbusOfferCatalogParseResult duplicate =
            VmbusOfferPlan.ParseCatalog(catalog, descriptors);
        VmbusOfferCatalogParseResult destinationTooSmall =
            VmbusOfferPlan.ParseCatalog(catalog, shortDestination);
        VmbusOfferCatalogParseResult malformedCatalog =
            VmbusOfferPlan.ParseCatalog(catalog.AsSpan(0, catalog.Length - 1), descriptors);

        return HostCheck.Result(!duplicate.Succeeded
            && duplicate.Status == VmbusOfferPlanStatus.DuplicateChannelId
            && duplicate.OfferCount == 2
            && duplicate.ParsedOfferCount == 1
            && duplicate.FailingOfferIndex == 1
            && duplicate.FailingOfferStatus == VmbusOfferPlanStatus.DuplicateChannelId
            && duplicate.DuplicateChannelId == 90
            && descriptors[0].ChildRelationId == 90
            && !destinationTooSmall.Succeeded
            && destinationTooSmall.Status == VmbusOfferPlanStatus.DestinationTooSmall
            && destinationTooSmall.RequiredOfferCapacity == 2
            && !malformedCatalog.Succeeded
            && malformedCatalog.Status == VmbusOfferPlanStatus.MalformedLength);
    }

    [HostTest("drivers.vmbus.offer rejects metadata bounds", HostLane.Fast, 5)]
    private static bool TestRejectsMetadataBounds()
    {
        byte[] longMetadata = new byte[VmbusOfferPlan.MaximumFixtureMetadataLength + 1];
        Array.Fill(longMetadata, (byte)'A');
        byte[] longMetadataOffer = CreateOffer(
            VmbusOfferPlan.NetVscInterfaceTypeGuid,
            CreateInstanceGuid(0x88),
            childRelationId: 91,
            monitorId: 1,
            subChannelIndex: 0,
            connectionId: 2,
            metadata: longMetadata);
        byte[] invalidMetadataOffer = CreateOffer(
            VmbusOfferPlan.NetVscInterfaceTypeGuid,
            CreateInstanceGuid(0x99),
            childRelationId: 92,
            monitorId: 1,
            subChannelIndex: 0,
            connectionId: 2,
            metadata: [(byte)'o', 0x01]);

        VmbusOfferParseResult tooLong = VmbusOfferPlan.ParseOffer(longMetadataOffer);
        VmbusOfferParseResult invalidAscii = VmbusOfferPlan.ParseOffer(invalidMetadataOffer);

        return HostCheck.Result(!tooLong.Succeeded
            && tooLong.Status == VmbusOfferPlanStatus.MetadataTooLong
            && !invalidAscii.Succeeded
            && invalidAscii.Status == VmbusOfferPlanStatus.MetadataContainsInvalidAscii);
    }

    [HostTest("drivers.vmbus.offer default results are not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        VmbusOfferParseResult parseResult = default;
        VmbusOfferCatalogParseResult catalogResult = default;

        return HostCheck.Result(!parseResult.IsInitialized
            && !parseResult.Succeeded
            && parseResult.Status == VmbusOfferPlanStatus.Success
            && !catalogResult.IsInitialized
            && !catalogResult.Succeeded
            && catalogResult.Status == VmbusOfferPlanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VmbusOfferParseResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VmbusOfferCatalogParseResult>());
    }

    private static byte[] CreateOffer(
        VmbusGuid interfaceTypeGuid,
        VmbusGuid instanceGuid,
        uint childRelationId,
        byte monitorId,
        ushort subChannelIndex,
        uint connectionId,
        byte[]? metadata = null)
    {
        byte[] offer = new byte[VmbusOfferPlan.OfferMessageLength];
        EndianPrimitives.WriteUInt32LittleEndian(
            offer.AsSpan(0, 4),
            VmbusOfferPlan.OfferChannelMessageType);
        WriteGuid(offer.AsSpan(4, 16), interfaceTypeGuid);
        WriteGuid(offer.AsSpan(20, 16), instanceGuid);
        EndianPrimitives.WriteUInt16LittleEndian(offer.AsSpan(176, 2), subChannelIndex);
        EndianPrimitives.WriteUInt32LittleEndian(offer.AsSpan(180, 4), childRelationId);
        offer[184] = monitorId;
        offer[185] = VmbusOfferPlan.HasMonitorNotificationFacilityFlag;
        EndianPrimitives.WriteUInt32LittleEndian(offer.AsSpan(188, 4), connectionId);

        if (metadata is not null)
        {
            metadata.CopyTo(offer.AsSpan(56, metadata.Length));
        }

        return offer;
    }

    private static void WriteGuid(Span<byte> destination, VmbusGuid value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination[..4], value.Data1);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), value.Data2);
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), value.Data3);
        destination[8] = value.Data4A;
        destination[9] = value.Data4B;
        destination[10] = value.Data4C;
        destination[11] = value.Data4D;
        destination[12] = value.Data4E;
        destination[13] = value.Data4F;
        destination[14] = value.Data4G;
        destination[15] = value.Data4H;
    }

    private static VmbusGuid CreateInstanceGuid(byte lastByte)
    {
        return new VmbusGuid(
            0x10203040,
            0x5060,
            0x7080,
            0x90,
            0xA0,
            0xB0,
            0xC0,
            0xD0,
            0xE0,
            0xF0,
            lastByte);
    }

    private static VmbusGuid CreateUnknownGuid()
    {
        return new VmbusGuid(
            0x11111111,
            0x2222,
            0x3333,
            0x44,
            0x55,
            0x66,
            0x77,
            0x88,
            0x99,
            0xAA,
            0xBB);
    }
}
