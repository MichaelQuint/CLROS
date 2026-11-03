namespace OS.Drivers;

public static partial class NvmeRegisterMap
{
    public static class InterruptMaskClear
    {
        public const uint Offset = 0x0010;
        public const uint Width = sizeof(uint);
    }
}
