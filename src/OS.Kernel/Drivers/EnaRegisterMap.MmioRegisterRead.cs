namespace OS.Drivers;
public static partial class EnaRegisterMap
{
    public static class MmioRegisterRead
    {
        public const uint Offset = 0x005C;
        public const uint Width = sizeof(uint);
        public const uint RequestIdMask = 0x0000FFFF;
        public const byte RegisterOffsetShift = 16;
        public const uint RegisterOffsetMask = 0xFFFF0000;
    }
}
