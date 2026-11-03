using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct VmbusOfferDescriptor
{
    public VmbusOfferDescriptor(uint messageType, VmbusGuid interfaceTypeGuid, VmbusGuid instanceGuid, VmbusSyntheticDeviceClass deviceClass, ushort subChannelIndex, uint childRelationId, byte monitorId, bool hasMonitorNotificationFacility, uint connectionId, int metadataLength)
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

    public uint MessageType
    {
        get;
    }
    public VmbusGuid InterfaceTypeGuid
    {
        get;
    }
    public VmbusGuid InstanceGuid
    {
        get;
    }
    public VmbusSyntheticDeviceClass DeviceClass
    {
        get;
    }
    public ushort SubChannelIndex
    {
        get;
    }
    public uint ChildRelationId
    {
        get;
    }
    public byte MonitorId
    {
        get;
    }
    public bool HasMonitorNotificationFacility
    {
        get;
    }
    public uint ConnectionId
    {
        get;
    }
    public int MetadataLength
    {
        get;
    }
}
