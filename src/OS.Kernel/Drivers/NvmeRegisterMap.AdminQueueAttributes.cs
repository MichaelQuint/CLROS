namespace OS.Drivers;
public static partial class NvmeRegisterMap
{
    public static class AdminQueueAttributes
    {
        public const uint Offset = 0x0024;
        public const uint Width = sizeof(uint);
        public const byte SubmissionQueueSizeShift = 0;
        public const byte CompletionQueueSizeShift = 16;
        public const uint QueueSizeMask = 0x0FFF;
        public const uint SubmissionQueueSizeMask = QueueSizeMask << SubmissionQueueSizeShift;
        public const uint CompletionQueueSizeMask = QueueSizeMask << CompletionQueueSizeShift;
    }
}
