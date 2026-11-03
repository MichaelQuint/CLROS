namespace OS.Kernel.Diagnostics;

public interface IPanicSerialByteSink
{
    bool TryWriteByte(byte value);
}
