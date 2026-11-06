namespace OS.Kernel.Diagnostics;

public interface IBytePortBackend
{
    bool TryReadByte(ushort port, out byte value);
    bool TryWriteByte(ushort port, byte value);
}
