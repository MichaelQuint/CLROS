namespace OS.Drivers;

public static partial class EnaRegisterMap
{
    public static class DeviceControl
    {
        public const uint Offset = 0x0054;
        public const uint Width = sizeof(uint);
        public const byte DeviceResetBit = 0;
        public const uint DeviceReset = 1u << DeviceResetBit;
        public const byte AdminQueueRestartShift = 1;
        public const uint AdminQueueRestart = 0x00000002;
        public const byte QuiescentShift = 2;
        public const uint Quiescent = 0x00000004;
        public const byte IoResumeShift = 3;
        public const uint IoResume = 0x00000008;
        public const byte ResetReasonShift = 28;
        public const uint ResetReasonMask = 0xF0000000;
    }
}
