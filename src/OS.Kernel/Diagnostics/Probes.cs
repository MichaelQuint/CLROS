namespace OS.Kernel.Diagnostics;

public static partial class Probes
{
#if M04_SEEDED_COLLECTOR_CHURN_PROBE
    public const bool EnableM04SeededCollectorChurnProbe = true;
#else
    public const bool EnableM04SeededCollectorChurnProbe = false;
#endif

#if M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE
    public const bool EnableM03FirstRealExitBootServicesProbe = true;
#else
    public const bool EnableM03FirstRealExitBootServicesProbe = false;
#endif

#if M03_FIRMWARE_ADAPTER_REVOCATION_PROBE
    public const bool EnableM03FirmwareAdapterRevocationProbe = true;
#else
    public const bool EnableM03FirmwareAdapterRevocationProbe = false;
#endif

#if M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE
    public const bool EnableM03OwnedSubsystemSurvivalProbe = true;
#else
    public const bool EnableM03OwnedSubsystemSurvivalProbe = false;
#endif

#if M03_OWNED_SUBSYSTEM_PANIC_PROBE
    public const bool EnableM03OwnedSubsystemPanicProbe = true;
#else
    public const bool EnableM03OwnedSubsystemPanicProbe = false;
#endif

#if M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE
    public const bool EnableM04ClassConstructorExecutionProbe = true;
#else
    public const bool EnableM04ClassConstructorExecutionProbe = false;
#endif

#if M04_INTERFACE_DISPATCH_PROBE
    public const bool EnableM04InterfaceDispatchProbe = true;
#else
    public const bool EnableM04InterfaceDispatchProbe = false;
#endif

#if M04_X64_UNWIND_METADATA_PROBE
    public const bool EnableM04X64UnwindMetadataProbe = true;
#else
    public const bool EnableM04X64UnwindMetadataProbe = false;
#endif

#if !NOSTDLIB_EFI
    public const int ProbeCatalogCount = 23;

    public const string BootSmokeGroup = "boot.smoke";

    public const string SerialSmokeGroup = "serial.smoke";

    public const string EfiSmokeGroup = "efi.smoke";

    public const string PanicSmokeGroup = "panic.smoke";

    public const string MemoryMapSmokeGroup = "memory-map.smoke";

    public const string M01LiveMemoryGroup = "m01.live-memory";

    public const string UefiExitBootServicesGroup = "uefi.exitbootservices";

    public const string AcpiDiscoveryGroup = "acpi.discovery";

    public const string StorageSmokeGroup = "storage.smoke";

    public const string NetworkSmokeGroup = "network.smoke";

    public const bool EmitBootSmokeMarkers = true;

    public const bool EmitSerialSmokeMarkers = true;

    public const bool EnableEfiSmokeProbe = true;

    public const bool EnableMemoryMapSmokeProbe = true;

    public const bool EnableM01LoadedImageStackProbe = false;

    public const bool EnableM01LiveAllocatorProbe = false;

    public const bool EnableM01OwnedPagingProbe = false;

    public const bool EnableUefiGoneSimulationProbe = false;

    public const bool EnableExitBootServicesFreshMapProbe = false;

    public const bool EnableAcpiPhysicalDiscoveryProbe = false;

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

    public static ProbeDefinition GetM01LoadedImageStackProbe()
    {
        return new ProbeDefinition(
            M01LiveMemoryGroup,
            nameof(EnableM01LoadedImageStackProbe),
            EnableM01LoadedImageStackProbe,
            ProbeRisk.Destructive,
            "Opts into future M01 stage-one loaded-image and owned-stack integration.");
    }

    public static ProbeDefinition GetM01LiveAllocatorProbe()
    {
        return new ProbeDefinition(
            M01LiveMemoryGroup,
            nameof(EnableM01LiveAllocatorProbe),
            EnableM01LiveAllocatorProbe,
            ProbeRisk.Destructive,
            "Opts into future M01 stage-two live-allocator integration.");
    }

