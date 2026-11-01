namespace OS.Kernel.Diagnostics;
public readonly struct SerialPortDivisorLatch(ushort divisor)
{
    public ushort Divisor { get; } = divisor;
    public byte LowByte { get; } = (byte)divisor;
    public byte HighByte { get; } = (byte)(divisor >> 8);
}
