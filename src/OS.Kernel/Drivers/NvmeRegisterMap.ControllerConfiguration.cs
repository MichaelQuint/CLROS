namespace OS.Drivers;

public static partial class NvmeRegisterMap
{
    public static class ControllerConfiguration
    {
        public const uint Offset = 0x0014;
        public const uint Width = sizeof(uint);
        public const byte EnableBit = 0;
        public const uint Enable = 1u << EnableBit;
        public const byte IoSubmissionQueueEntrySizeShift = 16;
        public const byte IoCompletionQueueEntrySizeShift = 20;
        public const uint QueueEntrySizeMask = 0x0F;
        public const uint IoSubmissionQueueEntrySizeMask = QueueEntrySizeMask << IoSubmissionQueueEntrySizeShift;
        public const uint IoCompletionQueueEntrySizeMask = QueueEntrySizeMask << IoCompletionQueueEntrySizeShift;
    }
}
