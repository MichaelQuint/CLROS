namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class PhcDoorbell
    {
        public const uint Offset = 0x0100;
        public const uint Width = sizeof(uint);
        public const uint RequestIdMask = 0x0000FFFF;
    }
}
