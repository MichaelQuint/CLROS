namespace OS.Drivers;

public static partial class NvmeRegisterMap
{
    public static class ControllerCapabilities
    {
        public const uint Offset = 0x0000;
        public const uint Width = sizeof(ulong);
        public const byte DoorbellStrideShift = 32;
        public const ulong DoorbellStrideMask = 0x0F;
    }
}
