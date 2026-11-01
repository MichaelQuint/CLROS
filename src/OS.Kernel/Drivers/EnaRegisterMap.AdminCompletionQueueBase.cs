namespace OS.Drivers;
public static partial class EnaRegisterMap
{
    public static class AdminCompletionQueueBase
    {
        public const uint LowOffset = 0x0020;
        public const uint HighOffset = 0x0024;
        public const uint Offset = LowOffset;
        public const uint Width = sizeof(ulong);
        public const uint RegisterWidth = sizeof(uint);
    }
}
