namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class MmioResponse
    {
        public const uint LowOffset = 0x0060;
        public const uint HighOffset = 0x0064;
        public const uint Offset = LowOffset;
        public const uint Width = sizeof(ulong);
        public const uint RegisterWidth = sizeof(uint);
    }
}
