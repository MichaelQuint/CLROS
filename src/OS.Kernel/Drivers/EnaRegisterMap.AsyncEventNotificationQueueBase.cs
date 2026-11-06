namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class AsyncEventNotificationQueueBase
    {
        public const uint LowOffset = 0x0038;
        public const uint HighOffset = 0x003C;
        public const uint Offset = LowOffset;
        public const uint Width = sizeof(ulong);
        public const uint RegisterWidth = sizeof(uint);
    }
}
