namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class InterruptMask
    {
        public const uint Offset = 0x004C;
        public const uint Width = sizeof(uint);
    }
}
