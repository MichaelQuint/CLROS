namespace OS.Drivers;

public static partial class VirtioPciModernRegisterMap
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
}
