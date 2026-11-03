namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class AdminSubmissionQueueDoorbell
    {
        public const uint Offset = 0x002C;
        public const uint Width = sizeof(uint);
    }
}
