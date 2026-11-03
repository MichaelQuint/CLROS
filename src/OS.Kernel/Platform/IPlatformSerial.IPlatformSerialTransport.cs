namespace OS.Kernel.Platform;

public interface IPlatformSerialTransport
{
    PlatformSerialTransportKind Kind
    {
        get;
    }

    PlatformSerialTransportResult Write(ReadOnlySpan<byte> bytes);
}
