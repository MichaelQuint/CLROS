namespace OS.Drivers;

public static partial class NvmeRegisterMap
{
    public static class ControllerStatus
    {
        public const uint Offset = 0x001C;
        public const uint Width = sizeof(uint);
        public const byte ReadyBit = 0;
        public const byte FatalStatusBit = 1;
        public const uint Ready = 1u << ReadyBit;
        public const uint ControllerFatalStatus = 1u << FatalStatusBit;
    }
}
