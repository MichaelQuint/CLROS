namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class Version
    {
        public const uint Offset = 0x0000;
        public const uint Width = sizeof(uint);
        public const uint MinorVersionMask = 0x000000FF;
        public const byte MajorVersionShift = 8;
        public const uint MajorVersionMask = 0x0000FF00;
    }
}
