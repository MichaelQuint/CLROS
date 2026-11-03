namespace OS.Drivers;

public static partial class NvmeRegisterMap
{
    public static class AdminCompletionQueueBase
    {
        public const uint Offset = 0x0030;
        public const uint Width = sizeof(ulong);
    }
}
