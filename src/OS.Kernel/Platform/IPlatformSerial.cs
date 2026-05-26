namespace OS.Kernel.Platform;

public interface IPlatformSerial
{
    PlatformSerialWriteResult Write(ReadOnlySpan<byte> bytes);
}

public interface IPlatformSerialTransport
{
    PlatformSerialTransportKind Kind { get; }

    PlatformSerialTransportResult Write(ReadOnlySpan<byte> bytes);
}

public static class PlatformSerialOperations
{
    public const string WriteBytes = "Serial.WriteBytes";
}

public enum PlatformSerialTransportKind
{
    None = 0,
    FirmwareBacked = 1,
    Owned = 2
}

public enum PlatformSerialTransportStatus
{
    Unknown = 0,
    Success = 1,
    Unavailable = 2,
    BackendFailure = 3
}

public enum PlatformSerialWriteStatus
{
    Unknown = 0,
    Success = 1,
    InvalidPayload = 2,
    NoOwnedTransport = 3,
    UefiGoneRequiresOwnedSerial = 4,
    OwnedSerialUnavailable = 5,
    BackendFailure = 6,
    ShortWrite = 7
}

public readonly struct PlatformSerialTransportResult
{
    private PlatformSerialTransportResult(
        PlatformSerialTransportStatus status,
        int bytesWritten,
        ulong backendStatus)
    {
        Status = status;
        BytesWritten = bytesWritten;
        BackendStatus = backendStatus;
    }

    public PlatformSerialTransportStatus Status { get; }

    public int BytesWritten { get; }

    public ulong BackendStatus { get; }

    public bool Succeeded => Status == PlatformSerialTransportStatus.Success;

    public static PlatformSerialTransportResult Written(int bytesWritten)
    {
        return Written(bytesWritten, 0);
    }

    public static PlatformSerialTransportResult Written(int bytesWritten, ulong backendStatus)
    {
        if (bytesWritten < 0)
        {
            return BackendFailure(backendStatus);
        }

        return new PlatformSerialTransportResult(
            PlatformSerialTransportStatus.Success,
            bytesWritten,
            backendStatus);
    }

    public static PlatformSerialTransportResult Unavailable()
    {
        return new PlatformSerialTransportResult(
            PlatformSerialTransportStatus.Unavailable,
            0,
            0);
    }

    public static PlatformSerialTransportResult BackendFailure(ulong backendStatus)
    {
        return new PlatformSerialTransportResult(
            PlatformSerialTransportStatus.BackendFailure,
            0,
            backendStatus);
    }
}

public readonly struct PlatformSerialWriteResult
{
    public const int MaximumPayloadBytes = 512;

    private PlatformSerialWriteResult(
        PlatformSerialWriteStatus status,
        FirmwareServicesMode mode,
        PlatformSerialTransportKind transportKind,
        int requestedBytes,
        int bytesWritten,
        int requiredBytes,
        ulong backendStatus)
    {
        Status = status;
        Mode = mode;
        TransportKind = transportKind;
        RequestedBytes = requestedBytes;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        BackendStatus = backendStatus;
    }

    public PlatformSerialWriteStatus Status { get; }

    public FirmwareServicesMode Mode { get; }

    public PlatformSerialTransportKind TransportKind { get; }

    public string Operation => PlatformSerialOperations.WriteBytes;

    public int RequestedBytes { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public ulong BackendStatus { get; }

    public bool Succeeded => Status == PlatformSerialWriteStatus.Success;

    public static bool IsValidPayload(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty || bytes.Length > MaximumPayloadBytes)
        {
            return false;
        }

        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = bytes[index];
            if (value == 0x0a || value == 0x0d)
            {
                continue;
            }

            if (value < 0x20 || value > 0x7e)
            {
                return false;
            }
        }

        return true;
    }

    public static PlatformSerialWriteResult InvalidPayload(
        FirmwareServicesMode mode,
        PlatformSerialTransportKind transportKind,
        int requestedBytes)
    {
        return new PlatformSerialWriteResult(
            PlatformSerialWriteStatus.InvalidPayload,
            mode,
            transportKind,
            requestedBytes,
            0,
            requestedBytes > 0 ? requestedBytes : 0,
            0);
    }

    public static PlatformSerialWriteResult NoOwnedTransport(
        FirmwareServicesMode mode,
        PlatformSerialTransportKind transportKind,
        int requestedBytes)
    {
        return new PlatformSerialWriteResult(
            PlatformSerialWriteStatus.NoOwnedTransport,
            mode,
            transportKind,
            requestedBytes,
            0,
            requestedBytes,
            0);
    }

    public static PlatformSerialWriteResult UefiGoneRequiresOwnedSerial(
        FirmwareServicesMode mode,
        PlatformSerialTransportKind transportKind,
        int requestedBytes)
    {
        return new PlatformSerialWriteResult(
            PlatformSerialWriteStatus.UefiGoneRequiresOwnedSerial,
            mode,
            transportKind,
            requestedBytes,
            0,
            requestedBytes,
            0);
    }

    public static PlatformSerialWriteResult FromTransport(
        FirmwareServicesMode mode,
        PlatformSerialTransportKind transportKind,
        PlatformSerialTransportResult transportResult,
        int requestedBytes)
    {
        if (!transportResult.Succeeded)
        {
            PlatformSerialWriteStatus status =
                transportResult.Status == PlatformSerialTransportStatus.Unavailable
                    ? PlatformSerialWriteStatus.OwnedSerialUnavailable
                    : PlatformSerialWriteStatus.BackendFailure;

            return new PlatformSerialWriteResult(
                status,
                mode,
                transportKind,
                requestedBytes,
                transportResult.BytesWritten,
                requestedBytes,
                transportResult.BackendStatus);
        }

        if (transportResult.BytesWritten != requestedBytes)
        {
            return new PlatformSerialWriteResult(
                PlatformSerialWriteStatus.ShortWrite,
                mode,
                transportKind,
                requestedBytes,
                transportResult.BytesWritten,
                requestedBytes,
                transportResult.BackendStatus);
        }

        return new PlatformSerialWriteResult(
            PlatformSerialWriteStatus.Success,
            mode,
            transportKind,
            requestedBytes,
            transportResult.BytesWritten,
            requestedBytes,
            transportResult.BackendStatus);
    }
}
