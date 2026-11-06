namespace OS.Drivers;

using OS.Std.Binary;

public readonly struct EnaAdminGetFeatureCommand
{
    public EnaAdminGetFeatureCommand(EnaAdminAqCommonDescriptor commonDescriptor, EnaAdminControlBuffer controlBuffer, byte featureFlags, byte featureId, byte featureVersion)
    {
        CommonDescriptor = commonDescriptor;
        ControlBuffer = controlBuffer;
        FeatureFlags = featureFlags;
        FeatureId = featureId;
        FeatureVersion = featureVersion;
    }

    public EnaAdminAqCommonDescriptor CommonDescriptor
    {
        get;
    }
    public EnaAdminControlBuffer ControlBuffer
    {
        get;
    }
    public byte FeatureFlags
    {
        get;
    }
    public byte FeatureSelect => (byte)(FeatureFlags & EnaAdminDescriptors.FeatureSelectMask);
    public byte FeatureId
    {
        get;
    }
    public byte FeatureVersion
    {
        get;
    }

    public static EnaAdminGetFeatureCommand Create(ushort commandId, bool phase, EnaAdminFeatureId featureId, byte featureVersion, EnaAdminFeatureSelect featureSelect, EnaAdminControlBuffer controlBuffer, bool controlBufferAddressValid, bool controlBufferIndirect)
    {
        byte flags = phase ? EnaAdminDescriptors.PhaseFlag : (byte)0;
        if (controlBufferAddressValid)
        {
            flags |= EnaAdminDescriptors.ControlDataFlag;
        }

        if (controlBufferIndirect)
        {
            flags |= EnaAdminDescriptors.ControlDataIndirectFlag;
        }

        return new EnaAdminGetFeatureCommand(new EnaAdminAqCommonDescriptor(commandId, (byte)EnaAdminOpcode.GetFeature, flags), controlBuffer, (byte)featureSelect, (byte)featureId, featureVersion);
    }

    public static EnaAdminGetFeatureCommand CreateDeviceAttributes(ushort commandId, bool phase)
    {
        return Create(commandId, phase, EnaAdminFeatureId.DeviceAttributes, featureVersion: 0, EnaAdminFeatureSelect.CurrentValue, default, controlBufferAddressValid: false, controlBufferIndirect: false);
    }
}
