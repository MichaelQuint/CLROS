namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class RssIndirectionEntryUpdate
    {
        public const uint Offset = 0x0068;
        public const uint Width = sizeof(uint);
        public const uint IndexMask = 0x0000FFFF;
        public const byte CompletionQueueIndexShift = 16;
        public const uint CompletionQueueIndexMask = 0xFFFF0000;
    }
}
