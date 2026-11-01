namespace OS.Drivers;
public static partial class EnaRegisterMap
{
    public static class AsyncEventNotificationQueueCapabilities
    {
        public const uint Offset = 0x0034;
        public const uint Width = sizeof(uint);
        public const uint DepthMask = 0x0000FFFF;
        public const byte EntrySizeShift = 16;
        public const uint EntrySizeMask = 0xFFFF0000;
    }
}
