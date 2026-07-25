namespace OS.Drivers;

public static class VirtioPciModernRegisterMap
{
    public static class CommonConfiguration
    {
        public const uint DeviceFeatureSelectOffset = 0x00;
        public const uint DeviceFeatureSelectWidth = sizeof(uint);
        public const uint DeviceFeatureOffset = 0x04;
        public const uint DeviceFeatureWidth = sizeof(uint);
        public const uint DriverFeatureSelectOffset = 0x08;
        public const uint DriverFeatureSelectWidth = sizeof(uint);
        public const uint DriverFeatureOffset = 0x0C;
        public const uint DriverFeatureWidth = sizeof(uint);
        public const uint ConfigMsixVectorOffset = 0x10;
        public const uint ConfigMsixVectorWidth = sizeof(ushort);
        public const uint NumQueuesOffset = 0x12;
        public const uint NumQueuesWidth = sizeof(ushort);
        public const uint DeviceStatusOffset = 0x14;
        public const uint DeviceStatusWidth = sizeof(byte);
        public const uint ConfigGenerationOffset = 0x15;
        public const uint ConfigGenerationWidth = sizeof(byte);
        public const uint QueueSelectOffset = 0x16;
        public const uint QueueSelectWidth = sizeof(ushort);
        public const uint QueueSizeOffset = 0x18;
        public const uint QueueSizeWidth = sizeof(ushort);
        public const uint QueueMsixVectorOffset = 0x1A;
        public const uint QueueMsixVectorWidth = sizeof(ushort);
        public const uint QueueEnableOffset = 0x1C;
        public const uint QueueEnableWidth = sizeof(ushort);
        public const uint QueueNotifyOffOffset = 0x1E;
        public const uint QueueNotifyOffWidth = sizeof(ushort);
        public const uint QueueDescOffset = 0x20;
        public const uint QueueDescWidth = sizeof(ulong);
        public const uint QueueDriverOffset = 0x28;
        public const uint QueueDriverWidth = sizeof(ulong);
        public const uint QueueDeviceOffset = 0x30;
        public const uint QueueDeviceWidth = sizeof(ulong);
        public const uint FirstLiveQueueSetupLength = QueueDeviceOffset + QueueDeviceWidth;
    }

    public static class DeviceStatus
    {
        public const byte Acknowledge = 0x01;
        public const byte Driver = 0x02;
        public const byte DriverOk = 0x04;
        public const byte FeaturesOk = 0x08;
        public const byte DeviceNeedsReset = 0x40;
        public const byte Failed = 0x80;
    }

    public static class IsrStatus
    {
        public const uint Offset = 0x00;
        public const uint Width = sizeof(byte);
        public const byte QueueInterruptBit = 0;
        public const byte DeviceConfigurationChangeBit = 1;
        public const byte QueueInterrupt = 1 << QueueInterruptBit;
        public const byte DeviceConfigurationChange = 1 << DeviceConfigurationChangeBit;
    }

    public static class NetDeviceConfiguration
    {
        public const uint MacAddressOffset = 0x00;
        public const uint MacAddressLength = 6;
        public const uint StatusOffset = 0x06;
        public const uint StatusWidth = sizeof(ushort);
        public const uint StatusConfigLength = StatusOffset + StatusWidth;
    }

    public static class Notification
    {
        public const uint NoNotificationDataWriteWidthBits = 16;
        public const uint NoNotificationDataWriteWidthBytes = sizeof(ushort);
    }
}
