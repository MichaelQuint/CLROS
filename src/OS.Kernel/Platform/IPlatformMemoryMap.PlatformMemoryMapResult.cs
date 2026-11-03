using OS.Boot;

namespace OS.Kernel.Platform;

public readonly struct PlatformMemoryMapResult
{
    private readonly string? _operation;
    private readonly string? _message;
    private PlatformMemoryMapResult(bool succeeded, PlatformMemoryMapStatus status, UefiMemoryMapSnapshot snapshot, FirmwareServicesMode mode, string operation, string message)
    {
        Succeeded = succeeded;
        Status = status;
        Snapshot = snapshot;
        Mode = mode;
        _operation = NormalizeOperation(operation);
        _message = NormalizeMessage(message);
    }

    public bool Succeeded
    {
        get;
    }
    public PlatformMemoryMapStatus Status
    {
        get;
    }
    public UefiMemoryMapSnapshot Snapshot
    {
        get;
    }
    public FirmwareServicesMode Mode
    {
        get;
    }
    public string Operation => _operation ?? PlatformMemoryMapOperations.GetMemoryMap;
    public string Message => _message ?? string.Empty;

    public static PlatformMemoryMapResult Copied(FirmwareCallResult guardResult, UefiMemoryMapSnapshot snapshot)
    {
        if (!guardResult.Succeeded)
        {
            return FirmwareServicesUnavailable(guardResult);
        }

        if (!snapshot.HasDescriptors)
        {
            return InvalidSnapshot(guardResult);
        }

        return new PlatformMemoryMapResult(true, PlatformMemoryMapStatus.Success, snapshot, guardResult.Mode, guardResult.Operation, string.Empty);
    }

    public static PlatformMemoryMapResult FirmwareServicesUnavailable(FirmwareCallResult guardResult)
    {
        return new PlatformMemoryMapResult(false, PlatformMemoryMapStatus.FirmwareServicesUnavailable, default, guardResult.Mode, guardResult.Operation, guardResult.Reason);
    }

    public static PlatformMemoryMapResult InvalidSnapshot(FirmwareCallResult guardResult)
    {
        return new PlatformMemoryMapResult(false, PlatformMemoryMapStatus.InvalidSnapshot, default, guardResult.Mode, guardResult.Operation, "UEFI memory-map snapshot did not contain descriptor entries.");
    }

    private static string NormalizeOperation(string operation)
    {
        return string.IsNullOrWhiteSpace(operation) ? PlatformMemoryMapOperations.GetMemoryMap : operation;
    }

    private static string NormalizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? string.Empty : message;
    }
}
