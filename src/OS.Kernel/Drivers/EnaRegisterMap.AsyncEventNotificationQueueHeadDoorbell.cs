namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class AsyncEventNotificationQueueHeadDoorbell
    {
        public const uint Offset = 0x0040;
        public const uint Width = sizeof(uint);
    }
}
