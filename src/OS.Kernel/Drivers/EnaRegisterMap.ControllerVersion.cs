namespace OS.Drivers;
public static partial class EnaRegisterMap
{
    public static class ControllerVersion
    {
        public const uint Offset = 0x0004;
        public const uint Width = sizeof(uint);
        public const uint SubminorVersionMask = 0x000000FF;
        public const byte MinorVersionShift = 8;
        public const uint MinorVersionMask = 0x0000FF00;
        public const byte MajorVersionShift = 16;
        public const uint MajorVersionMask = 0x00FF0000;
        public const byte ImplementationIdShift = 24;
        public const uint ImplementationIdMask = 0xFF000000;
    }
}
