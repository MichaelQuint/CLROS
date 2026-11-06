namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class CapabilitiesExtension
    {
        public const uint Offset = 0x000C;
        public const uint Width = sizeof(uint);
    }
}
