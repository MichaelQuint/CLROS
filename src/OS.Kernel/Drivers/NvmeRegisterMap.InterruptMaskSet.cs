namespace OS.Drivers;

public static partial class NvmeRegisterMap
{
    public static class InterruptMaskSet
    {
        public const uint Offset = 0x000C;
        public const uint Width = sizeof(uint);
    }
}
