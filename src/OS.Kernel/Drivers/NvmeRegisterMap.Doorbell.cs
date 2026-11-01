namespace OS.Drivers;
public static partial class NvmeRegisterMap
{
    public static class Doorbell
    {
        public const uint RegisterWidth = sizeof(uint);
        public const uint MinimumStrideBytes = sizeof(uint);
        public const uint AdminSubmissionQueueTailOffset = DoorbellBaseOffset;
    }
}
