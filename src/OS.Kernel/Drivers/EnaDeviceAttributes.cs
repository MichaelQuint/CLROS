namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public readonly struct EnaDeviceAttributes
{
    public EnaDeviceAttributes(ushort commandId, bool phase, EnaAdminCompletionStatus completionStatus, ushort extendedStatus, ushort submissionQueueHeadIndex, EnaAdminFeatureId featureId, uint implementationId, uint deviceVersion, uint supportedFeatures, uint capabilities, uint physicalAddressWidth, uint virtualAddressWidth, MacAddress macAddress, uint maxMtu)
    {
        CommandId = commandId;
        Phase = phase;
        CompletionStatus = completionStatus;
        ExtendedStatus = extendedStatus;
        SubmissionQueueHeadIndex = submissionQueueHeadIndex;
        FeatureId = featureId;
        ImplementationId = implementationId;
        DeviceVersion = deviceVersion;
        SupportedFeatures = supportedFeatures;
        Capabilities = capabilities;
        PhysicalAddressWidth = physicalAddressWidth;
        VirtualAddressWidth = virtualAddressWidth;
        MacAddress = macAddress;
        MaxMtu = maxMtu;
    }

    public ushort CommandId
    {
        get;
    }
    public bool Phase
    {
        get;
    }
    public EnaAdminCompletionStatus CompletionStatus
    {
        get;
    }
    public ushort ExtendedStatus
    {
        get;
    }
    public ushort SubmissionQueueHeadIndex
    {
        get;
    }
    public EnaAdminFeatureId FeatureId
    {
        get;
    }
    public uint ImplementationId
    {
        get;
    }
    public uint DeviceVersion
    {
        get;
    }
    public uint SupportedFeatures
    {
        get;
    }
    public uint Capabilities
    {
        get;
    }
    public uint PhysicalAddressWidth
    {
        get;
    }
    public uint VirtualAddressWidth
    {
        get;
    }
    public MacAddress MacAddress
    {
        get;
    }
    public uint MaxMtu
    {
        get;
    }
}
