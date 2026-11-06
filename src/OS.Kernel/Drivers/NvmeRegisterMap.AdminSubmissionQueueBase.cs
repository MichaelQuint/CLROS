namespace OS.Drivers;

public static partial class NvmeRegisterMap
{
    public static class AdminSubmissionQueueBase
    {
        public const uint Offset = 0x0028;
        public const uint Width = sizeof(ulong);
    }
}
