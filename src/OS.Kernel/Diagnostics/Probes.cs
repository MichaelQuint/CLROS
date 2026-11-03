namespace OS.Kernel.Diagnostics;

public static partial class Probes
{
#if M16_027_TIMING_PROBE
    public const bool EnableM16027TimingProbe = true;
#else
    public const bool EnableM16027TimingProbe = false;
#endif
#if M16_027_PROBE
    public const bool EnableM16027Probe = true;
#else
    public const bool EnableM16027Probe = false;
#endif
#if M16_023_PROBE
    public const bool EnableM16023Probe = true;
#else
    public const bool EnableM16023Probe = false;
#endif
#if M16_024_PROBE
    public const bool EnableM16024Probe = true;
#else
    public const bool EnableM16024Probe = false;
#endif
#if M16_025_PROBE
    public const bool EnableM16025Probe = true;
#else
    public const bool EnableM16025Probe = false;
#endif
#if M10_018_TIMING_PROBE
    public const bool EnableM10018TimingProbe = true;
#else
    public const bool EnableM10018TimingProbe = false;
#endif
#if M10_018_FULL_PROBE
    public const bool EnableM10018FullProbe = true;
#else
    public const bool EnableM10018FullProbe = false;
#endif
#if M10_018_PROBE
    public const bool EnableM10018Probe = true;
#else
    public const bool EnableM10018Probe = false;
#endif
#if M10_017_PROBE
    public const bool EnableM10017Probe = true;
#else
    public const bool EnableM10017Probe = false;
#endif
#if M16_019_UNAVAILABLE_PROBE
    public const bool EnableM16019UnavailableProbe = true;
#else
    public const bool EnableM16019UnavailableProbe = false;
#endif
#if M16_019_INVALID_PROBE
    public const bool EnableM16019InvalidProbe = true;
#else
    public const bool EnableM16019InvalidProbe = false;
#endif
#if M10_004_PROBE
    public const bool EnableM10004Probe = true;
#else
    public const bool EnableM10004Probe = false;
#endif
#if M16_011_PROBE
    public const bool EnableM16011Probe = true;
#else
    public const bool EnableM16011Probe = false;
#endif
#if M16_012_PROBE
    public const bool EnableM16012Probe = true;
#else
    public const bool EnableM16012Probe = false;
#endif
#if M16_013_PROBE
    public const bool EnableM16013Probe = true;
#else
    public const bool EnableM16013Probe = false;
#endif
#if M16_014_PROBE
    public const bool EnableM16014Probe = true;
#else
    public const bool EnableM16014Probe = false;
#endif
#if M09_012_PROBE
    public const bool EnableM09012Probe = true;
#else
    public const bool EnableM09012Probe = false;
#endif
#if M09_011_PROBE
    public const bool EnableM09011Probe = true;
#else
    public const bool EnableM09011Probe = false;
#endif
#if M09_010_PROBE
    public const bool EnableM09010Probe = true;
#else
    public const bool EnableM09010Probe = false;
#endif
#if M09_009_PROBE
    public const bool EnableM09009Probe = true;
#else
    public const bool EnableM09009Probe = false;
#endif
#if M09_006_PROBE
    public const bool EnableM09006Probe = true;
#else
    public const bool EnableM09006Probe = false;
#endif
#if M09_005_PROBE
    public const bool EnableM09005Probe = true;
#else
    public const bool EnableM09005Probe = false;
#endif
#if M09_004_PROBE
    public const bool EnableM09004Probe = true;
#else
    public const bool EnableM09004Probe = false;
#endif
#if M16_010_PROBE
    public const bool EnableM16010Probe = true;
#else
    public const bool EnableM16010Probe = false;
#endif
#if M16_009_PROBE
    public const bool EnableM16009Probe = true;
#else
    public const bool EnableM16009Probe = false;
#endif
#if M16_008_PROBE
    public const bool EnableM16008Probe = true;
#else
    public const bool EnableM16008Probe = false;
#endif
#if M16_007_PROBE
    public const bool EnableM16007Probe = true;
#else
    public const bool EnableM16007Probe = false;
#endif
#if M16_006_PROBE
    public const bool EnableM16006Probe = true;
#else
    public const bool EnableM16006Probe = false;
#endif
#if M16_005_PROBE
    public const bool EnableM16005Probe = true;
#else
    public const bool EnableM16005Probe = false;
#endif
#if M16_004_PROBE
    public const bool EnableM16004Probe = true;
#else
    public const bool EnableM16004Probe = false;
#endif
#if M16_003_PROBE
    public const bool EnableM16003Probe = true;
#else
    public const bool EnableM16003Probe = false;
#endif
#if M16_002_PROBE
    public const bool EnableM16002Probe = true;
#else
    public const bool EnableM16002Probe = false;
#endif
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
#if M04_SIMPLE_THROW_CATCH_PROBE
    public const bool EnableM04SimpleThrowCatchProbe = true;
#else
    public const bool EnableM04SimpleThrowCatchProbe = false;
#endif
#if M04_SINGLE_FINALLY_CATCH_PROBE
    public const bool EnableM04SingleFinallyCatchProbe = true;
#else
    public const bool EnableM04SingleFinallyCatchProbe = false;
#endif
#if M04_RETHROW_PROBE
    public const bool EnableM04RethrowProbe = true;
#else
    public const bool EnableM04RethrowProbe = false;
#endif
#if M04_NESTED_FINALLY_CATCH_PROBE
    public const bool EnableM04NestedFinallyCatchProbe = true;
#else
    public const bool EnableM04NestedFinallyCatchProbe = false;
#endif
#if M04_UNHANDLED_EXCEPTION_PROBE
    public const bool EnableM04UnhandledExceptionProbe = true;
#else
    public const bool EnableM04UnhandledExceptionProbe = false;
#endif
#if M04_FRAME_DISPATCH_PROBE
    public const bool EnableM04FrameDispatchProbe = true;
#else
    public const bool EnableM04FrameDispatchProbe = false;
#endif
#if M04_NORMAL_FINALLY_PROBE
    public const bool EnableM04NormalFinallyProbe = true;
#else
    public const bool EnableM04NormalFinallyProbe = false;
#endif
#if M04_EH_CATALOG_PROBE
    public const bool EnableM04EhCatalogProbe = true;
#else
    public const bool EnableM04EhCatalogProbe = false;
#endif
#if M04_CONSTRUCTOR_FAILURE_PROBE
    public const bool EnableM04ConstructorFailureProbe = true;
#else
    public const bool EnableM04ConstructorFailureProbe = false;
#endif
#if M04_FILTERED_CATCH_PROBE
    public const bool EnableM04FilteredCatchProbe = true;
#else
    public const bool EnableM04FilteredCatchProbe = false;
#endif
#if M05_OWNED_ACPI_SELECTION_PROBE
    public const bool EnableM05OwnedAcpiSelectionProbe = true;
#else
    public const bool EnableM05OwnedAcpiSelectionProbe = false;
#endif
#if M05_PHYSICAL_MAPPING_PROBE
    public const bool EnableM05PhysicalMappingProbe = true;
#else
    public const bool EnableM05PhysicalMappingProbe = false;
#endif
#if M05_MONOTONIC_CLOCK_PROBE
    public const bool EnableM05MonotonicClockProbe = true;
#else
    public const bool EnableM05MonotonicClockProbe = false;
#endif
#if M05_PCI_ENUMERATION_PROBE
    public const bool EnableM05PciEnumerationProbe = true;
#else
    public const bool EnableM05PciEnumerationProbe = false;
#endif
#if M05_BAR_DMA_PROBE
    public const bool EnableM05BarDmaProbe = true;
#else
    public const bool EnableM05BarDmaProbe = false;
#endif
#if M05_APIC_SETUP_PROBE
    public const bool EnableM05ApicSetupProbe = true;
#else
    public const bool EnableM05ApicSetupProbe = false;
#endif
#if M05_VIRTQUEUE_INTERRUPT_PROBE
    public const bool EnableM05VirtqueueInterruptProbe = true;
#else
    public const bool EnableM05VirtqueueInterruptProbe = false;
#endif
#if M05_VIRTQUEUE_POLLING_PROBE
    public const bool EnableM05VirtqueuePollingProbe = true;
#else
    public const bool EnableM05VirtqueuePollingProbe = false;
#endif
#if M05_VIRTIO_NEGOTIATION_PROBE
    public const bool EnableM05VirtioNegotiationProbe = true;
#else
    public const bool EnableM05VirtioNegotiationProbe = false;
#endif
#if M06_VIRTIO_BLOCK_QUEUE_SETUP_PROBE
    public const bool EnableM06VirtioBlockQueueSetupProbe = true;
#else
    public const bool EnableM06VirtioBlockQueueSetupProbe = false;
#endif
#if M06_VIRTIO_BLOCK_READ_PROBE
    public const bool EnableM06VirtioBlockReadProbe = true;
#else
    public const bool EnableM06VirtioBlockReadProbe = false;
#endif
#if M06_FAT_READ_PROBE
    public const bool EnableM06FatReadProbe = true;
#else
    public const bool EnableM06FatReadProbe = false;
#endif
#if M06_PACKAGE_READ_PROBE
    public const bool EnableM06PackageReadProbe = true;
#else
    public const bool EnableM06PackageReadProbe = false;
#endif
#if M06_BLOCK_MSIX_PROBE
    public const bool EnableM06BlockMsixProbe = true;
#else
    public const bool EnableM06BlockMsixProbe = false;
#endif
#if M06_BLOCK_MSIX_POLLING_RECOVERY_PROBE
    public const bool EnableM06BlockMsixPollingRecoveryProbe = true;
#else
    public const bool EnableM06BlockMsixPollingRecoveryProbe = false;
#endif
#if M07_VIRTIO_NET_PROBE
    public const bool EnableM07VirtioNetProbe = true;
#else
    public const bool EnableM07VirtioNetProbe = false;
#endif
#if M07_026_PROBE
    public const bool EnableM07026Probe = true;
#else
    public const bool EnableM07026Probe = false;
#endif
#if M07_027_PROBE
    public const bool EnableM07027Probe = true;
#else
    public const bool EnableM07027Probe = false;
#endif
#if M07_028_PROBE
    public const bool EnableM07028Probe = true;
#else
    public const bool EnableM07028Probe = false;
#endif
#if M07_ARP_RECEIVE_PROBE
    public const bool EnableM07ArpReceiveProbe = true;
#else
    public const bool EnableM07ArpReceiveProbe = false;
#endif
#if M06_GPT_PARTITION_PROBE
    public const bool EnableM06GptPartitionProbe = true;
#else
    public const bool EnableM06GptPartitionProbe = false;
#endif
#if M06_LIVE_BLOCK_CONTRACT_PROBE
    public const bool EnableM06LiveBlockContractProbe = true;
#else
    public const bool EnableM06LiveBlockContractProbe = false;
#endif
#if !NOSTDLIB_EFI
    public const int ProbeCatalogCount = 77;
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
        return new ProbeDefinition(BootSmokeGroup, nameof(EmitBootSmokeMarkers), EmitBootSmokeMarkers, ProbeRisk.Safe, "Emits OS_BOOT_OK and OS_SMOKE_PASS markers.");
    }

    public static ProbeDefinition GetSerialSmokeProbe()
    {
        return new ProbeDefinition(SerialSmokeGroup, nameof(EmitSerialSmokeMarkers), EmitSerialSmokeMarkers, ProbeRisk.Safe, "Confirms the early serial marker path is enabled.");
    }

    public static ProbeDefinition GetEfiSmokeProbe()
    {
        return new ProbeDefinition(EfiSmokeGroup, nameof(EnableEfiSmokeProbe), EnableEfiSmokeProbe, ProbeRisk.Safe, "Confirms the EFI marker path is enabled.");
    }

    public static ProbeDefinition GetMemoryMapSmokeProbe()
    {
        return new ProbeDefinition(MemoryMapSmokeGroup, nameof(EnableMemoryMapSmokeProbe), EnableMemoryMapSmokeProbe, ProbeRisk.Safe, "Confirms the UEFI memory-map smoke marker path is enabled.");
    }

    public static ProbeDefinition GetM01LoadedImageStackProbe()
    {
        return new ProbeDefinition(M01LiveMemoryGroup, nameof(EnableM01LoadedImageStackProbe), EnableM01LoadedImageStackProbe, ProbeRisk.Destructive, "Opts into future M01 stage-one loaded-image and owned-stack integration.");
    }

    public static ProbeDefinition GetM01LiveAllocatorProbe()
    {
        return new ProbeDefinition(M01LiveMemoryGroup, nameof(EnableM01LiveAllocatorProbe), EnableM01LiveAllocatorProbe, ProbeRisk.Destructive, "Opts into future M01 stage-two live-allocator integration.");
    }

    public static ProbeDefinition GetM01OwnedPagingProbe()
    {
        return new ProbeDefinition(M01LiveMemoryGroup, nameof(EnableM01OwnedPagingProbe), EnableM01OwnedPagingProbe, ProbeRisk.Destructive, "Opts into future M01 stage-three owned-paging integration.");
    }

    public static ProbeDefinition GetUefiGoneSimulationProbe()
    {
        return new ProbeDefinition(EfiSmokeGroup, nameof(EnableUefiGoneSimulationProbe), EnableUefiGoneSimulationProbe, ProbeRisk.BoundarySimulation, "Simulates firmware services being unavailable without calling ExitBootServices.");
    }

    public static ProbeDefinition GetExitBootServicesFreshMapProbe()
    {
        return new ProbeDefinition(UefiExitBootServicesGroup, nameof(EnableExitBootServicesFreshMapProbe), EnableExitBootServicesFreshMapProbe, ProbeRisk.BoundarySimulation, "Requires a fresh UEFI memory map before a future ExitBootServices attempt without calling firmware.");
    }

    public static ProbeDefinition GetM03FirstRealExitBootServicesProbe()
    {
        return new ProbeDefinition(UefiExitBootServicesGroup, nameof(EnableM03FirstRealExitBootServicesProbe), EnableM03FirstRealExitBootServicesProbe, ProbeRisk.Destructive, "Exits firmware boot services and adopts owned descriptors, CR3 and COM1 before terminal halt.");
    }

    public static ProbeDefinition GetM03FirmwareAdapterRevocationProbe() => new(UefiExitBootServicesGroup, nameof(EnableM03FirmwareAdapterRevocationProbe), EnableM03FirmwareAdapterRevocationProbe, ProbeRisk.Destructive, "Checks retained typed firmware capabilities deny after real exit and owned CR3 adoption.");
    public static ProbeDefinition GetM04ClassConstructorExecutionProbe() => new("runtime.cctor", nameof(EnableM04ClassConstructorExecutionProbe), EnableM04ClassConstructorExecutionProbe, ProbeRisk.Destructive, "Executes compiler eager and lazy constructors once with single-thread recursive access.");
    public static ProbeDefinition GetM04InterfaceDispatchProbe() => new("runtime.dispatch", nameof(EnableM04InterfaceDispatchProbe), EnableM04InterfaceDispatchProbe, ProbeRisk.Destructive, "Resolves compiler instance-interface cells with retained module, receiver and target authority.");
    public static ProbeDefinition GetM04SimpleThrowCatchProbe() => new("runtime.exception", nameof(EnableM04SimpleThrowCatchProbe), EnableM04SimpleThrowCatchProbe, ProbeRisk.Destructive, "Opts into one original-image typed throw/catch on the collecting runtime owned stack.");
    public static ProbeDefinition GetM04SingleFinallyCatchProbe() => new("runtime.exception", nameof(EnableM04SingleFinallyCatchProbe), EnableM04SingleFinallyCatchProbe, ProbeRisk.Destructive, "Opts into one bounded compiler finally before typed catch; transfer requires measured admission.");
    public static ProbeDefinition GetM04RethrowProbe() => new("runtime.exception", nameof(EnableM04RethrowProbe), EnableM04RethrowProbe, ProbeRisk.Destructive, "Opts into one compiler rethrow from inner catch to outer handler; transfer requires measured admission.");
    public static ProbeDefinition GetM04NestedFinallyCatchProbe() => new("runtime.exception", nameof(EnableM04NestedFinallyCatchProbe), EnableM04NestedFinallyCatchProbe, ProbeRisk.Destructive, "Opts into two nested compiler cleanups and typed catch search; transfer requires measured admission.");
    public static ProbeDefinition GetM04UnhandledExceptionProbe() => new("runtime.exception", nameof(EnableM04UnhandledExceptionProbe), EnableM04UnhandledExceptionProbe, ProbeRisk.Destructive, "Ends a bounded multi-frame unhandled search at its owned diagnostic boundary; native admission required.");
    public static ProbeDefinition GetM04FrameDispatchProbe() => new("runtime.exception", nameof(EnableM04FrameDispatchProbe), EnableM04FrameDispatchProbe, ProbeRisk.Destructive, "Selects one earlier-frame cleanup and an outer typed catch; exact native admission required.");
    public static ProbeDefinition GetM04ConstructorFailureProbe() => new("runtime.exception", nameof(EnableM04ConstructorFailureProbe), EnableM04ConstructorFailureProbe, ProbeRisk.Destructive, "Selects one compiled constructor failure, wrapping catch and outer typed catch; exact native admission required.");
    public static ProbeDefinition GetM04FilteredCatchProbe() => new("runtime.exception", nameof(EnableM04FilteredCatchProbe), EnableM04FilteredCatchProbe, ProbeRisk.Destructive, "Selects two ordered nonthrowing compiler filters and the selected catch; exact native admission required.");
    public static ProbeDefinition GetM04NormalFinallyProbe() => new("runtime.exception", nameof(EnableM04NormalFinallyProbe), EnableM04NormalFinallyProbe, ProbeRisk.Destructive, "Selects one throw from a normally called finally; exact native admission required.");
    public static ProbeDefinition GetM04EhCatalogProbe() => new("runtime.exception", nameof(EnableM04EhCatalogProbe), EnableM04EhCatalogProbe, ProbeRisk.Destructive, "Uses reviewed compiler contribution bounds for the frame-dispatch profile; exact native admission required.");
    public static ProbeDefinition GetM05OwnedAcpiSelectionProbe() => new("platform.acpi-selection", nameof(EnableM05OwnedAcpiSelectionProbe), EnableM05OwnedAcpiSelectionProbe, ProbeRisk.Destructive, "Selects MADT, HPET and MCFG from the original retained directory after real firmware exit.");
    public static ProbeDefinition GetM05PhysicalMappingProbe() => new("platform.mapping", nameof(EnableM05PhysicalMappingProbe), EnableM05PhysicalMappingProbe, ProbeRisk.Destructive, "Changes one exclusive scratch span between WB, UC and WC after real firmware exit.");
    public static ProbeDefinition GetM05PciEnumerationProbe() => new("platform.pci", nameof(EnableM05PciEnumerationProbe), EnableM05PciEnumerationProbe, ProbeRisk.Destructive, "Reads one retained ECAM bus with bounded enumeration after real firmware exit.");
    public static ProbeDefinition GetM05BarDmaProbe() => new("platform.bar-dma", nameof(EnableM05BarDmaProbe), EnableM05BarDmaProbe, ProbeRisk.Destructive, "Sizes quiescent PCI BARs and exercises owned DMA buffer lifetime.");
    public static ProbeDefinition GetM05ApicSetupProbe() => new("platform.apic", nameof(EnableM05ApicSetupProbe), EnableM05ApicSetupProbe, ProbeRisk.Destructive, "Validates the original BSP and masks owned local and IO APIC sources.");
    public static ProbeDefinition GetM06VirtioBlockReadProbe() => new("storage.virtio-block-read", nameof(EnableM06VirtioBlockReadProbe), EnableM06VirtioBlockReadProbe, ProbeRisk.Destructive, "Reads one sector through an owned queue, validates completion and resets before release.");
    public static ProbeDefinition GetM16002Probe() => new("app.linux-elf", nameof(EnableM16002Probe), EnableM16002Probe, ProbeRisk.Destructive, "Immutable pre-exit Linux ELF package staging; no executable entry.");
    public static ProbeDefinition GetM16003Probe() => new("workload.service-loop", nameof(EnableM16003Probe), EnableM16003Probe, ProbeRisk.Destructive, "Bounded owned service loop after staged package verification and firmware revocation.");
    public static ProbeDefinition GetM16004Probe() => new("app.linux-elf-startup", nameof(EnableM16004Probe), EnableM16004Probe, ProbeRisk.Destructive, "Bounded owned Linux ELF mapping and startup after staged package verification.");
    public static ProbeDefinition GetM16005Probe() => new("app.linux-syscalls", nameof(EnableM16005Probe), EnableM16005Probe, ProbeRisk.Destructive, "Bounded owned Linux syscall boundary after startup, service stop and firmware revocation.");
    public static ProbeDefinition GetM16006Probe() => new("app.linux-memory", nameof(EnableM16006Probe), EnableM16006Probe, ProbeRisk.Destructive, "Sparse Linux memory/startup syscalls with inactive-root publication after firmware revocation.");
    public static ProbeDefinition GetM16007Probe() => new("app.linux-descriptors", nameof(EnableM16007Probe), EnableM16007Probe, ProbeRisk.Destructive, "Owned Linux descriptors, immutable views and mechanical nonblocking pipes after firmware revocation.");
    public static ProbeDefinition GetM16008Probe() => new("app.linux-threads", nameof(EnableM16008Probe), EnableM16008Probe, ProbeRisk.Destructive, "Owned mechanical Linux threads, TLS, futex and blocking pipes after firmware revocation.");
    public static ProbeDefinition GetM16009Probe() => new("app.linux-signals", nameof(EnableM16009Probe), EnableM16009Probe, ProbeRisk.Destructive, "Owned mechanical Linux signal delivery and checked return after firmware revocation.");
    public static ProbeDefinition GetM16010Probe() => new("app.nativeaot-runtime", nameof(EnableM16010Probe), EnableM16010Probe, ProbeRisk.Destructive, "Owned NativeAOT runtime workload through the Linux signal and thread boundary after firmware revocation.");
    public static ProbeDefinition GetM16011Probe() => new("app.linux-sockets", nameof(EnableM16011Probe), EnableM16011Probe, ProbeRisk.Destructive, "Finite NativeAOT socket workload inside retained live NIC and Linux worker lifetimes.");
    public static ProbeDefinition GetM16012Probe() => new("app.linux-epoll", nameof(EnableM16012Probe), EnableM16012Probe, ProbeRisk.Destructive, "Finite original SocketAsyncEngine workload with real epoll parking and NIC readiness.");
    public static ProbeDefinition GetM16013Probe() => new("app.linux-socket-vectors", nameof(EnableM16013Probe), EnableM16013Probe, ProbeRisk.Destructive, "Finite original BufferList workload with checked vector I/O, reset, disposal and live NIC ownership.");
    public static ProbeDefinition GetM16014Probe() => new("app.continuing-service", nameof(EnableM16014Probe), EnableM16014Probe, ProbeRisk.Destructive, "Continuing original Minimal API with bounded service and live socket ownership; external protocol proof required.");
    public static ProbeDefinition GetM10004Probe() => new("app.nativeaot-web", nameof(EnableM10004Probe), EnableM10004Probe, ProbeRisk.Destructive, "Continuing original Minimal API with selected configuration and live socket owners; external HTTP proof required.");
    public static ProbeDefinition GetM16023Probe() => new("storage.continuing-readonly-block", nameof(EnableM16023Probe), EnableM16023Probe, ProbeRisk.Destructive, "Serialized retained block reads with bounded progress and explicit retirement; live device evidence required.");
    public static ProbeDefinition GetM16024Probe() => new("storage.readonly-webroot", nameof(EnableM16024Probe), EnableM16024Probe, ProbeRisk.Destructive, "Confined read-only FAT webroot with bounded cache and cursors; live file evidence required.");
    public static ProbeDefinition GetM16025Probe() => new("app.linux-diskfiles", nameof(EnableM16025Probe), EnableM16025Probe, ProbeRisk.Destructive, "Mechanical Linux disk descriptors with owned I/O waits and cleanup; native and guest evidence required.");
    public static ProbeDefinition GetM10018FullProbe() => new("app.nativeaot-disk-static-full-v1", nameof(EnableM10018FullProbe), EnableM10018FullProbe, ProbeRisk.Destructive, "Finite full disk asset and external Blazor capture; requires the disk/static parent and original consumer.");
    public static ProbeDefinition GetM10018TimingProbe() => new("app.nativeaot-disk-read-timing", nameof(EnableM10018TimingProbe), EnableM10018TimingProbe, ProbeRisk.Destructive, "Short original disk read timing diagnostic with intentional owned runner cancellation; no full or cleanup qualification.");
    public static ProbeDefinition GetM10018Probe() => new("app.nativeaot-disk-static-files", nameof(EnableM10018Probe), EnableM10018Probe, ProbeRisk.Destructive, "Finite physical-disk static-file application; independent disk, HTTP and browser evidence required.");
    public static ProbeDefinition GetM10017Probe() => new("app.nativeaot-static-files", nameof(EnableM10017Probe), EnableM10017Probe, ProbeRisk.Destructive, "Finite separate static-file Minimal API with owned SIGTERM request and real exit cleanup; external HTTP proof required.");
    public static ProbeDefinition GetM16019UnavailableProbe() => new("app.realtime-unavailable", nameof(EnableM16019UnavailableProbe), EnableM16019UnavailableProbe, ProbeRisk.Destructive, "Copied UTC-result fault: real web preparation must reject before entry; no firmware failure claim.");
    public static ProbeDefinition GetM16019InvalidProbe() => new("app.realtime-invalid", nameof(EnableM16019InvalidProbe), EnableM16019InvalidProbe, ProbeRisk.Destructive, "Copied UTC-calendar fault: real web preparation must reject before entry; no service success claim.");
    public static ProbeDefinition GetM09004Probe() => new("thread.cooperative-switch", nameof(EnableM09004Probe), EnableM09004Probe, ProbeRisk.Destructive, "Bounded two-context cooperative transfer after owned firmware exit and revocation.");
    public static ProbeDefinition GetM09005Probe() => new("thread.timer-preemption", nameof(EnableM09005Probe), EnableM09005Probe, ProbeRisk.Destructive, "Bounded owned LAPIC timer interrupt capture, deadline wake and context restoration after firmware revocation.");
    public static ProbeDefinition GetM09006Probe() => new("thread.cooperative-lifecycle", nameof(EnableM09006Probe), EnableM09006Probe, ProbeRisk.Destructive, "Bounded cooperative entry, waits, join, cancellation and off-stack reap with owned timer wakes.");
    public static ProbeDefinition GetM09009Probe() => new("thread.wait-address", nameof(EnableM09009Probe), EnableM09009Probe, ProbeRisk.Destructive, "Bounded cooperative wait-address enrollment, wake and off-stack lifetime cleanup.");
    public static ProbeDefinition GetM09010Probe() => new("thread.local-storage", nameof(EnableM09010Probe), EnableM09010Probe, ProbeRisk.Destructive, "Bounded private FS/user-GS bindings, cooperative transfer and off-stack TLS scrub.");
    public static ProbeDefinition GetM09011Probe() => new("thread.nativeaot-suspension", nameof(EnableM09011Probe), EnableM09011Probe, ProbeRisk.Destructive, "Owned NativeAOT suspension workload through the Linux signal and thread boundary after firmware revocation.");
    public static ProbeDefinition GetM09012Probe() => new("thread.nativeaot-threadpool", nameof(EnableM09012Probe), EnableM09012Probe, ProbeRisk.Destructive, "Owned NativeAOT ThreadPool workload through the Linux signal and thread boundary after firmware revocation.");
    public static ProbeDefinition GetM06GptPartitionProbe() => new("storage.gpt-partition", nameof(EnableM06GptPartitionProbe), EnableM06GptPartitionProbe, ProbeRisk.Destructive, "Validates bounded live GPT tables, backup fallback and EFI partition location.");
    public static ProbeDefinition GetM06FatReadProbe() => new("storage.fat-read", nameof(EnableM06FatReadProbe), EnableM06FatReadProbe, ProbeRisk.Destructive, "Reads bounded FAT chains, a directory with VFAT entries and a known file checksum.");
    public static ProbeDefinition GetM06LiveBlockContractProbe() => new("storage.live-block-contract", nameof(EnableM06LiveBlockContractProbe), EnableM06LiveBlockContractProbe, ProbeRisk.Destructive, "Reads LBA0 through a closed constrained block contract and validates its protective MBR.");
    public static ProbeDefinition GetM06VirtioBlockQueueSetupProbe() => new("storage.virtio-block-queue", nameof(EnableM06VirtioBlockQueueSetupProbe), EnableM06VirtioBlockQueueSetupProbe, ProbeRisk.Destructive, "Binds modern block queue zero with DMA disabled and resets before releasing its owned buffer.");
    public static ProbeDefinition GetM05VirtqueueInterruptProbe() => new("platform.virtqueue-interrupt", nameof(EnableM05VirtqueueInterruptProbe), EnableM05VirtqueueInterruptProbe, ProbeRisk.Destructive, "Completes one TX queue through a bounded returning MSI-X handler.");
    public static ProbeDefinition GetM05VirtqueuePollingProbe() => new("platform.virtqueue-polling", nameof(EnableM05VirtqueuePollingProbe), EnableM05VirtqueuePollingProbe, ProbeRisk.Destructive, "Completes one bounded split TX queue with explicit fences and reset before releasing DMA.");
    public static ProbeDefinition GetM05VirtioNegotiationProbe() => new("platform.virtio-negotiation", nameof(EnableM05VirtioNegotiationProbe), EnableM05VirtioNegotiationProbe, ProbeRisk.Destructive, "Validates modern virtio PCI capabilities and negotiates VERSION_1 after owned PCI enumeration.");
    public static ProbeDefinition GetM05MonotonicClockProbe() => new("platform.clock", nameof(EnableM05MonotonicClockProbe), EnableM05MonotonicClockProbe, ProbeRisk.Destructive, "Initializes one owned HPET clock with a bounded qualified TSC fallback after real firmware exit.");
    public static ProbeDefinition GetM04X64UnwindMetadataProbe() => new("runtime.unwind-metadata", nameof(EnableM04X64UnwindMetadataProbe), EnableM04X64UnwindMetadataProbe, ProbeRisk.Destructive, "Consumes current-image x64 unwind metadata through the original module registration.");
    public static ProbeDefinition GetM03OwnedSubsystemSurvivalProbe() => new(UefiExitBootServicesGroup, nameof(EnableM03OwnedSubsystemSurvivalProbe), EnableM03OwnedSubsystemSurvivalProbe, ProbeRisk.Destructive, "Uses the original allocator, copied ACPI and installed descriptor observations after real exit.");
    public static ProbeDefinition GetM03OwnedSubsystemPanicProbe() => new(UefiExitBootServicesGroup, nameof(EnableM03OwnedSubsystemPanicProbe), EnableM03OwnedSubsystemPanicProbe, ProbeRisk.Destructive, "Reads an original unmapped guard to enter the installed page-fault gate and halt through owned diagnostics.");
    public static ProbeDefinition GetAcpiPhysicalDiscoveryProbe()
    {
        return new ProbeDefinition(AcpiDiscoveryGroup, nameof(EnableAcpiPhysicalDiscoveryProbe), EnableAcpiPhysicalDiscoveryProbe, ProbeRisk.DeviceIo, "Dereferences firmware-provided ACPI physical addresses before ExitBootServices to validate RSDP, root SDT, and MCFG table bytes.");
    }

    public static ProbeDefinition GetNullPointerPanicProbe()
    {
        return new ProbeDefinition(PanicSmokeGroup, nameof(EnableNullPointerPanicProbe), EnableNullPointerPanicProbe, ProbeRisk.Destructive, "Intentionally triggers a null-pointer panic path.");
    }

    public static ProbeDefinition GetInvalidOpcodePanicProbe()
    {
        return new ProbeDefinition(PanicSmokeGroup, nameof(EnableInvalidOpcodePanicProbe), EnableInvalidOpcodePanicProbe, ProbeRisk.Destructive, "Intentionally triggers an invalid-opcode panic path.");
    }

    public static ProbeDefinition GetVirtioBlockReadProbe()
    {
        return new ProbeDefinition(StorageSmokeGroup, nameof(EnableVirtioBlockReadProbe), EnableVirtioBlockReadProbe, ProbeRisk.DeviceIo, "Exercises future virtio-blk read smoke behavior.");
    }

    public static ProbeDefinition GetPacketDumpProbe()
    {
        return new ProbeDefinition(NetworkSmokeGroup, nameof(EnablePacketDumpProbe), EnablePacketDumpProbe, ProbeRisk.DeviceIo, "Emits future packet dump diagnostics.");
    }

    public static ProbeDefinition GetVirtioNetworkProbe()
    {
        return new ProbeDefinition(NetworkSmokeGroup, nameof(EnableVirtioNetworkProbe), EnableVirtioNetworkProbe, ProbeRisk.DeviceIo, "Exercises future virtio-net smoke behavior.");
    }

    public static ProbeDefinition GetFetchAppHttpTcpExchangeProbe()
    {
        return new ProbeDefinition(NetworkSmokeGroup, nameof(EnableFetchAppHttpTcpExchangeProbe), EnableFetchAppHttpTcpExchangeProbe, ProbeRisk.DeviceIo, "Exercises future FetchApp HTTP-over-TCP exchange readiness marker behavior.");
    }

    public static M01LiveProbePolicyStatus GetConfiguredM01LiveProbePolicyStatus()
    {
        return ValidateM01LiveProbePolicy(EnableM01LoadedImageStackProbe, EnableM01LiveAllocatorProbe, EnableM01OwnedPagingProbe);
    }

    public static M01LiveProbePolicyStatus ValidateM01LiveProbePolicy(bool loadedImageStackEnabled, bool liveAllocatorEnabled, bool ownedPagingEnabled)
    {
        if (liveAllocatorEnabled && !loadedImageStackEnabled)
        {
            return M01LiveProbePolicyStatus.LiveAllocatorRequiresLoadedImageStack;
        }

        if (ownedPagingEnabled && !liveAllocatorEnabled)
        {
            return M01LiveProbePolicyStatus.OwnedPagingRequiresLiveAllocator;
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
            case 23:
                definition = GetM04SimpleThrowCatchProbe();
                return true;
            case 24:
                definition = GetM04SingleFinallyCatchProbe();
                return true;
            case 25:
                definition = GetM04RethrowProbe();
                return true;
            case 26:
                definition = GetM05PhysicalMappingProbe();
                return true;
            case 27:
                definition = GetM05OwnedAcpiSelectionProbe();
                return true;
            case 30:
                definition = GetM05BarDmaProbe();
                return true;
            case 33:
                definition = GetM05ApicSetupProbe();
                return true;
            case 34:
                definition = GetM05VirtqueueInterruptProbe();
                return true;
            case 36:
                definition = GetM06VirtioBlockReadProbe();
                return true;
            case 39:
                definition = GetM06FatReadProbe();
                return true;
            case 38:
                definition = GetM06GptPartitionProbe();
                return true;
            case 37:
                definition = GetM06LiveBlockContractProbe();
                return true;
            case 35:
                definition = GetM06VirtioBlockQueueSetupProbe();
                return true;
            case 32:
                definition = GetM05VirtqueuePollingProbe();
                return true;
            case 31:
                definition = GetM05VirtioNegotiationProbe();
                return true;
            case 29:
                definition = GetM05PciEnumerationProbe();
                return true;
            case 28:
                definition = GetM05MonotonicClockProbe();
                return true;
            case 40:
                definition = GetM04NestedFinallyCatchProbe();
                return true;
            case 41:
                definition = GetM04UnhandledExceptionProbe();
                return true;
            case 42:
                definition = GetM04FrameDispatchProbe();
                return true;
            case 43:
                definition = GetM04ConstructorFailureProbe();
                return true;
            case 44:
                definition = GetM04FilteredCatchProbe();
                return true;
            case 45:
                definition = GetM04EhCatalogProbe();
                return true;
            case 46:
                definition = GetM04NormalFinallyProbe();
                return true;
            case 47:
                definition = GetM16002Probe();
                return true;
            case 48:
                definition = GetM16003Probe();
                return true;
            case 49:
                definition = GetM16004Probe();
                return true;
            case 50:
                definition = GetM16005Probe();
                return true;
            case 51:
                definition = GetM16006Probe();
                return true;
            case 52:
                definition = GetM16007Probe();
                return true;
            case 53:
                definition = GetM09004Probe();
                return true;
            case 54:
                definition = GetM09005Probe();
                return true;
            case 55:
                definition = GetM09006Probe();
                return true;
            case 56:
                definition = GetM09009Probe();
                return true;
            case 57:
                definition = GetM09010Probe();
                return true;
            case 58:
                definition = GetM16008Probe();
                return true;
            case 59:
                definition = GetM16009Probe();
                return true;
            case 60:
                definition = GetM09011Probe();
                return true;
            case 61:
                definition = GetM09012Probe();
                return true;
            case 62:
                definition = GetM16010Probe();
                return true;
            case 63:
                definition = GetM16011Probe();
                return true;
            case 64:
                definition = GetM16012Probe();
                return true;
            case 65:
                definition = GetM10004Probe();
                return true;
            case 66:
                definition = GetM16013Probe();
                return true;
            case 67:
                definition = GetM16019UnavailableProbe();
                return true;
            case 68:
                definition = GetM16019InvalidProbe();
                return true;
            case 69:
                definition = GetM16014Probe();
                return true;
            case 70:
                definition = GetM10017Probe();
                return true;
            case 71:
                definition = GetM16023Probe();
                return true;
            case 72:
                definition = GetM16024Probe();
                return true;
            case 73:
                definition = GetM16025Probe();
                return true;
            case 74:
                definition = GetM10018Probe();
                return true;
            case 75:
                definition = GetM10018FullProbe();
                return true;
            case 76:
                definition = GetM10018TimingProbe();
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
            return ProbeCatalogCopyResult.Fail(ProbeCatalogStatus.DestinationTooSmall, ProbeCatalogCount, destination.Length, writtenCount: 0);
        }

        for (int i = 0; i < ProbeCatalogCount; i++)
        {
            if (!TryGetProbe(i, out ProbeDefinition definition))
            {
                return ProbeCatalogCopyResult.Fail(ProbeCatalogStatus.IndexOutOfRange, ProbeCatalogCount, destination.Length, i);
            }

            destination[i] = definition;
        }

        return ProbeCatalogCopyResult.Success(ProbeCatalogCount, destination.Length, ProbeCatalogCount);
    }
#endif
}
#if !NOSTDLIB_EFI
#endif
