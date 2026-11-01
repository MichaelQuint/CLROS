namespace OS.Drivers;
public static partial class VirtioPciModernRegisterMap
{
    public static class NetDeviceConfiguration
    {
        public const uint MacAddressOffset = 0x00;
        public const uint MacAddressLength = 6;
        public const uint StatusOffset = 0x06;
        public const uint StatusWidth = sizeof(ushort);
        public const uint StatusConfigLength = StatusOffset + StatusWidth;
    }
}
