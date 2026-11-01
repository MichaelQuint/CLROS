namespace OS.Kernel.Diagnostics;
public enum SerialPortConfigurationStatus
{
    Success = 0,
    InvalidBasePort = 1,
    InvalidInputClock = 2,
    InvalidBaudRate = 3,
    DivisorOutOfRange = 4,
    OutputBufferTooSmall = 5
}
