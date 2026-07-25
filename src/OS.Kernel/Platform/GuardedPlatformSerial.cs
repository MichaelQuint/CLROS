namespace OS.Kernel.Platform;

public sealed class GuardedPlatformSerial : IPlatformSerial
{
    private readonly IPlatformFirmware _firmware;
    private readonly IPlatformSerialTransport _transport;

    public GuardedPlatformSerial(IPlatformFirmware firmware, IPlatformSerialTransport transport)
    {
        _firmware = firmware;
        _transport = transport;
    }

    public PlatformSerialWriteResult Write(ReadOnlySpan<byte> bytes)
    {
        FirmwareServicesMode mode = _firmware.Mode;
        PlatformSerialTransportKind transportKind = _transport.Kind;

        if (!PlatformSerialWriteResult.IsValidPayload(bytes))
        {
            return PlatformSerialWriteResult.InvalidPayload(
                mode,
                transportKind,
                bytes.Length);
        }

        if (transportKind == PlatformSerialTransportKind.None)
        {
            return PlatformSerialWriteResult.NoOwnedTransport(
                mode,
                transportKind,
                bytes.Length);
        }

        if (mode != FirmwareServicesMode.BootServicesAvailable
            && transportKind != PlatformSerialTransportKind.Owned)
        {
            return PlatformSerialWriteResult.UefiGoneRequiresOwnedSerial(
                mode,
                transportKind,
                bytes.Length);
        }

        PlatformSerialTransportResult transportResult = _transport.Write(bytes);
        return PlatformSerialWriteResult.FromTransport(
            mode,
            transportKind,
            transportResult,
            bytes.Length);
    }
}
