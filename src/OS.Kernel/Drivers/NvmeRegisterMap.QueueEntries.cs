namespace OS.Drivers;

public static partial class NvmeRegisterMap
{
    public static class QueueEntries
    {
        public const ushort AdminQueueId = 0;
        public const uint SubmissionEntryBytes = 64;
        public const uint CompletionEntryBytes = 16;
        public const byte SubmissionEntrySizeExponent = 6;
        public const byte CompletionEntrySizeExponent = 4;
    }
}
