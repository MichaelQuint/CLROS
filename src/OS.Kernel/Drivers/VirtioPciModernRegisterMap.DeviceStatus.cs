namespace OS.Drivers;
public static partial class VirtioPciModernRegisterMap
{
    public static class DeviceStatus
    {
        public const byte Acknowledge = 0x01;
        public const byte Driver = 0x02;
        public const byte DriverOk = 0x04;
        public const byte FeaturesOk = 0x08;
        public const byte DeviceNeedsReset = 0x40;
        public const byte Failed = 0x80;
    }
}
