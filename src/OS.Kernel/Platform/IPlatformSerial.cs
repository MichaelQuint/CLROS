namespace OS.Kernel.Platform;

public interface IPlatformSerial
{
    PlatformSerialWriteResult Write(ReadOnlySpan<byte> bytes);
}
