namespace OS.Kernel.Diagnostics;

public static class Probes
{
    public const int ProbeCatalogCount = 13;

    public const string BootSmokeGroup = "boot.smoke";

    public const string SerialSmokeGroup = "serial.smoke";

    public const string EfiSmokeGroup = "efi.smoke";

    public const string PanicSmokeGroup = "panic.smoke";

    public const string MemoryMapSmokeGroup = "memory-map.smoke";

    public const string UefiExitBootServicesGroup = "uefi.exitbootservices";

    public const string AcpiDiscoveryGroup = "acpi.discovery";

    public const string StorageSmokeGroup = "storage.smoke";

    public const string NetworkSmokeGroup = "network.smoke";

    public const bool EmitBootSmokeMarkers = true;

    public const bool EmitSerialSmokeMarkers = true;

    public const bool EnableEfiSmokeProbe = true;

    public const bool EnableMemoryMapSmokeProbe = true;

    public const bool EnableUefiGoneSimulationProbe = false;

    public const bool EnableExitBootServicesFreshMapProbe = false;

    public const bool EnableAcpiPhysicalDiscoveryProbe = false;

    public const bool EnableNullPointerPanicProbe = false;

    public const bool EnableInvalidOpcodePanicProbe = false;

    public const bool EnablePacketDumpProbe = false;

    public const bool EnableVirtioBlockReadProbe = false;

    public const bool EnableVirtioNetworkProbe = false;

    public const bool EnableFetchAppHttpTcpExchangeProbe = false;

    public static ProbeDefinition GetBootSmokeProbe()
    {
        return new ProbeDefinition(
            BootSmokeGroup,
            nameof(EmitBootSmokeMarkers),
            EmitBootSmokeMarkers,
            ProbeRisk.Safe,
            "Emits OS_BOOT_OK and OS_SMOKE_PASS markers.");
    }

    public static ProbeDefinition GetSerialSmokeProbe()
    {
        return new ProbeDefinition(
            SerialSmokeGroup,
            nameof(EmitSerialSmokeMarkers),
            EmitSerialSmokeMarkers,
            ProbeRisk.Safe,
            "Confirms the early serial marker path is enabled.");
    }

    public static ProbeDefinition GetEfiSmokeProbe()
    {
        return new ProbeDefinition(
            EfiSmokeGroup,
            nameof(EnableEfiSmokeProbe),
            EnableEfiSmokeProbe,
            ProbeRisk.Safe,
            "Confirms the EFI marker path is enabled.");
    }

    public static ProbeDefinition GetMemoryMapSmokeProbe()
    {
        return new ProbeDefinition(
            MemoryMapSmokeGroup,
            nameof(EnableMemoryMapSmokeProbe),
            EnableMemoryMapSmokeProbe,
            ProbeRisk.Safe,
            "Confirms the UEFI memory-map smoke marker path is enabled.");
    }

    public static ProbeDefinition GetUefiGoneSimulationProbe()
    {
        return new ProbeDefinition(
            EfiSmokeGroup,
            nameof(EnableUefiGoneSimulationProbe),
            EnableUefiGoneSimulationProbe,
            ProbeRisk.BoundarySimulation,
            "Simulates firmware services being unavailable without calling ExitBootServices.");
    }

    public static ProbeDefinition GetExitBootServicesFreshMapProbe()
    {
        return new ProbeDefinition(
            UefiExitBootServicesGroup,
            nameof(EnableExitBootServicesFreshMapProbe),
            EnableExitBootServicesFreshMapProbe,
            ProbeRisk.BoundarySimulation,
            "Requires a fresh UEFI memory map before a future ExitBootServices attempt without calling firmware.");
    }

    public static ProbeDefinition GetAcpiPhysicalDiscoveryProbe()
    {
        return new ProbeDefinition(
            AcpiDiscoveryGroup,
            nameof(EnableAcpiPhysicalDiscoveryProbe),
            EnableAcpiPhysicalDiscoveryProbe,
            ProbeRisk.DeviceIo,
            "Dereferences firmware-provided ACPI physical addresses before ExitBootServices to validate RSDP, root SDT, and MCFG table bytes.");
    }

    public static ProbeDefinition GetNullPointerPanicProbe()
    {
        return new ProbeDefinition(
            PanicSmokeGroup,
            nameof(EnableNullPointerPanicProbe),
            EnableNullPointerPanicProbe,
            ProbeRisk.Destructive,
            "Intentionally triggers a null-pointer panic path.");
    }

    public static ProbeDefinition GetInvalidOpcodePanicProbe()
    {
        return new ProbeDefinition(
            PanicSmokeGroup,
            nameof(EnableInvalidOpcodePanicProbe),
            EnableInvalidOpcodePanicProbe,
            ProbeRisk.Destructive,
            "Intentionally triggers an invalid-opcode panic path.");
    }

    public static ProbeDefinition GetVirtioBlockReadProbe()
    {
        return new ProbeDefinition(
            StorageSmokeGroup,
            nameof(EnableVirtioBlockReadProbe),
            EnableVirtioBlockReadProbe,
            ProbeRisk.DeviceIo,
            "Exercises future virtio-blk read smoke behavior.");
    }

