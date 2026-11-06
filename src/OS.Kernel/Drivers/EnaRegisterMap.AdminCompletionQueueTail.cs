namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class AdminCompletionQueueTail
    {
        public const uint Offset = 0x0030;
        public const uint Width = sizeof(uint);
    }
}
