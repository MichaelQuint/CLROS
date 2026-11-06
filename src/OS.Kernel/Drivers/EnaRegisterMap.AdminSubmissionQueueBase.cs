namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class AdminSubmissionQueueBase
    {
        public const uint LowOffset = 0x0010;
        public const uint HighOffset = 0x0014;
        public const uint Offset = LowOffset;
        public const uint Width = sizeof(ulong);
        public const uint RegisterWidth = sizeof(uint);
    }
}
