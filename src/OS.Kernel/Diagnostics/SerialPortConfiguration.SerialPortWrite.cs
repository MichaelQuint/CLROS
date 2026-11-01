namespace OS.Kernel.Diagnostics;
public readonly struct SerialPortWrite(ushort port, byte value)
{
    public ushort Port { get; } = port;
    public byte Value { get; } = value;
}
