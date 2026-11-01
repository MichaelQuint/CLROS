namespace OS.Drivers;
public static partial class VirtioPciModernRegisterMap
{
    public static class IsrStatus
    {
        public const uint Offset = 0x00;
        public const uint Width = sizeof(byte);
        public const byte QueueInterruptBit = 0;
        public const byte DeviceConfigurationChangeBit = 1;
        public const byte QueueInterrupt = 1 << QueueInterruptBit;
        public const byte DeviceConfigurationChange = 1 << DeviceConfigurationChangeBit;
    }
}
