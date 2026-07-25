namespace OS.Boot;

public readonly struct UefiMemoryMapResult
{
    private readonly string? _message;

    private UefiMemoryMapResult(
        bool succeeded,
        UefiMemoryMapStatus status,
        UefiMemoryMapSnapshot snapshot,
        ulong requiredBufferSize,
        ulong firmwareStatus,
        string message)
    {
        Succeeded = succeeded;
        Status = status;
        Snapshot = snapshot;
        RequiredBufferSize = requiredBufferSize;
        FirmwareStatus = firmwareStatus;
        _message = message;
    }

    public bool Succeeded { get; }

    public UefiMemoryMapStatus Status { get; }

    public UefiMemoryMapSnapshot Snapshot { get; }

    public ulong RequiredBufferSize { get; }

    public ulong FirmwareStatus { get; }

    public string Message => _message ?? string.Empty;

    public static UefiMemoryMapResult Captured(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        UefiMemoryDescriptor[]? descriptorEntries)
    {
        if (descriptorSize == 0)
        {
            return Invalid(
                UefiMemoryMapStatus.InvalidDescriptorSize,
                "UEFI memory map descriptor size is required.");
        }

        if (descriptorVersion == 0)
        {
            return Invalid(
                UefiMemoryMapStatus.InvalidDescriptorVersion,
                "UEFI memory map descriptor version is required.");
        }

        if (descriptorEntries is null || descriptorEntries.Length == 0)
        {
            return Invalid(
                UefiMemoryMapStatus.InvalidDescriptorEntries,
                "UEFI memory map descriptor entries are required.");
        }

        UefiMemoryMapSnapshot snapshot = UefiMemoryMapSnapshot.CreateUnchecked(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorEntries);

        return new UefiMemoryMapResult(
            true,
            UefiMemoryMapStatus.Success,
            snapshot,
            0,
            0,
            string.Empty);
    }

    public static UefiMemoryMapResult BufferTooSmall(ulong requiredBufferSize)
    {
        return new UefiMemoryMapResult(
            false,
            UefiMemoryMapStatus.BufferTooSmall,
            default,
            requiredBufferSize,
            0,
            "UEFI memory map buffer is too small.");
    }

    public static UefiMemoryMapResult FirmwareError(ulong firmwareStatus)
    {
        return new UefiMemoryMapResult(
            false,
            UefiMemoryMapStatus.FirmwareError,
            default,
            0,
            firmwareStatus,
            "UEFI GetMemoryMap failed.");
    }

    private static UefiMemoryMapResult Invalid(
        UefiMemoryMapStatus status,
        string message)
    {
        return new UefiMemoryMapResult(
            false,
            status,
            default,
            0,
            0,
            message);
    }
}