    public static ProbeDefinition GetPacketDumpProbe()
    {
        return new ProbeDefinition(
            NetworkSmokeGroup,
            nameof(EnablePacketDumpProbe),
            EnablePacketDumpProbe,
            ProbeRisk.DeviceIo,
            "Emits future packet dump diagnostics.");
    }

    public static ProbeDefinition GetVirtioNetworkProbe()
    {
        return new ProbeDefinition(
            NetworkSmokeGroup,
            nameof(EnableVirtioNetworkProbe),
            EnableVirtioNetworkProbe,
            ProbeRisk.DeviceIo,
            "Exercises future virtio-net smoke behavior.");
    }

    public static ProbeDefinition GetFetchAppHttpTcpExchangeProbe()
    {
        return new ProbeDefinition(
            NetworkSmokeGroup,
            nameof(EnableFetchAppHttpTcpExchangeProbe),
            EnableFetchAppHttpTcpExchangeProbe,
            ProbeRisk.DeviceIo,
            "Exercises future FetchApp HTTP-over-TCP exchange readiness marker behavior.");
    }

    public static bool TryGetProbe(int index, out ProbeDefinition definition)
    {
        switch (index)
        {
            case 0:
                definition = GetBootSmokeProbe();
                return true;
            case 1:
                definition = GetSerialSmokeProbe();
                return true;
            case 2:
                definition = GetEfiSmokeProbe();
                return true;
            case 3:
                definition = GetMemoryMapSmokeProbe();
                return true;
            case 4:
                definition = GetUefiGoneSimulationProbe();
                return true;
            case 5:
                definition = GetExitBootServicesFreshMapProbe();
                return true;
            case 6:
                definition = GetAcpiPhysicalDiscoveryProbe();
                return true;
            case 7:
                definition = GetNullPointerPanicProbe();
                return true;
            case 8:
                definition = GetInvalidOpcodePanicProbe();
                return true;
            case 9:
                definition = GetVirtioBlockReadProbe();
                return true;
            case 10:
                definition = GetPacketDumpProbe();
                return true;
            case 11:
                definition = GetVirtioNetworkProbe();
                return true;
            case 12:
                definition = GetFetchAppHttpTcpExchangeProbe();
                return true;
            default:
                definition = default;
                return false;
        }
    }

    public static ProbeCatalogCopyResult CopyProbeCatalog(Span<ProbeDefinition> destination)
    {
        if (destination.Length < ProbeCatalogCount)
        {
            return ProbeCatalogCopyResult.Fail(
                ProbeCatalogStatus.DestinationTooSmall,
                ProbeCatalogCount,
                destination.Length,
                writtenCount: 0);
        }

        for (int i = 0; i < ProbeCatalogCount; i++)
        {
            if (!TryGetProbe(i, out ProbeDefinition definition))
            {
                return ProbeCatalogCopyResult.Fail(
                    ProbeCatalogStatus.IndexOutOfRange,
                    ProbeCatalogCount,
                    destination.Length,
                    i);
            }

            destination[i] = definition;
        }

        return ProbeCatalogCopyResult.Success(
            ProbeCatalogCount,
            destination.Length,
            ProbeCatalogCount);
    }
}

public enum ProbeRisk
{
    Safe = 0,
    BoundarySimulation = 1,
    DeviceIo = 2,
    Destructive = 3
}

public enum ProbeCatalogStatus
{
    Success = 0,
    DestinationTooSmall = 1,
    IndexOutOfRange = 2
}

public readonly struct ProbeCatalogCopyResult
{
    private readonly bool _isInitialized;

    private ProbeCatalogCopyResult(
        ProbeCatalogStatus status,
        int requiredCount,
        int availableCount,
        int writtenCount)
    {
        _isInitialized = true;
        Status = status;
        RequiredCount = requiredCount;
        AvailableCount = availableCount;
        WrittenCount = writtenCount;
    }

    public ProbeCatalogStatus Status { get; }

    public int RequiredCount { get; }

    public int AvailableCount { get; }

    public int WrittenCount { get; }

    public bool Succeeded =>
        _isInitialized &&
        Status == ProbeCatalogStatus.Success &&
        RequiredCount == Probes.ProbeCatalogCount &&
        WrittenCount == RequiredCount;

    public static ProbeCatalogCopyResult Success(
        int requiredCount,
        int availableCount,
        int writtenCount)
    {
        return new ProbeCatalogCopyResult(
            ProbeCatalogStatus.Success,
            requiredCount,
            availableCount,
            writtenCount);
    }

    public static ProbeCatalogCopyResult Fail(
        ProbeCatalogStatus status,
        int requiredCount,
        int availableCount,
        int writtenCount)
    {
        return new ProbeCatalogCopyResult(status, requiredCount, availableCount, writtenCount);
    }
}

public readonly struct ProbeDefinition(
    string group,
    string name,
    bool enabledByDefault,
    ProbeRisk risk,
    string description)
{
    public string Group { get; } = group;

    public string Name { get; } = name;

    public bool EnabledByDefault { get; } = enabledByDefault;

    public ProbeRisk Risk { get; } = risk;

    public string Description { get; } = description;
}
