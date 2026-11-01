namespace OS.Drivers;
public static partial class EnaRegisterMap
{
    public static class AdminCompletionQueueCapabilities
    {
        public const uint Offset = 0x0028;
        public const uint Width = sizeof(uint);
        public const uint DepthMask = 0x0000FFFF;
        public const byte EntrySizeShift = 16;
        public const uint EntrySizeMask = 0xFFFF0000;
        public static uint GetDepth(uint capabilities)
        {
            return capabilities & DepthMask;
        }

        public static uint GetEntrySizeBytes(uint capabilities)
        {
            return (capabilities & EntrySizeMask) >> EntrySizeShift;
        }
    }
}