    public static ProbeDefinition GetM01OwnedPagingProbe()
    {
        return new ProbeDefinition(
            M01LiveMemoryGroup,
            nameof(EnableM01OwnedPagingProbe),
            EnableM01OwnedPagingProbe,
            ProbeRisk.Destructive,
            "Opts into future M01 stage-three owned-paging integration.");
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

    public static ProbeDefinition GetM03FirstRealExitBootServicesProbe()
    {
        return new ProbeDefinition(
            UefiExitBootServicesGroup,
            nameof(EnableM03FirstRealExitBootServicesProbe),
            EnableM03FirstRealExitBootServicesProbe,
            ProbeRisk.Destructive,
            "Exits firmware boot services and adopts owned descriptors, CR3 and COM1 before terminal halt.");
    }

    public static ProbeDefinition GetM03FirmwareAdapterRevocationProbe() =>
        new(UefiExitBootServicesGroup, nameof(EnableM03FirmwareAdapterRevocationProbe),
            EnableM03FirmwareAdapterRevocationProbe, ProbeRisk.Destructive,
            "Checks retained typed firmware capabilities deny after real exit and owned CR3 adoption.");

    public static ProbeDefinition GetM04ClassConstructorExecutionProbe() =>
        new("runtime.cctor", nameof(EnableM04ClassConstructorExecutionProbe),
            EnableM04ClassConstructorExecutionProbe, ProbeRisk.Destructive,
            "Executes compiler eager and lazy constructors once with single-thread recursive access.");

    public static ProbeDefinition GetM04InterfaceDispatchProbe() =>
        new("runtime.dispatch", nameof(EnableM04InterfaceDispatchProbe),
            EnableM04InterfaceDispatchProbe, ProbeRisk.Destructive,
            "Resolves compiler instance-interface cells with retained module, receiver and target authority.");

    public static ProbeDefinition GetM04X64UnwindMetadataProbe() =>
        new("runtime.unwind-metadata", nameof(EnableM04X64UnwindMetadataProbe),
            EnableM04X64UnwindMetadataProbe, ProbeRisk.Destructive,
            "Consumes current-image x64 unwind metadata through the original module registration.");

    public static ProbeDefinition GetM03OwnedSubsystemSurvivalProbe() =>
        new(UefiExitBootServicesGroup, nameof(EnableM03OwnedSubsystemSurvivalProbe),
            EnableM03OwnedSubsystemSurvivalProbe, ProbeRisk.Destructive,
            "Uses the original allocator, copied ACPI and installed descriptor observations after real exit.");

    public static ProbeDefinition GetM03OwnedSubsystemPanicProbe() =>
        new(UefiExitBootServicesGroup, nameof(EnableM03OwnedSubsystemPanicProbe),
            EnableM03OwnedSubsystemPanicProbe, ProbeRisk.Destructive,
            "Reads an original unmapped guard to enter the installed page-fault gate and halt through owned diagnostics.");

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

    public static M01LiveProbePolicyStatus
        GetConfiguredM01LiveProbePolicyStatus()
    {
        return ValidateM01LiveProbePolicy(
            EnableM01LoadedImageStackProbe,
            EnableM01LiveAllocatorProbe,
            EnableM01OwnedPagingProbe);
    }

    public static M01LiveProbePolicyStatus ValidateM01LiveProbePolicy(
        bool loadedImageStackEnabled,
        bool liveAllocatorEnabled,
        bool ownedPagingEnabled)
    {
        if (liveAllocatorEnabled &&
            !loadedImageStackEnabled)
        {
            return M01LiveProbePolicyStatus
                .LiveAllocatorRequiresLoadedImageStack;
        }

        if (ownedPagingEnabled &&
            !liveAllocatorEnabled)
        {
            return M01LiveProbePolicyStatus
                .OwnedPagingRequiresLiveAllocator;
        }

        return M01LiveProbePolicyStatus.Success;
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
            case 13:
                definition = GetM01LoadedImageStackProbe();
                return true;
            case 14:
                definition = GetM01LiveAllocatorProbe();
                return true;
            case 15:
                definition = GetM01OwnedPagingProbe();
                return true;
            case 16:
                definition = GetM03FirstRealExitBootServicesProbe();
                return true;
            case 17:
                definition = GetM03FirmwareAdapterRevocationProbe();
                return true;
            case 18:
                definition = GetM04ClassConstructorExecutionProbe();
                return true;
            case 19:
                definition = GetM04InterfaceDispatchProbe();
                return true;
            case 20:
                definition = GetM04X64UnwindMetadataProbe();
                return true;
            case 21:
                definition = GetM03OwnedSubsystemSurvivalProbe();
                return true;
            case 22:
                definition = GetM03OwnedSubsystemPanicProbe();
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
#endif
}

#if !NOSTDLIB_EFI
public enum M01LiveProbePolicyStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    LiveAllocatorRequiresLoadedImageStack = 2,
    OwnedPagingRequiresLiveAllocator = 3
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
#endif
