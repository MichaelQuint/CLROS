namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class Capabilities
    {
        public const uint Offset = 0x0008;
        public const uint Width = sizeof(uint);
        public const byte ContiguousQueueRequiredBit = 0;
        public const uint ContiguousQueueRequired = 1u << ContiguousQueueRequiredBit;
        public const byte ResetTimeoutShift = 1;
        public const uint ResetTimeoutMask = 0x0000003E;
        public const byte DmaAddressWidthShift = 8;
        public const uint DmaAddressWidthMask = 0x0000FF00;
        public const byte AdminCommandTimeoutShift = 16;
        public const uint AdminCommandTimeoutMask = 0x000F0000;
        public static bool RequiresContiguousQueueMemory(uint capabilities)
        {
            return (capabilities & ContiguousQueueRequired) != 0;
        }

        public static uint GetResetTimeoutEncoded(uint capabilities)
        {
            return (capabilities & ResetTimeoutMask) >> ResetTimeoutShift;
        }

        public static uint GetDmaAddressWidthBits(uint capabilities)
        {
            return (capabilities & DmaAddressWidthMask) >> DmaAddressWidthShift;
        }

        public static uint GetAdminCommandTimeoutEncoded(uint capabilities)
        {
            return (capabilities & AdminCommandTimeoutMask) >> AdminCommandTimeoutShift;
        }
    }
}
