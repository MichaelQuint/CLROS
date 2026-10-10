#if !NOSTDLIB_EFI
using System.Text;
using OS.Drivers;
using OS.Manifest;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Kernel.Runtime;
using OS.Net;
using OS.Pal;
using OS.Storage;
#endif

namespace OS.Boot;

#if !NOSTDLIB_EFI
public enum EarlyDiagnosticsReadinessStatus
{
    Ready = 0,
    SerialBackendNotReady = 1,
    DescriptorTablesNotReady = 2
}

public enum ControlledFaultProbeReadinessStatus
{
    Ready = 0,
    EarlyDiagnosticsNotReady = 1,
    FaultProbePlanNotReady = 2
}

public enum CpuFaultPanicBootReadinessStatus
{
    Ready = 0,
    PanicReadinessFailed = 1
}

public enum DiagnosticStubMaterializationReadinessStatus
{
    Ready = 0,
    MaterializerFailed = 1,
    NoMaterializedPayloadEvidence = 2
}

public enum BootPageAllocatorReadinessStatus
{
    Ready = 0,
    StateInitializationFailed = 1,
    StateNotInitialized = 2,
    NoFreeNodes = 3,
    NoFreePages = 4
}

public enum AppSelectionReadinessStatus
{
    Ready = 0,
    ManifestFailed = 1,
    PackageFailed = 2,
    LaunchPlanFailed = 3,
    LaunchDescriptorFailed = 4
}

public enum AppSelectionPreflightReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    IncompleteReadSession = 2,
    InvalidBlockDevice = 3,
    ScratchPlanFailed = 4,
    SelectionLoadFailed = 5
}

public enum AppEntryReadinessStatus
{
    Ready = 0,
    AppPackageFailed = 1,
    AppSelectionFailed = 2,
    PayloadMappingFailed = 3,
    ExecutableMemoryFailed = 4,
    InvocationPlanFailed = 5,
    StagingPreflightFailed = 6
}

public enum AppPackageStagingReadinessStatus
{
    Ready = 0,
    StagingReadinessFailed = 1
}

public enum AppPackageFileStagingPreflightReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    PackagePreflightFailed = 2,
    PackageOpenFailed = 3,
    PackageStagingFailed = 4
}

public enum PalExportCatalogReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    CatalogValidationFailed = 2
}

public enum NativeAotHeapBootstrapReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    PlanFailed = 2
}

public enum NativeAotClassConstructorRunnerPreflightReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    PreflightFailed = 2
}

public enum NativeAotClassConstructorWorkItemReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    PlanFailed = 2
}

public enum FetchAppHttpCompletionReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    CompletionFailed = 2
}

public enum FetchAppHttpTcpExchangeReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    ExchangeFailed = 2,
    DiagnosticFormattingFailure = 3
}

public enum VirtioNetRxTcpDispatchReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    DispatchFailed = 2,
    DiagnosticFormattingFailure = 3
}

public enum HttpStaticTcpResponseReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    ResponsePlanFailed = 2,
    DiagnosticFormattingFailure = 3
}

public enum FetchAppHttpRequestEndpointReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    EndpointFailed = 2
}

public enum NativeManifestFetchAppConfigPreflightReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    PreflightFailed = 2
}

public enum NativeManifestEnvironmentReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    ParseFailure = 2,
    SourceRangeInvalid = 3,
    DiagnosticFormattingFailure = 4
}

public enum AppExitObserverReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    ObserverFailure = 2
}

public enum NativeAppArgumentVectorReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    ArgumentVectorFailed = 2
}

public enum NativeAppArgumentHandoffReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    HandoffFailed = 2
}

#endif
public static class BootSequence
{
    /// <summary>
    /// Terminal boot boundary, after the original M01 allocator and copied
    /// ACPI. The ordinary policy defers success markers until owned post-exit checks.
    /// The adapter enters on the prepared owned stack;
    /// this boundary consumes firmware authority before the final map/exit window.
    /// Success transfers directly to owned descriptor/CR3 adoption and cannot return.
    /// </summary>
    public static unsafe OS.Boot.FirstExitReturn RunFirstRealExitBootServicesTransition(
        ref OS.Boot.FirstExitOwnedArenaState arena, byte* code)
    {
        if (!IsFirstRealExitBootServicesEnabled())
            return OS.Boot.FirstExitReturn.NotStarted;
        if (!arena.IsReady || code == null ||
            !OS.Boot.FirstExitOwnedArena.ValidateCanaries(arena))
            return OS.Boot.FirstExitReturn.InvalidStorage;
        if (OS.Boot.FirstExitFirmwareBinding.Consume(ref *arena.Binding,
                OS.Kernel.Platform.FirmwareServicesMode.BootServicesAvailable,
                out var getMap, out var exit, out void* handle) != OS.Boot.FirstExitBindingStatus.Consumed)
            return OS.Boot.FirstExitReturn.FirmwareUnavailable;
        return OS.Boot.FirstOptRealExitBootServicesTransition.AttemptAndTransfer(ref *arena.Transition,
            OS.Kernel.Platform.FirmwareServicesMode.BootServicesAvailable, getMap, exit, handle,
            arena.Map, OS.Boot.FirstExitOwnedArena.MapBytes,
            (delegate* unmanaged<OS.Boot.FirstExitTransitionState*, void*, void>)
                (code + OS.Boot.FirstExitCodeBundle.PermanentOffset),
            arena.Permanent, OS.Boot.FirstExitPermanentCr3Stub.ContextBytes);
    }

    private static bool IsFirstRealExitBootServicesEnabled() =>
        VerifiedExitPathOptDefaultRepeatedCleanQEMU.Enabled ||
        OS.Kernel.Diagnostics.Probes.EnableM03FirstRealExitBootServicesProbe;

    /// <summary>After retained module/static publication and before constructor consumers.</summary>
    public static unsafe OS.Kernel.Runtime.CompilerClassConstructorStatus RunCompilerClassConstructors(
        byte* storage, ulong capacity) =>
        OS.Kernel.Diagnostics.Probes.EnableM04ClassConstructorExecutionProbe
            ? OS.Kernel.Runtime.CompilerClassConstructorExecution.RunEager(storage, capacity)
            : OS.Kernel.Runtime.CompilerClassConstructorStatus.NotBound;

    public static bool IsFirmwareAdapterRevocationEnabled() =>
        OS.Kernel.Diagnostics.Probes.EnableM03FirmwareAdapterRevocationProbe;

    public static bool IsSimpleThrowCatchEnabled() =>
        OS.Kernel.Diagnostics.Probes.EnableM04SimpleThrowCatchProbe;

    public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunSimpleThrowCatchUnwind(
        OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping,
        byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity,
        byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input) =>
        IsSimpleThrowCatchEnabled()
            ? OS.Kernel.Runtime.X64FrameUnwind.UnwindOne(
                OS.Kernel.Runtime.NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, capture, mapping,
                owner, ownerCapacity, handle, metadata, metadataCapacity, stack, stackBytes, input)
            : default;

    public static bool IsSingleFinallyCatchEnabled() =>
        OS.Kernel.Diagnostics.Probes.EnableM04SingleFinallyCatchProbe;

    public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunSingleFinallyCatchUnwind(
        OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping,
        byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity,
        byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input) =>
        IsSingleFinallyCatchEnabled()
            ? OS.Kernel.Runtime.X64FrameUnwind.UnwindOne(
                OS.Kernel.Runtime.NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, capture, mapping,
                owner, ownerCapacity, handle, metadata, metadataCapacity, stack, stackBytes, input)
            : default;

    public static bool IsRethrowEnabled() => OS.Kernel.Diagnostics.Probes.EnableM04RethrowProbe;

    // Runs only after the original owned diagnostics and allocator/ACPI checks.
    // The adapter authenticates its state and halts on every return.
    public static unsafe bool RunOwnedAcpiSelection(
        bool ownedSubsystemsComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM05OwnedAcpiSelectionProbe &&
        ownedSubsystemsComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedPciEnumeration(
        bool ownedSubsystemsComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM05PciEnumerationProbe &&
        ownedSubsystemsComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedBarDma(
        bool pciEnumerationComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM05BarDmaProbe &&
        pciEnumerationComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedApicSetup(
        bool pciEnumerationComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM05ApicSetupProbe &&
        pciEnumerationComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedVirtqueueInterrupt(
        bool apicAndNegotiationComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM05VirtqueueInterruptProbe &&
        apicAndNegotiationComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedVirtqueuePolling(
        bool virtioNegotiationComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM05VirtqueuePollingProbe &&
        virtioNegotiationComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedVirtioNegotiation(
        bool pciEnumerationComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM05VirtioNegotiationProbe &&
        pciEnumerationComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedVirtioBlockQueueSetup(
        bool pciEnumerationComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM06VirtioBlockQueueSetupProbe &&
        pciEnumerationComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedVirtioBlockRead(
        bool blockQueueSetupComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM06VirtioBlockReadProbe &&
        blockQueueSetupComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedGptPartition(
        bool liveBlockContractComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM06GptPartitionProbe &&
        liveBlockContractComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedFatRead(
        bool gptPartitionComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM06FatReadProbe &&
        gptPartitionComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedLiveBlockContract(
        bool blockReadComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM06LiveBlockContractProbe &&
        blockReadComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunOwnedMonotonicClock(
        bool ownedSubsystemsComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM05MonotonicClockProbe &&
        ownedSubsystemsComplete && context != null && execute != null && execute(context);

    public static unsafe bool RunPostFirmwarePhysicalMapping(
        bool ownedSubsystemsComplete, void* context, delegate*<void*, bool> execute) =>
        OS.Kernel.Diagnostics.Probes.EnableM05PhysicalMappingProbe &&
        ownedSubsystemsComplete && context != null && execute != null && execute(context);

    public static unsafe OS.Kernel.Runtime.X64FrameUnwindResult RunRethrowUnwind(
        OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping,
        byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity,
        byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input) =>
        IsRethrowEnabled()
            ? OS.Kernel.Runtime.X64FrameUnwind.UnwindOne(
                OS.Kernel.Runtime.NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian, capture, mapping,
                owner, ownerCapacity, handle, metadata, metadataCapacity, stack, stackBytes, input)
            : default;

    /// <summary>Look up current-image metadata after binding the original retained module.</summary>
    public static unsafe OS.Kernel.Runtime.X64UnwindMetadataResult RunX64UnwindMetadata(
        byte* storage, ulong capacity, ulong instructionPointer) =>
        OS.Kernel.Diagnostics.Probes.EnableM04X64UnwindMetadataProbe
            ? OS.Kernel.Runtime.X64PERuntimeFunctionUnwindMetadataHostileInput.Lookup(
                storage, capacity, instructionPointer)
            : default;

    /// <summary>One original owned allocator operation after real exit; the operation invokes diagnostics once.</summary>
    public static unsafe OS.Boot.OwnedAllocatorRoundTripStatus RunOwnedSubsystemAllocatorRoundTrip(
        ref OS.Boot.OwnedAllocatorRoundTripState state, ref OS.Boot.FirstExitDiagnosticState diagnostic,
        ref OS.Boot.FirstExitOwnedArenaState arena, OS.Boot.FirstExitDiagnosticMemory memory,
        OS.Boot.M01LiveAllocatorStaticState* owner) =>
        (VerifiedExitPathOptDefaultRepeatedCleanQEMU.Enabled ||
            OS.Kernel.Diagnostics.Probes.EnableM03OwnedSubsystemSurvivalProbe)
            ? OS.Boot.OwnedAllocatorDescriptorTablesACPISnapshotPanicOutput.RunAllocatorRoundTrip(
                ref state, ref diagnostic, ref arena, memory, owner)
            : OS.Boot.OwnedAllocatorRoundTripStatus.AdmissionFailed;

    /// <summary>Normal success is emitted only by the admitted owned post-exit writer.</summary>
    public static unsafe bool ReportVerifiedExitCompletion(void* context,
        delegate*<void*, byte*, uint, bool> write) =>
        VerifiedExitPathOptDefaultRepeatedCleanQEMU.Enabled &&
        VerifiedExitPathOptDefaultRepeatedCleanQEMU.WriteCompletion(context, write);

    /// <summary>Resolve only after the retained module, allocator and compiler-cell snapshot are bound.</summary>
    public static unsafe OS.Kernel.Runtime.CompilerInterfaceDispatchStatus RunInterfaceDispatch(
        byte* storage, ulong capacity, ulong receiver, ulong cell,
        out OS.Kernel.Runtime.CompilerInterfaceDispatchResolution resolution)
    {
        resolution = default;
        return OS.Kernel.Diagnostics.Probes.EnableM04InterfaceDispatchProbe
            ? OS.Kernel.Runtime.CompilerInterfaceDispatch.Resolve(
                storage, capacity, receiver, cell, out resolution)
            : OS.Kernel.Runtime.CompilerInterfaceDispatchStatus.NotBound;
    }

#if !NOSTDLIB_EFI
    private const int NativeManifestEnvironmentDiagnosticMarkerCapacity = 512;
    private const int FetchAppHttpTcpExchangeDiagnosticMarkerCapacity = 1024;
    private const int VirtioNetRxTcpDispatchDiagnosticMarkerCapacity = 1024;
    private const int HttpStaticTcpResponseDiagnosticMarkerCapacity = 1024;

    public const string BootMemoryMapAccepted = "BOOT_MEMORY_MAP_ACCEPTED";
    public const string BootMemoryMapRejectedPrefix = "BOOT_MEMORY_MAP_REJECTED ";
    public const string FaultInfrastructureReady = "FAULT_INFRA_READY";
    public const string FaultInfrastructureNotReadyPrefix = "FAULT_INFRA_NOT_READY status=";
    public const string EarlyDiagnosticsReady = "EARLY_DIAGNOSTICS_READY";
    public const string EarlyDiagnosticsNotReadyPrefix = "EARLY_DIAGNOSTICS_NOT_READY status=";
    public const string ControlledFaultProbesReady = "CONTROLLED_FAULT_PROBES_READY";
    public const string ControlledFaultProbesNotReadyPrefix = "CONTROLLED_FAULT_PROBES_NOT_READY status=";
    public const string CpuFaultPanicReady = "CPU_FAULT_PANIC_READY";
    public const string CpuFaultPanicNotReadyPrefix = "CPU_FAULT_PANIC_NOT_READY ";
    public const string DiagnosticStubsMaterialized = "DIAGNOSTIC_STUBS_MATERIALIZED";
    public const string DiagnosticStubsNotMaterializedPrefix = "DIAGNOSTIC_STUBS_NOT_MATERIALIZED status=";
    public const string DiagnosticsChainReady = "DIAGNOSTICS_CHAIN_READY";
    public const string DiagnosticsChainNotReadyPrefix = "DIAGNOSTICS_CHAIN_NOT_READY ";
    public const string DiagnosticStubInvocationReady = "DIAGNOSTIC_STUB_INVOCATION_READY";
    public const string DiagnosticStubInvocationNotReadyPrefix = "DIAGNOSTIC_STUB_INVOCATION_NOT_READY ";
    public const string BootPageAllocatorReadyPrefix = "BOOT_PAGE_ALLOCATOR_READY ";
    public const string BootPageAllocatorNotReadyPrefix = "BOOT_PAGE_ALLOCATOR_NOT_READY ";
    public const string PostFirmwareDiagnosticsReady = "POST_FIRMWARE_DIAGNOSTICS_READY";
    public const string PostFirmwareDiagnosticsBlockedPrefix = "POST_FIRMWARE_DIAGNOSTICS_BLOCKED status=";
    public const string ExitBootServicesPreflightReady = "UEFI_EBS_PREFLIGHT_READY";
    public const string ExitBootServicesPreflightBlockedPrefix = "UEFI_EBS_PREFLIGHT_BLOCKED diagnostics=";
    public const string ExitBootServicesTransitionReady = "UEFI_EBS_TRANSITION_READY";
    public const string ExitBootServicesTransitionBlockedPrefix = "UEFI_EBS_TRANSITION_BLOCKED status=";
    public const string AppPackageReady = "APP_PACKAGE_READY";
    public const string AppPackageNotReadyPrefix = "APP_PACKAGE_NOT_READY ";
    public const string AppPackageStagingReady = "APP_PACKAGE_STAGING_READY";
    public const string AppPackageStagingNotReadyPrefix = "APP_PACKAGE_STAGING_NOT_READY ";
    public const string AppPackageFileStagingPreflightReadyPrefix =
        "APP_PACKAGE_FILE_STAGING_PREFLIGHT_READY ";
    public const string AppPackageFileStagingPreflightNotReadyPrefix =
        "APP_PACKAGE_FILE_STAGING_PREFLIGHT_NOT_READY ";
    public const string PalExportCatalogReadyPrefix = "PAL_EXPORT_CATALOG_READY ";
    public const string PalExportCatalogNotReadyPrefix = "PAL_EXPORT_CATALOG_NOT_READY ";
    public const string NativeAotHeapBootstrapReadyPrefix = "NATIVEAOT_HEAP_BOOTSTRAP_READY ";
    public const string NativeAotHeapBootstrapNotReadyPrefix = "NATIVEAOT_HEAP_BOOTSTRAP_NOT_READY ";
    public const string NativeAotClassConstructorRunnerPreflightReadyPrefix =
        "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_READY ";
    public const string NativeAotClassConstructorRunnerPreflightNotReadyPrefix =
        "NATIVEAOT_CCTOR_RUNNER_PREFLIGHT_NOT_READY ";
    public const string NativeAotClassConstructorWorkItemReadyPrefix =
        "NATIVEAOT_CCTOR_WORK_ITEMS_READY ";
    public const string NativeAotClassConstructorWorkItemNotReadyPrefix =
        "NATIVEAOT_CCTOR_WORK_ITEMS_NOT_READY ";
    public const string FetchAppHttpRequestEndpointReadyPrefix = "FETCHAPP_HTTP_ENDPOINT_READY ";
    public const string FetchAppHttpRequestEndpointNotReadyPrefix =
        "FETCHAPP_HTTP_ENDPOINT_NOT_READY ";
    public const string FetchAppHttpCompletionReadyPrefix = "FETCH_APP_HTTP_COMPLETION_READY ";
    public const string FetchAppHttpCompletionNotReadyPrefix =
        "FETCH_APP_HTTP_COMPLETION_NOT_READY ";
    public const string FetchAppHttpTcpExchangeReadyPrefix =
        "FETCH_APP_HTTP_TCP_EXCHANGE_READY ";
    public const string FetchAppHttpTcpExchangeNotReadyPrefix =
        "FETCH_APP_HTTP_TCP_EXCHANGE_NOT_READY ";
    public const string VirtioNetRxTcpDispatchReady = "VIRTIO_NET_RX_TCP_DISPATCH_READY";
    public const string VirtioNetRxTcpDispatchNotReadyPrefix =
        "VIRTIO_NET_RX_TCP_DISPATCH_NOT_READY ";
    public const string HttpStaticTcpResponseReady = "HTTP_STATIC_TCP_RESPONSE_READY";
    public const string HttpStaticTcpResponseNotReadyPrefix =
        "HTTP_STATIC_TCP_RESPONSE_NOT_READY ";
    public const string NativeManifestFetchAppConfigPreflightReadyPrefix =
        "FETCHAPP_CONFIG_PREFLIGHT_READY ";
    public const string NativeManifestFetchAppConfigPreflightNotReadyPrefix =
        "FETCHAPP_CONFIG_PREFLIGHT_NOT_READY ";
    public const string NativeManifestEnvironmentReadyPrefix = "NATIVE_MANIFEST_ENV_READY ";
    public const string NativeManifestEnvironmentNotReadyPrefix = "NATIVE_MANIFEST_ENV_NOT_READY ";
    public const string AppExitObserverReadyPrefix = "APP_EXIT_OBSERVER_READY ";
    public const string AppExitObserverNotReadyPrefix = "APP_EXIT_OBSERVER_NOT_READY ";
    public const string NativeAppArgumentVectorReadyPrefix = "APP_ARGS_VECTOR_READY ";
    public const string NativeAppArgumentVectorNotReadyPrefix = "APP_ARGS_VECTOR_NOT_READY ";
    public const string NativeAppArgumentHandoffReadyPrefix = "APP_ARGS_HANDOFF_READY ";
    public const string NativeAppArgumentHandoffNotReadyPrefix = "APP_ARGS_HANDOFF_NOT_READY ";
    public const string AppLaunchReady = "APP_LAUNCH_READY";
    public const string AppLaunchNotReadyPrefix = "APP_LAUNCH_NOT_READY ";
    public const string AppPayloadReady = "APP_PAYLOAD_READY";
    public const string AppPayloadNotReadyPrefix = "APP_PAYLOAD_NOT_READY ";
    public const string AppExecutableMappingReady = "APP_EXEC_MAPPING_READY";
    public const string AppExecutableMappingNotReadyPrefix = "APP_EXEC_MAPPING_NOT_READY ";
    public const string AppExecutableMemoryReady = "APP_EXEC_MEMORY_READY";
    public const string AppExecutableMemoryNotReadyPrefix = "APP_EXEC_MEMORY_NOT_READY ";
    public const string AppInvocationReady = "APP_INVOCATION_READY";
    public const string AppInvocationNotReadyPrefix = "APP_INVOCATION_NOT_READY ";
    public const string AppEntryReady = "APP_ENTRY_READY";
    public const string AppEntryNotReadyPrefix = "APP_ENTRY_NOT_READY ";
    public const string AppLifecycleReady = "APP_LIFECYCLE_READY";
    public const string AppLifecycleNotReadyPrefix = "APP_LIFECYCLE_NOT_READY ";
    public const string AppSelectionReady = "APP_SELECTION_READY";
    public const string AppSelectionNotReadyPrefix = "APP_SELECTION_NOT_READY ";
    public const string AppSelectionPreflightReady = "APP_SELECTION_PREFLIGHT_READY";
    public const string AppSelectionPreflightNotReadyPrefix = "APP_SELECTION_PREFLIGHT_NOT_READY ";
    public const string UefiGoneSimulationStart = "UEFI_GONE_SIM_START";
    public const string UefiGoneSimulationPlatformDenied = "UEFI_GONE_SIM_PLATFORM_DENIED";
    public const string UefiGoneSimulationPass = "UEFI_GONE_SIM_PASS";
    public const string M01StageOneTestFailurePrefix =
        "TEST_FAIL m01.stage1 status=";
    public const string M01StageOneFailurePrefix =
        "M01_STAGE1_FAIL status=";
    public const string M01StageTwoTestFailurePrefix =
        "TEST_FAIL m01.stage2 status=";
    public const string M01StageTwoFailurePrefix =
        "M01_STAGE2_FAIL status=";
    public const string M01StageThreeTestFailurePrefix =
        "TEST_FAIL m01.stage3 status=";
    public const string M01StageThreeFailurePrefix =
        "M01_STAGE3_FAIL status=";

    /// <summary>
    /// Presents already-completed M01 stage-one host evidence through the
    /// frozen byte plan. This helper does not prepare the coordinator, invoke
    /// firmware, or execute the stack-switch target, and the default Run
    /// overloads never call it.
    /// </summary>
    public static BootResult RunM01LoadedImageStackIntegration(
        M01LoadedImageStackIntegrationResult integration,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!integration.Succeeded)
        {
            M01LoadedImageStackStatus failureStatus =
                integration.Status == M01LoadedImageStackStatus.Success
                    ? M01LoadedImageStackStatus
                        .IllegalCoordinatorState
                    : integration.Status;
            string failureDetail =
                failureStatus
                + " checkpoint="
                + integration.LastCompletedCheckpoint;
            Emit(
                serial,
                events,
                CreateM01StageOneSuccessLine(0));
            Emit(
                serial,
                events,
                M01StageOneTestFailurePrefix + failureDetail);
            Emit(
                serial,
                events,
                M01StageOneFailurePrefix + failureDetail);
            return BootResult.Fail(
                "M01 stage one failed: " + failureDetail);
        }

        for (int lineIndex = 0;
            lineIndex
                < BootSequencePlan.M01StageOneSuccessLineCount;
            lineIndex++)
        {
            Emit(
                serial,
                events,
                CreateM01StageOneSuccessLine(lineIndex));
        }

        return BootResult.Pass();
    }

    /// <summary>
    /// Presents already-completed M01 stage-two host evidence through the
    /// frozen byte plan. Success markers are retrospective: no stage-two
    /// success line is emitted until the supplied result reports the complete
    /// alloc/free/double-free-rejection cycle as successful. This helper does
    /// not call firmware, allocate memory, or mutate allocator state, and the
    /// default Run overloads never call it.
    /// </summary>
    public static BootResult RunM01LiveAllocatorIntegration(
        M01LiveAllocatorIntegrationResult integration,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!integration.Succeeded)
        {
            M01LiveAllocatorStatus failureStatus =
                integration.Status == M01LiveAllocatorStatus.Success
                    ? M01LiveAllocatorStatus.IllegalCoordinatorState
                    : integration.Status;
            string failureDetail =
                ((byte)failureStatus)
                + " checkpoint="
                + (byte)integration.LastCompletedCheckpoint;
            Emit(
                serial,
                events,
                CreateM01StageTwoSuccessLine(0));
            Emit(
                serial,
                events,
                M01StageTwoTestFailurePrefix + failureDetail);
            Emit(
                serial,
                events,
                M01StageTwoFailurePrefix + failureDetail);
            return BootResult.Fail(
                "M01 stage two failed: status=" + failureDetail);
        }

        for (int lineIndex = 0;
            lineIndex
                < BootSequencePlan.M01StageTwoSuccessLineCount;
            lineIndex++)
        {
            Emit(
                serial,
                events,
                CreateM01StageTwoSuccessLine(lineIndex));
        }

        return BootResult.Pass();
    }

    /// <summary>
    /// Presents already-completed M01 stage-three host evidence through the
    /// frozen byte plan. Success markers are retrospective: no stage-three
    /// success line is emitted until the supplied result reports the complete
    /// separately modeled reversible owned-CR3, protection, invalidation,
    /// probe, restoration, canary, and page-zero audits as successful. The
    /// result does not prove that those leaves executed in one machine-code
    /// interval. This helper does not call firmware or execute privileged
    /// instructions, and the default Run overloads never call it.
    /// </summary>
    public static BootResult RunM01LivePagingIntegration(
        M01LivePagingIntegrationResult integration,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!integration.Succeeded)
        {
            M01LivePagingStatus failureStatus =
                integration.Status == M01LivePagingStatus.Success
                    ? M01LivePagingStatus.IllegalCoordinatorState
                    : integration.Status;
            string failureDetail =
                ((byte)failureStatus)
                + " checkpoint="
                + (byte)integration.LastCompletedCheckpoint;
            Emit(
                serial,
                events,
                CreateM01StageThreeSuccessLine(0));
            Emit(
                serial,
                events,
                M01StageThreeTestFailurePrefix + failureDetail);
            Emit(
                serial,
                events,
                M01StageThreeFailurePrefix + failureDetail);
            return BootResult.Fail(
                "M01 stage three failed: status=" + failureDetail);
        }

        for (int lineIndex = 0;
            lineIndex
                < BootSequencePlan.M01StageThreeSuccessLineCount;
            lineIndex++)
        {
            Emit(
                serial,
                events,
                CreateM01StageThreeSuccessLine(lineIndex));
        }

        return BootResult.Pass();
    }

    public static BootResult Run(ISerialSink serial)
    {
        BootEventRingBuffer events = new BootEventRingBuffer();
        return Run(serial, events);
    }

    public static BootResult Run(ISerialSink serial, BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        Emit(serial, events, CreatePlanLine(0));
        EmitImplementedPhases(serial, events);

        if (IsUefiGoneSimulationProbeEnabled())
        {
            FirmwareServicesGuard firmware = new FirmwareServicesGuard();
            BootResult simulationResult = RunUefiGoneSimulation(firmware, serial, events);
            if (!simulationResult.Succeeded)
            {
                return simulationResult;
            }
        }

        if (Probes.EmitBootSmokeMarkers)
        {
            for (int lineIndex = BootSequencePlan.NonSmokeLineCount; lineIndex < BootSequencePlan.LineCount; lineIndex++)
            {
                Emit(serial, events, CreatePlanLine(lineIndex));
            }
        }

        return BootResult.Pass();
    }

    public static BootResult Run(BootHandoffRecord handoffRecord, ISerialSink serial)
    {
        BootEventRingBuffer events = new BootEventRingBuffer();
        return Run(handoffRecord, serial, events);
    }

    public static BootResult Run(
        BootHandoffRecord handoffRecord,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        Emit(serial, events, CreatePlanLine(0));

        BootSequenceHandoffValidationResult handoffValidation = ValidateHandoff(handoffRecord);
        if (!handoffValidation.Succeeded)
        {
            Emit(
                serial,
                events,
                BootSequenceHandoffValidator.CreateFailMarker(handoffValidation.Status));
            return BootResult.Fail("boot handoff validation failed: " + handoffValidation.Status);
        }

        Emit(serial, events, BootSequenceHandoffValidator.ReadyMarker);
        EmitImplementedPhases(serial, events);

        if (IsUefiGoneSimulationProbeEnabled())
        {
            FirmwareServicesGuard firmware = new FirmwareServicesGuard();
            BootResult simulationResult = RunUefiGoneSimulation(firmware, serial, events);
            if (!simulationResult.Succeeded)
            {
                return simulationResult;
            }
        }

        if (Probes.EmitBootSmokeMarkers)
        {
            for (int lineIndex = BootSequencePlan.NonSmokeLineCount; lineIndex < BootSequencePlan.LineCount; lineIndex++)
            {
                Emit(serial, events, CreatePlanLine(lineIndex));
            }
        }

        return BootResult.Pass();
    }

    public static BootResult Run(
        BootHandoffRecord handoffRecord,
        EfiMemoryMapSummary normalizedMemoryMap,
        ISerialSink serial)
    {
        BootEventRingBuffer events = new BootEventRingBuffer();
        return Run(handoffRecord, normalizedMemoryMap, serial, events);
    }

    public static BootResult Run(
        BootHandoffRecord handoffRecord,
        EfiMemoryMapSummary normalizedMemoryMap,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        Emit(serial, events, CreatePlanLine(0));

        BootSequenceHandoffValidationResult handoffValidation = ValidateHandoff(handoffRecord);
        if (!handoffValidation.Succeeded)
        {
            Emit(
                serial,
                events,
                BootSequenceHandoffValidator.CreateFailMarker(handoffValidation.Status));
            return BootResult.Fail("boot handoff validation failed: " + handoffValidation.Status);
        }

        Emit(serial, events, BootSequenceHandoffValidator.ReadyMarker);

        BootMemoryMapAcceptanceResult memoryMapAcceptance = AcceptNormalizedMemoryMap(normalizedMemoryMap);
        if (!memoryMapAcceptance.Succeeded)
        {
            Emit(serial, events, CreateMemoryMapRejectedMarker(memoryMapAcceptance.Status));
            return BootResult.Fail("boot memory map rejected: " + memoryMapAcceptance.Status);
        }

        Emit(serial, events, BootMemoryMapAccepted);
        EmitImplementedPhases(serial, events);

        if (IsUefiGoneSimulationProbeEnabled())
        {
            FirmwareServicesGuard firmware = new FirmwareServicesGuard();
            BootResult simulationResult = RunUefiGoneSimulation(firmware, serial, events);
            if (!simulationResult.Succeeded)
            {
                return simulationResult;
            }
        }

        if (Probes.EmitBootSmokeMarkers)
        {
            for (int lineIndex = BootSequencePlan.NonSmokeLineCount; lineIndex < BootSequencePlan.LineCount; lineIndex++)
            {
                Emit(serial, events, CreatePlanLine(lineIndex));
            }
        }

        return BootResult.Pass();
    }

    public static BootSequenceHandoffValidationResult ValidateHandoff(BootHandoffRecord handoffRecord)
    {
        return BootSequenceHandoffValidator.Validate(handoffRecord);
    }

    public static BootMemoryMapAcceptanceResult AcceptNormalizedMemoryMap(EfiMemoryMapSummary summary)
    {
        return BootMemoryMapAcceptance.Validate(summary);
    }

    public static BootResult RunFaultInfrastructureReadiness(
        FaultInfrastructureReadinessResult readiness,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!readiness.Succeeded)
        {
            Emit(serial, events, CreateFaultInfrastructureNotReadyMarker(readiness.Status));
            return BootResult.Fail("fault infrastructure not ready: " + readiness.Status);
        }

        Emit(serial, events, FaultInfrastructureReady);
        return BootResult.Pass();
    }

    public static BootResult RunExitBootServicesTransition(
        UefiExitBootServicesPlanResult plan,
        FirmwareServicesGuard firmware,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (firmware is null)
        {
            return BootResult.Fail("firmware guard is required");
        }

        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (plan.Status != UefiExitBootServicesPlanStatus.Success)
        {
            Emit(serial, events, CreateExitBootServicesTransitionBlockedMarker(plan.Status));
            return BootResult.Fail("ExitBootServices transition blocked: " + plan.Status);
        }

        FirmwareCallResult exited = firmware.MarkBootServicesExited();
        if (!exited.Succeeded)
        {
            return BootResult.Fail("firmware guard rejected ExitBootServices transition");
        }

        Emit(serial, events, ExitBootServicesTransitionReady);
        return BootResult.Pass();
    }

    public static BootResult RunExitBootServicesPreflight(
        SerialDiagnosticsGuardResult postFirmwareDiagnostics,
        UefiExitBootServicesPlanResult plan,
        FirmwareServicesGuard firmware,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (firmware is null)
        {
            return BootResult.Fail("firmware guard is required");
        }

        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!postFirmwareDiagnostics.PostFirmwareDiagnosticsAvailable ||
            plan.Status != UefiExitBootServicesPlanStatus.Success)
        {
            Emit(
                serial,
                events,
                CreateExitBootServicesPreflightBlockedMarker(
                    postFirmwareDiagnostics.Status,
                    plan.Status));
            return BootResult.Fail(
                "ExitBootServices preflight blocked: diagnostics=" + postFirmwareDiagnostics.Status
                + " plan=" + plan.Status);
        }

        FirmwareCallResult exited = firmware.MarkBootServicesExited();
        if (!exited.Succeeded)
        {
            return BootResult.Fail("firmware guard rejected ExitBootServices preflight transition");
        }

        Emit(serial, events, ExitBootServicesPreflightReady);
        return BootResult.Pass();
    }

    public static BootResult RunEarlyDiagnosticsReadiness(
        Com1PortIoBackendPlanResult serialBackendReadiness,
        DescriptorTableInstallReadinessResult descriptorTableReadiness,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!serialBackendReadiness.Succeeded)
        {
            Emit(
                serial,
                events,
                CreateEarlyDiagnosticsNotReadyMarker(EarlyDiagnosticsReadinessStatus.SerialBackendNotReady));
            return BootResult.Fail(
                "early diagnostics not ready: " + EarlyDiagnosticsReadinessStatus.SerialBackendNotReady);
        }

        if (!descriptorTableReadiness.Succeeded)
        {
            Emit(
                serial,
                events,
                CreateEarlyDiagnosticsNotReadyMarker(EarlyDiagnosticsReadinessStatus.DescriptorTablesNotReady));
            return BootResult.Fail(
                "early diagnostics not ready: " + EarlyDiagnosticsReadinessStatus.DescriptorTablesNotReady);
        }

        Emit(serial, events, EarlyDiagnosticsReady);
        return BootResult.Pass();
    }

    public static BootResult RunDiagnosticStubMaterializationReadiness(
        ExecutableStubBundleMaterializerResult materializerResult,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!materializerResult.Succeeded)
        {
            DiagnosticStubMaterializationReadinessStatus status =
                DiagnosticStubMaterializationReadinessStatus.MaterializerFailed;
            Emit(serial, events, CreateDiagnosticStubsNotMaterializedMarker(status));
            return BootResult.Fail("diagnostic stubs not materialized: " + status);
        }

        if (materializerResult.BytesCopied == 0)
        {
            DiagnosticStubMaterializationReadinessStatus status =
                DiagnosticStubMaterializationReadinessStatus.NoMaterializedPayloadEvidence;
            Emit(serial, events, CreateDiagnosticStubsNotMaterializedMarker(status));
            return BootResult.Fail("diagnostic stubs not materialized: " + status);
        }

        Emit(serial, events, DiagnosticStubsMaterialized);
        return BootResult.Pass();
    }

    public static BootResult RunPostFirmwareDiagnosticsReadiness(
        SerialDiagnosticsGuardResult diagnosticsReadiness,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!diagnosticsReadiness.PostFirmwareDiagnosticsAvailable)
        {
            Emit(serial, events, CreatePostFirmwareDiagnosticsBlockedMarker(diagnosticsReadiness.Status));
            return BootResult.Fail("post-firmware diagnostics blocked: " + diagnosticsReadiness.Status);
        }

        Emit(serial, events, PostFirmwareDiagnosticsReady);
        return BootResult.Pass();
    }

    public static BootResult RunControlledFaultProbeReadiness(
        EarlyDiagnosticsReadinessStatus earlyDiagnosticsStatus,
        FaultProbeReadinessResult faultProbeReadiness,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (earlyDiagnosticsStatus != EarlyDiagnosticsReadinessStatus.Ready)
        {
            ControlledFaultProbeReadinessStatus status =
                ControlledFaultProbeReadinessStatus.EarlyDiagnosticsNotReady;
            Emit(serial, events, CreateControlledFaultProbesNotReadyMarker(status));
            return BootResult.Fail("controlled fault probes not ready: " + status);
        }

        if (!faultProbeReadiness.Succeeded ||
            faultProbeReadiness.Status != FaultProbeReadinessStatus.Ready)
        {
            ControlledFaultProbeReadinessStatus status =
                ControlledFaultProbeReadinessStatus.FaultProbePlanNotReady;
            Emit(serial, events, CreateControlledFaultProbesNotReadyMarker(status));
            return BootResult.Fail("controlled fault probes not ready: " + status);
        }

        Emit(serial, events, ControlledFaultProbesReady);
        return BootResult.Pass();
    }

    public static BootResult RunCpuFaultPanicReadiness(
        CpuFaultPanicReadinessResult readiness,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!readiness.Succeeded)
        {
            CpuFaultPanicBootReadinessStatus status =
                CpuFaultPanicBootReadinessStatus.PanicReadinessFailed;
            Emit(serial, events, CreateCpuFaultPanicNotReadyMarker(status, readiness));
            return BootResult.Fail(
                "CPU fault panic not ready: status=" + status
                + " readiness=" + readiness.Status
                + " frame=" + readiness.FrameStatus
                + " reporter=" + readiness.ReporterStatus);
        }

        Emit(serial, events, CpuFaultPanicReady);
        return BootResult.Pass();
    }

    public static BootResult RunDiagnosticsChainReadiness(
        DiagnosticsChainReadinessResult readiness,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!readiness.Succeeded)
        {
            string marker = CreateDiagnosticsChainNotReadyMarker(readiness);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "diagnostics chain not ready: status=" + readiness.Status
                + " serial=" + readiness.SerialBackendStatus
                + " descriptor=" + readiness.DescriptorTableStatus
                + " probe=" + readiness.FaultProbeStatus
                + " panic=" + readiness.CpuFaultPanicStatus
                + " materializer=" + readiness.StubMaterializationStatus);
        }

        Emit(serial, events, DiagnosticsChainReady);
        return BootResult.Pass();
    }

    public static BootResult RunDiagnosticStubInvocationReadiness(
        DiagnosticStubInvocationReadinessResult readiness,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!readiness.Succeeded)
        {
            string marker = CreateDiagnosticStubInvocationNotReadyMarker(readiness);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "diagnostic stub invocation not ready: status=" + readiness.Status
                + " pageTransition=" + readiness.PageTransitionStatus
                + " backend=" + readiness.BackendContractStatus
                + " bytes=" + readiness.BytesMaterialized);
        }

        Emit(serial, events, DiagnosticStubInvocationReady);
        return BootResult.Pass();
    }

    public static BootResult RunBootPageAllocatorReadiness(
        BootPageAllocatorStateInitResult allocatorState,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        BootPageAllocatorReadinessStatus readiness = GetBootPageAllocatorReadinessStatus(allocatorState);
        if (readiness != BootPageAllocatorReadinessStatus.Ready)
        {
            string marker = CreateBootPageAllocatorNotReadyMarker(readiness, allocatorState);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "boot page allocator not ready: status=" + readiness
                + " state=" + allocatorState.Status
                + " freeList=" + allocatorState.FreeListPlanStatus);
        }

        Emit(serial, events, CreateBootPageAllocatorReadyMarker(allocatorState));
        return BootResult.Pass();
    }

    public static BootResult RunAppPackageReadiness(
        NativeManifestLoadResult manifestLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!manifestLoad.Succeeded || !packageParse.Succeeded || !launchPlan.Succeeded)
        {
            string marker = CreateAppPackageNotReadyMarker(
                manifestLoad.Status,
                packageParse.Status,
                launchPlan.Status);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app package not ready: manifest=" + manifestLoad.Status
                + " package=" + packageParse.Status
                + " launch=" + launchPlan.Status);
        }

        Emit(serial, events, AppPackageReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppLaunchDescriptorReadiness(
        NativeAppLaunchDescriptorResult descriptor,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!descriptor.Succeeded)
        {
            string marker = CreateAppLaunchNotReadyMarker(
                descriptor.Status,
                descriptor.LaunchStatus,
                descriptor.PackageFileStatus,
                descriptor.PackageStatus);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app launch descriptor not ready: status=" + descriptor.Status
                + " launch=" + descriptor.LaunchStatus
                + " packageFile=" + descriptor.PackageFileStatus
                + " package=" + descriptor.PackageStatus);
        }

        Emit(serial, events, AppLaunchReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppPayloadReadiness(
        NativeAppLaunchDescriptorResult descriptor,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!descriptor.Succeeded)
        {
            string marker = CreateAppPayloadNotReadyMarker(
                descriptor.Status,
                descriptor.PayloadHeaderStatus);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app payload not ready: descriptor=" + descriptor.Status
                + " payload=" + descriptor.PayloadHeaderStatus);
        }

        Emit(serial, events, AppPayloadReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppExecutableMappingReadiness(
        NativeAppPayloadMappingPlanResult mappingPlan,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!mappingPlan.Succeeded)
        {
            string marker = CreateAppExecutableMappingNotReadyMarker(
                mappingPlan.Status,
                mappingPlan.DescriptorStatus,
                mappingPlan.PayloadHeaderStatus);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app executable mapping not ready: status=" + mappingPlan.Status
                + " descriptor=" + mappingPlan.DescriptorStatus
                + " payload=" + mappingPlan.PayloadHeaderStatus);
        }

        Emit(serial, events, AppExecutableMappingReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppExecutableMemoryReadiness(
        NativeAppExecutableMemoryPlanResult memoryPlan,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!memoryPlan.Succeeded)
        {
            string marker = CreateAppExecutableMemoryNotReadyMarker(
                memoryPlan.Status,
                memoryPlan.PayloadMappingStatus,
                memoryPlan.DescriptorStatus,
                memoryPlan.PayloadHeaderStatus);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app executable memory not ready: status=" + memoryPlan.Status
                + " mapping=" + memoryPlan.PayloadMappingStatus
                + " descriptor=" + memoryPlan.DescriptorStatus
                + " payload=" + memoryPlan.PayloadHeaderStatus);
        }

        Emit(serial, events, AppExecutableMemoryReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppInvocationReadiness(
        NativeAppInvocationPlanResult invocationPlan,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!invocationPlan.Succeeded)
        {
            string marker = CreateAppInvocationNotReadyMarker(
                invocationPlan.Status,
                invocationPlan.ExecutableMemoryStatus,
                invocationPlan.LaunchStatus);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app invocation not ready: status=" + invocationPlan.Status
                + " memory=" + invocationPlan.ExecutableMemoryStatus
                + " launch=" + invocationPlan.LaunchStatus);
        }

        Emit(serial, events, AppInvocationReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppLifecycleReadiness(
        NativeAppLifecyclePlanResult lifecyclePlan,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!lifecyclePlan.Succeeded)
        {
            string marker = CreateAppLifecycleNotReadyMarker(
                lifecyclePlan.Status,
                lifecyclePlan.InvocationStatus,
                lifecyclePlan.LogStatus);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app lifecycle not ready: status=" + lifecyclePlan.Status
                + " invocation=" + lifecyclePlan.InvocationStatus
                + " log=" + lifecyclePlan.LogStatus);
        }

        Emit(serial, events, AppLifecycleReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppEntryReadiness(
        NativeManifestLoadResult manifestLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor,
        NativeAppEntryStagingPreflightResult stagingPreflight,
        NativeAppPayloadMappingPlanResult mappingPlan,
        NativeAppExecutableMemoryPlanResult memoryPlan,
        NativeAppInvocationPlanResult invocationPlan,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        AppEntryReadinessStatus entryStatus = GetAppEntryStatus(
            manifestLoad,
            packageParse,
            launchPlan,
            descriptor,
            stagingPreflight,
            mappingPlan,
            memoryPlan,
            invocationPlan);
        if (entryStatus != AppEntryReadinessStatus.Ready)
        {
            string marker = CreateAppEntryNotReadyMarker(
                entryStatus,
                manifestLoad.Status,
                packageParse.Status,
                launchPlan.Status,
                descriptor.Status,
                stagingPreflight.Status,
                stagingPreflight.PackageFileStagingStatus,
                stagingPreflight.PayloadStagingStatus,
                mappingPlan.Status,
                memoryPlan.Status,
                invocationPlan.Status);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app entry not ready: status=" + entryStatus
                + " manifest=" + manifestLoad.Status
                + " package=" + packageParse.Status
                + " launch=" + launchPlan.Status
                + " descriptor=" + descriptor.Status
                + " staging=" + stagingPreflight.Status
                + " packageStaging=" + stagingPreflight.PackageFileStagingStatus
                + " payloadStaging=" + stagingPreflight.PayloadStagingStatus
                + " mapping=" + mappingPlan.Status
                + " memory=" + memoryPlan.Status
                + " invocation=" + invocationPlan.Status);
        }

        Emit(serial, events, AppEntryReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppPackageStagingReadiness(
        NativeAppPackageStagingReadinessResult readiness,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        if (!readiness.Succeeded)
        {
            AppPackageStagingReadinessStatus status =
                AppPackageStagingReadinessStatus.StagingReadinessFailed;
            Emit(serial, events, CreateAppPackageStagingNotReadyMarker(status, readiness));
            return BootResult.Fail(
                "app package staging not ready: status=" + status
                + " readiness=" + readiness.Status
                + " manifest=" + readiness.ManifestStatus
                + " package=" + readiness.PackageStatus
                + " launch=" + readiness.LaunchStatus
                + " descriptor=" + readiness.DescriptorStatus);
        }

        Emit(serial, events, AppPackageStagingReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppPackageFileStagingPreflightReadiness(
        VirtioBlockPackageFileStagingPreflightResult preflight,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        AppPackageFileStagingPreflightReadinessStatus readiness =
            GetAppPackageFileStagingPreflightStatus(preflight);
        if (readiness != AppPackageFileStagingPreflightReadinessStatus.Ready)
        {
            string marker = CreateAppPackageFileStagingPreflightNotReadyMarker(readiness, preflight);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app package file staging preflight not ready: status=" + readiness
                + " preflight=" + preflight.Status
                + " packagePreflight=" + preflight.PackagePreflightStatus
                + " open=" + preflight.PackageOpenStatus
                + " staging=" + preflight.PackageStagingStatus);
        }

        Emit(serial, events, CreateAppPackageFileStagingPreflightReadyMarker(preflight));
        return BootResult.Pass();
    }

    public static BootResult RunPalExportCatalogReadiness(
        OSHostExportCatalogValidationResult validation,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        PalExportCatalogReadinessStatus readiness = GetPalExportCatalogReadinessStatus(validation);
        if (readiness != PalExportCatalogReadinessStatus.Ready)
        {
            string marker = CreatePalExportCatalogNotReadyMarker(readiness, validation);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "pal export catalog not ready: status=" + readiness
                + " catalog=" + validation.Status
                + " entryCount=" + validation.EntryCount
                + " firstFailingIndex=" + validation.EntryIndex
                + " duplicateIndex=" + validation.DuplicateEntryIndex);
        }

        Emit(serial, events, CreatePalExportCatalogReadyMarker(validation));
        return BootResult.Pass();
    }

    public static BootResult RunNativeAotHeapBootstrapReadiness(
        NativeAotHeapBootstrapPlanResult plan,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        NativeAotHeapBootstrapReadinessStatus readiness =
            GetNativeAotHeapBootstrapReadinessStatus(plan);
        if (readiness != NativeAotHeapBootstrapReadinessStatus.Ready)
        {
            string marker = CreateNativeAotHeapBootstrapNotReadyMarker(readiness, plan);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "nativeaot heap bootstrap not ready: status=" + readiness
                + " plan=" + plan.Status
                + " initialized=" + plan.IsInitialized
                + " requestedHeapPages=" + plan.RequestedHeapPages
                + " allocatorFreePages=" + plan.AllocatorFreePages);
        }

        Emit(serial, events, CreateNativeAotHeapBootstrapReadyMarker(plan));
        return BootResult.Pass();
    }

    public static BootResult RunNativeAotClassConstructorRunnerPreflightReadiness(
        NativeAotClassConstructorRunnerPreflightResult preflight,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        NativeAotClassConstructorRunnerPreflightReadinessStatus readiness =
            GetNativeAotClassConstructorRunnerPreflightReadinessStatus(preflight);
        if (readiness != NativeAotClassConstructorRunnerPreflightReadinessStatus.Ready)
        {
            string marker = CreateNativeAotClassConstructorRunnerPreflightNotReadyMarker(
                readiness,
                preflight);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "nativeaot cctor runner preflight not ready: status=" + readiness
                + " preflight=" + preflight.Status
                + " initialized=" + preflight.IsInitialized
                + " typeCount=" + preflight.TypeCount
                + " targetMetadataCount=" + preflight.TargetMetadataCount);
        }

        Emit(serial, events, CreateNativeAotClassConstructorRunnerPreflightReadyMarker(preflight));
        return BootResult.Pass();
    }

    public static BootResult RunNativeAotClassConstructorWorkItemReadiness(
        NativeAotClassConstructorWorkItemPlanResult plan,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        NativeAotClassConstructorWorkItemReadinessStatus readiness =
            GetNativeAotClassConstructorWorkItemReadinessStatus(plan);
        if (readiness != NativeAotClassConstructorWorkItemReadinessStatus.Ready)
        {
            string marker = CreateNativeAotClassConstructorWorkItemNotReadyMarker(
                readiness,
                plan);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "nativeaot cctor work items not ready: status=" + readiness
                + " plan=" + plan.Status
                + " initialized=" + plan.IsInitialized
                + " preflight=" + plan.PreflightStatus
                + " plannedCount=" + plan.PlannedCount);
        }

        Emit(serial, events, CreateNativeAotClassConstructorWorkItemReadyMarker(plan));
        return BootResult.Pass();
    }

    public static BootResult RunFetchAppHttpRequestEndpointReadiness(
        FetchAppHttpRequestEndpointResult endpoint,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        FetchAppHttpRequestEndpointReadinessStatus readiness =
            GetFetchAppHttpRequestEndpointReadinessStatus(endpoint);
        if (readiness != FetchAppHttpRequestEndpointReadinessStatus.Ready)
        {
            string marker = CreateFetchAppHttpRequestEndpointNotReadyMarker(readiness, endpoint);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "fetch app http request endpoint not ready: status=" + readiness
                + " endpoint=" + endpoint.Status
                + " initialized=" + endpoint.IsInitialized
                + " port=" + endpoint.Port);
        }

        Emit(serial, events, CreateFetchAppHttpRequestEndpointReadyMarker(endpoint));
        return BootResult.Pass();
    }

    public static BootResult RunFetchAppHttpCompletionReadiness(
        FetchAppHttpCompletionPlanResult completion,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        FetchAppHttpCompletionReadinessStatus readiness =
            GetFetchAppHttpCompletionReadinessStatus(completion);
        if (readiness != FetchAppHttpCompletionReadinessStatus.Ready)
        {
            string marker = CreateFetchAppHttpCompletionNotReadyMarker(readiness, completion);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "fetch app http completion not ready: status=" + readiness
                + " completion=" + completion.Status
                + " httpPlan=" + completion.HttpPlanStatus
                + " httpStatus=" + completion.HttpStatusCode
                + " expectedStatus=" + completion.ExpectedHttpStatusCode);
        }

        Emit(serial, events, CreateFetchAppHttpCompletionReadyMarker(completion));
        return BootResult.Pass();
    }

    public static BootResult RunFetchAppHttpTcpExchangeReadiness(
        FetchAppHttpTcpExchangePlanResult exchange,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        FetchAppHttpTcpExchangePlanDiagnosticFormatResult format =
            TryFormatFetchAppHttpTcpExchangeDiagnostic(exchange, out string diagnosticPayload);
        FetchAppHttpTcpExchangeReadinessStatus readiness =
            GetFetchAppHttpTcpExchangeReadinessStatus(exchange, format);
        if (readiness != FetchAppHttpTcpExchangeReadinessStatus.Ready)
        {
            string marker = CreateFetchAppHttpTcpExchangeNotReadyMarker(readiness, exchange, format);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "fetch app http tcp exchange not ready: status=" + readiness
                + " exchange=" + exchange.Status
                + " format=" + format.Status
                + " initialized=" + exchange.IsInitialized);
        }

        Emit(serial, events, FetchAppHttpTcpExchangeReadyPrefix + diagnosticPayload);
        return BootResult.Pass();
    }

    public static BootResult RunVirtioNetRxTcpDispatchReadiness(
        VirtioNetReceiveRegisterWindowTcpDispatchResult dispatch,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult format =
            TryFormatVirtioNetRxTcpDispatchDiagnostic(dispatch, out string diagnosticPayload);
        VirtioNetRxTcpDispatchReadinessStatus readiness =
            GetVirtioNetRxTcpDispatchReadinessStatus(dispatch, format);
        if (readiness != VirtioNetRxTcpDispatchReadinessStatus.Ready)
        {
            string marker = CreateVirtioNetRxTcpDispatchNotReadyMarker(readiness, dispatch, format);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "virtio-net rx tcp dispatch not ready: status=" + readiness
                + " dispatch=" + dispatch.Status
                + " format=" + format.Status
                + " initialized=" + dispatch.IsInitialized);
        }

        Emit(serial, events, VirtioNetRxTcpDispatchReady);
        Emit(serial, events, diagnosticPayload);
        return BootResult.Pass();
    }

    public static BootResult RunHttpStaticTcpResponseReadiness(
        HttpStaticTcpResponsePlanResult response,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        HttpStaticTcpResponsePlanDiagnosticFormatResult format =
            TryFormatHttpStaticTcpResponseDiagnostic(response, out string diagnosticPayload);
        HttpStaticTcpResponseReadinessStatus readiness =
            GetHttpStaticTcpResponseReadinessStatus(response, format);
        if (readiness != HttpStaticTcpResponseReadinessStatus.Ready)
        {
            string marker = CreateHttpStaticTcpResponseNotReadyMarker(readiness, response, format);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "http static tcp response not ready: status=" + readiness
                + " response=" + response.Status
                + " format=" + format.Status
                + " initialized=" + response.IsInitialized);
        }

        Emit(serial, events, diagnosticPayload);
        Emit(serial, events, HttpStaticTcpResponseReady);
        return BootResult.Pass();
    }

    public static BootResult RunNativeManifestFetchAppConfigPreflightReadiness(
        NativeManifestFetchAppConfigPreflightResult preflight,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        NativeManifestFetchAppConfigPreflightReadinessStatus readiness =
            GetNativeManifestFetchAppConfigPreflightReadinessStatus(preflight);
        if (readiness != NativeManifestFetchAppConfigPreflightReadinessStatus.Ready)
        {
            string marker = CreateNativeManifestFetchAppConfigPreflightNotReadyMarker(
                readiness,
                preflight);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "fetchapp config preflight not ready: status=" + readiness
                + " preflight=" + preflight.Status
                + " initialized=" + preflight.IsInitialized
                + " manifestLoad=" + preflight.ManifestLoadStatus
                + " manifestParse=" + preflight.ManifestParseStatus
                + " expectation=" + preflight.ExpectationStatus);
        }

        Emit(serial, events, CreateNativeManifestFetchAppConfigPreflightReadyMarker(preflight));
        return BootResult.Pass();
    }

    public static BootResult RunNativeManifestEnvironmentReadiness(
        NativeManifestParseResult manifest,
        ReadOnlySpan<byte> source,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        NativeManifestEnvironmentDiagnosticFormatResult format =
            TryFormatNativeManifestEnvironmentDiagnostic(manifest, source, out string diagnosticPayload);
        NativeManifestEnvironmentReadinessStatus readiness =
            GetNativeManifestEnvironmentReadinessStatus(manifest, format);
        if (readiness != NativeManifestEnvironmentReadinessStatus.Ready)
        {
            string marker = CreateNativeManifestEnvironmentNotReadyMarker(
                readiness,
                manifest,
                format);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "native manifest environment not ready: status=" + readiness
                + " parse=" + manifest.Status
                + " format=" + format.Status
                + " initialized=" + manifest.IsInitialized);
        }

        Emit(serial, events, NativeManifestEnvironmentReadyPrefix + diagnosticPayload);
        return BootResult.Pass();
    }

    public static BootResult RunNativeAppExitObserverReadiness(
        NativeAppExitLogObserverResult observer,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        AppExitObserverReadinessStatus readiness = GetAppExitObserverReadinessStatus(observer);
        if (readiness != AppExitObserverReadinessStatus.Ready)
        {
            string marker = CreateAppExitObserverNotReadyMarker(readiness, observer);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app exit observer not ready: status=" + readiness
                + " observer=" + observer.Status
                + " initialized=" + observer.IsInitialized
                + " recordsScanned=" + observer.RecordsScanned
                + " recordIndex=" + observer.RecordIndex);
        }

        Emit(serial, events, CreateAppExitObserverReadyMarker(observer));
        return BootResult.Pass();
    }

    public static BootResult RunNativeAppArgumentVectorReadiness(
        NativeAppArgumentVectorPlanResult argumentVector,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        NativeAppArgumentVectorReadinessStatus readiness =
            GetNativeAppArgumentVectorReadinessStatus(argumentVector);
        if (readiness != NativeAppArgumentVectorReadinessStatus.Ready)
        {
            string marker = CreateNativeAppArgumentVectorNotReadyMarker(readiness, argumentVector);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "native app argument vector not ready: status=" + readiness
                + " vector=" + argumentVector.Status
                + " launch=" + argumentVector.LaunchStatus
                + " initialized=" + argumentVector.IsInitialized
                + " argumentCount=" + argumentVector.ArgumentCount);
        }

        Emit(serial, events, CreateNativeAppArgumentVectorReadyMarker(argumentVector));
        return BootResult.Pass();
    }

    public static BootResult RunNativeAppArgumentHandoffReadiness(
        NativeAppArgumentHandoffPlanResult handoff,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        NativeAppArgumentHandoffReadinessStatus readiness =
            GetNativeAppArgumentHandoffReadinessStatus(handoff);
        if (readiness != NativeAppArgumentHandoffReadinessStatus.Ready)
        {
            string marker = CreateNativeAppArgumentHandoffNotReadyMarker(readiness, handoff);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "native app argument handoff not ready: status=" + readiness
                + " handoff=" + handoff.Status
                + " invocation=" + handoff.InvocationStatus
                + " argumentVector=" + handoff.ArgumentVectorStatus
                + " initialized=" + handoff.IsInitialized
                + " argumentCount=" + handoff.ArgumentCount
                + " parsedArgumentBytes=" + handoff.ParsedArgumentBytes
                + " rawArgumentBytes=" + handoff.RawLaunchArgumentByteLength);
        }

        Emit(serial, events, CreateNativeAppArgumentHandoffReadyMarker(handoff));
        return BootResult.Pass();
    }

    public static BootResult RunAppSelectionReadiness(
        NativeManifestLoadResult manifestLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        AppSelectionReadinessStatus selectionStatus = GetAppSelectionStatus(
            manifestLoad,
            packageParse,
            launchPlan,
            descriptor);
        if (selectionStatus != AppSelectionReadinessStatus.Ready)
        {
            string marker = CreateAppSelectionNotReadyMarker(
                selectionStatus,
                manifestLoad.Status,
                packageParse.Status,
                launchPlan.Status,
                descriptor.Status,
                descriptor.PackageFileStatus,
                descriptor.PackageStatus);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app selection not ready: status=" + selectionStatus
                + " manifest=" + manifestLoad.Status
                + " package=" + packageParse.Status
                + " launch=" + launchPlan.Status
                + " descriptor=" + descriptor.Status
                + " packageFile=" + descriptor.PackageFileStatus
                + " descriptorPackage=" + descriptor.PackageStatus);
        }

        Emit(serial, events, AppSelectionReady);
        return BootResult.Pass();
    }

    public static BootResult RunAppSelectionPreflightReadiness(
        VirtioBlockNativeAppSelectionPreflightResult preflight,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        AppSelectionPreflightReadinessStatus readiness = GetAppSelectionPreflightStatus(preflight);
        if (readiness != AppSelectionPreflightReadinessStatus.Ready)
        {
            string marker = CreateAppSelectionPreflightNotReadyMarker(readiness, preflight);
            Emit(serial, events, marker);
            return BootResult.Fail(
                "app selection preflight not ready: status=" + readiness
                + " preflight=" + preflight.Status
                + " readSession=" + preflight.ReadSessionStatus
                + " scratch=" + preflight.ScratchPlan.Status
                + " selection=" + preflight.SelectionLoad.Status);
        }

        Emit(serial, events, AppSelectionPreflightReady);
        return BootResult.Pass();
    }

    public static BootResult RunUefiGoneSimulation(
        FirmwareServicesGuard firmware,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        SerialDiagnosticsGuard diagnostics = new SerialDiagnosticsGuard();
        diagnostics.MarkOwnedSerialReady();
        SerialDiagnosticsGuardResult readiness =
            diagnostics.RequirePostFirmwareDiagnostics("uefi.gone.simulation.diagnostics");

        return RunUefiGoneSimulation(firmware, readiness, serial, events);
    }

    public static BootResult RunUefiGoneSimulation(
        FirmwareServicesGuard firmware,
        SerialDiagnosticsGuardResult postFirmwareDiagnostics,
        ISerialSink serial,
        BootEventRingBuffer events)
    {
        if (firmware is null)
        {
            return BootResult.Fail("firmware guard is required");
        }

        if (serial is null)
        {
            return BootResult.Fail("serial sink is required");
        }

        if (events is null)
        {
            return BootResult.Fail("boot event ring buffer is required");
        }

        Emit(serial, events, UefiGoneSimulationStart);

        FirmwareCallResult simulation = firmware.EnterUefiGoneSimulation();
        if (!simulation.Succeeded)
        {
            return FailUefiGoneSimulation(
                serial,
                events,
                "simulation-denied");
        }

        FirmwareCallResult denied = firmware.RequireBootServices("GetMemoryMap");
        if (denied.Succeeded)
        {
            return FailUefiGoneSimulation(
                serial,
                events,
                "boot-services-allowed-after-uefi-gone");
        }

        Emit(serial, events, UefiGoneSimulationPlatformDenied);

        BootResult diagnosticsResult = RunPostFirmwareDiagnosticsReadiness(
            postFirmwareDiagnostics,
            serial,
            events);
        if (!diagnosticsResult.Succeeded)
        {
            return diagnosticsResult;
        }

        Emit(serial, events, UefiGoneSimulationPass);
        return BootResult.Pass();
    }

    public static string CreatePlanLine(int lineIndex)
    {
        int lineLength = BootSequencePlan.GetLineLength(lineIndex);
        if (lineLength == 0)
        {
            return string.Empty;
        }

        return string.Create(
            lineLength,
            lineIndex,
            static (buffer, planLineIndex) =>
            {
                for (int offset = 0; offset < buffer.Length; offset++)
                {
                    buffer[offset] = (char)BootSequencePlan.GetLineByte(planLineIndex, offset);
                }
            });
    }

    public static string CreateM01StageOneSuccessLine(int lineIndex)
    {
        int lineLength =
            BootSequencePlan.GetM01StageOneSuccessLineLength(
                lineIndex);
        if (lineLength == 0)
        {
            return string.Empty;
        }

        return string.Create(
            lineLength,
            lineIndex,
            static (buffer, planLineIndex) =>
            {
                for (int offset = 0;
                    offset < buffer.Length;
                    offset++)
                {
                    buffer[offset] = (char)BootSequencePlan
                        .GetM01StageOneSuccessLineByte(
                            planLineIndex,
                            offset);
                }
            });
    }

    public static string CreateM01StageTwoSuccessLine(int lineIndex)
    {
        int lineLength =
            BootSequencePlan.GetM01StageTwoSuccessLineLength(
                lineIndex);
        if (lineLength == 0)
        {
            return string.Empty;
        }

        return string.Create(
            lineLength,
            lineIndex,
            static (buffer, planLineIndex) =>
            {
                for (int offset = 0;
                    offset < buffer.Length;
                    offset++)
                {
                    buffer[offset] = (char)BootSequencePlan
                        .GetM01StageTwoSuccessLineByte(
                            planLineIndex,
                            offset);
                }
            });
    }

    public static string CreateM01StageThreeSuccessLine(int lineIndex)
    {
        int lineLength =
            BootSequencePlan.GetM01StageThreeSuccessLineLength(
                lineIndex);
        if (lineLength == 0)
        {
            return string.Empty;
        }

        return string.Create(
            lineLength,
            lineIndex,
            static (buffer, planLineIndex) =>
            {
                for (int offset = 0;
                    offset < buffer.Length;
                    offset++)
                {
                    buffer[offset] = (char)BootSequencePlan
                        .GetM01StageThreeSuccessLineByte(
                            planLineIndex,
                            offset);
                }
            });
    }

    private static BootResult FailUefiGoneSimulation(
        ISerialSink serial,
        BootEventRingBuffer events,
        string reason)
    {
        string line = "UEFI_GONE_SIM_FAIL reason=" + reason;
        Emit(serial, events, line);
        return BootResult.Fail(reason);
    }

    public static string CreateMemoryMapRejectedMarker(BootMemoryMapAcceptanceStatus status)
    {
        return BootMemoryMapRejectedPrefix + status;
    }

    public static string CreateFaultInfrastructureNotReadyMarker(FaultInfrastructureReadinessStatus status)
    {
        return FaultInfrastructureNotReadyPrefix + status;
    }

    public static string CreateEarlyDiagnosticsNotReadyMarker(EarlyDiagnosticsReadinessStatus status)
    {
        return EarlyDiagnosticsNotReadyPrefix + status;
    }

    public static string CreateControlledFaultProbesNotReadyMarker(ControlledFaultProbeReadinessStatus status)
    {
        return ControlledFaultProbesNotReadyPrefix + status;
    }

    public static string CreateCpuFaultPanicNotReadyMarker(
        CpuFaultPanicBootReadinessStatus status,
        CpuFaultPanicReadinessResult readiness)
    {
        return CpuFaultPanicNotReadyPrefix
            + "status=" + status
            + " initialized=" + (readiness.IsInitialized ? "true" : "false")
            + " readiness=" + readiness.Status
            + " frame=" + readiness.FrameStatus
            + " reporter=" + readiness.ReporterStatus
            + " vector=" + readiness.Vector
            + " requiredFrameBytes=" + readiness.RequiredFrameBytes
            + " formatted=" + readiness.BytesFormatted
            + " flushed=" + readiness.BytesFlushed;
    }

    public static string CreateDiagnosticStubsNotMaterializedMarker(
        DiagnosticStubMaterializationReadinessStatus status)
    {
        return DiagnosticStubsNotMaterializedPrefix + status;
    }

    public static string CreateDiagnosticsChainNotReadyMarker(DiagnosticsChainReadinessResult readiness)
    {
        return DiagnosticsChainNotReadyPrefix
            + "status=" + readiness.Status
            + " initialized=" + (readiness.IsInitialized ? "true" : "false")
            + " serial=" + readiness.SerialBackendStatus
            + " descriptor=" + readiness.DescriptorTableStatus
            + " probe=" + readiness.FaultProbeStatus
            + " panic=" + readiness.CpuFaultPanicStatus
            + " materializer=" + readiness.StubMaterializationStatus
            + " stubBytes=" + readiness.StubMaterializationBytesCopied;
    }

    public static string CreateDiagnosticStubInvocationNotReadyMarker(
        DiagnosticStubInvocationReadinessResult readiness)
    {
        return DiagnosticStubInvocationNotReadyPrefix
            + "status=" + readiness.Status
            + " initialized=" + (readiness.IsInitialized ? "true" : "false")
            + " pageTransition=" + readiness.PageTransitionStatus
            + " backend=" + readiness.BackendContractStatus
            + " payloadBase=" + readiness.PayloadBaseAddress
            + " payloadEnd=" + readiness.PayloadEndAddress
            + " bytes=" + readiness.BytesMaterialized
            + " out8=" + readiness.Out8StubAddress
            + " in8=" + readiness.In8StubAddress;
    }

    public static string CreateBootPageAllocatorReadyMarker(BootPageAllocatorStateInitResult allocatorState)
    {
        return BootPageAllocatorReadyPrefix + CreateBootPageAllocatorMetadata(
            BootPageAllocatorReadinessStatus.Ready,
            allocatorState);
    }

    public static string CreateBootPageAllocatorNotReadyMarker(
        BootPageAllocatorReadinessStatus readiness,
        BootPageAllocatorStateInitResult allocatorState)
    {
        return BootPageAllocatorNotReadyPrefix + CreateBootPageAllocatorMetadata(readiness, allocatorState);
    }

    public static string CreatePostFirmwareDiagnosticsBlockedMarker(SerialDiagnosticsGuardStatus status)
    {
        return PostFirmwareDiagnosticsBlockedPrefix + status;
    }

    public static string CreateExitBootServicesPreflightBlockedMarker(
        SerialDiagnosticsGuardStatus diagnosticsStatus,
        UefiExitBootServicesPlanStatus planStatus)
    {
        return ExitBootServicesPreflightBlockedPrefix + diagnosticsStatus + " plan=" + planStatus;
    }

    public static string CreateExitBootServicesTransitionBlockedMarker(UefiExitBootServicesPlanStatus status)
    {
        return ExitBootServicesTransitionBlockedPrefix + status;
    }

    public static string CreateAppPackageNotReadyMarker(
        NativeManifestLoadStatus manifestStatus,
        OsPackageStatus packageStatus,
        NativeAppLaunchStatus launchStatus)
    {
        return AppPackageNotReadyPrefix
            + "manifest=" + manifestStatus
            + " package=" + packageStatus
            + " launch=" + launchStatus;
    }

    public static string CreateAppPackageStagingNotReadyMarker(
        AppPackageStagingReadinessStatus status,
        NativeAppPackageStagingReadinessResult readiness)
    {
        return AppPackageStagingNotReadyPrefix
            + "status=" + status
            + " initialized=" + (readiness.IsInitialized ? "true" : "false")
            + " readiness=" + readiness.Status
            + " manifest=" + readiness.ManifestStatus
            + " package=" + readiness.PackageStatus
            + " launch=" + readiness.LaunchStatus
            + " descriptor=" + readiness.DescriptorStatus
            + " packageFileStaging=" + readiness.PackageFileStagingStatus
            + " mapping=" + readiness.PayloadMappingStatus
            + " payloadStaging=" + readiness.PayloadStagingStatus
            + " memory=" + readiness.ExecutableMemoryStatus
            + " invocation=" + readiness.InvocationStatus
            + " fileIndex=" + readiness.AppBinaryFileIndex
            + " packageBytes=" + readiness.PackageFileBytesStaged
            + " payloadOffset=" + readiness.PayloadOffsetInPackage
            + " executableOffset=" + readiness.ExecutableOffsetInPackage
            + " executableBytes=" + readiness.PayloadExecutableBytesStaged
            + " writableStagingBytes=" + readiness.RequiredWritableStagingBytes
            + " stackBytes=" + readiness.RequiredStackBytes
            + " entryOffset=" + readiness.EntryOffsetInPackage;
    }

    public static string CreateAppPackageFileStagingPreflightReadyMarker(
        VirtioBlockPackageFileStagingPreflightResult preflight)
    {
        return AppPackageFileStagingPreflightReadyPrefix
            + CreateAppPackageFileStagingPreflightMetadata(
                AppPackageFileStagingPreflightReadinessStatus.Ready,
                preflight);
    }

    public static string CreateAppPackageFileStagingPreflightNotReadyMarker(
        AppPackageFileStagingPreflightReadinessStatus readiness,
        VirtioBlockPackageFileStagingPreflightResult preflight)
    {
        return AppPackageFileStagingPreflightNotReadyPrefix
            + CreateAppPackageFileStagingPreflightMetadata(readiness, preflight);
    }

    public static string CreatePalExportCatalogReadyMarker(
        OSHostExportCatalogValidationResult validation)
    {
        return PalExportCatalogReadyPrefix
            + CreatePalExportCatalogMetadata(PalExportCatalogReadinessStatus.Ready, validation);
    }

    public static string CreatePalExportCatalogNotReadyMarker(
        PalExportCatalogReadinessStatus readiness,
        OSHostExportCatalogValidationResult validation)
    {
        return PalExportCatalogNotReadyPrefix
            + CreatePalExportCatalogMetadata(readiness, validation);
    }

    public static string CreateNativeAotHeapBootstrapReadyMarker(
        NativeAotHeapBootstrapPlanResult plan)
    {
        return NativeAotHeapBootstrapReadyPrefix
            + CreateNativeAotHeapBootstrapMetadata(NativeAotHeapBootstrapReadinessStatus.Ready, plan);
    }

    public static string CreateNativeAotHeapBootstrapNotReadyMarker(
        NativeAotHeapBootstrapReadinessStatus readiness,
        NativeAotHeapBootstrapPlanResult plan)
    {
        return NativeAotHeapBootstrapNotReadyPrefix
            + CreateNativeAotHeapBootstrapMetadata(readiness, plan);
    }

    public static string CreateNativeAotClassConstructorRunnerPreflightReadyMarker(
        NativeAotClassConstructorRunnerPreflightResult preflight)
    {
        return NativeAotClassConstructorRunnerPreflightReadyPrefix
            + CreateNativeAotClassConstructorRunnerPreflightMetadata(
                NativeAotClassConstructorRunnerPreflightReadinessStatus.Ready,
                preflight);
    }

    public static string CreateNativeAotClassConstructorRunnerPreflightNotReadyMarker(
        NativeAotClassConstructorRunnerPreflightReadinessStatus readiness,
        NativeAotClassConstructorRunnerPreflightResult preflight)
    {
        return NativeAotClassConstructorRunnerPreflightNotReadyPrefix
            + CreateNativeAotClassConstructorRunnerPreflightMetadata(readiness, preflight);
    }

    public static string CreateNativeAotClassConstructorWorkItemReadyMarker(
        NativeAotClassConstructorWorkItemPlanResult plan)
    {
        return NativeAotClassConstructorWorkItemReadyPrefix
            + CreateNativeAotClassConstructorWorkItemMetadata(
                NativeAotClassConstructorWorkItemReadinessStatus.Ready,
                plan);
    }

    public static string CreateNativeAotClassConstructorWorkItemNotReadyMarker(
        NativeAotClassConstructorWorkItemReadinessStatus readiness,
        NativeAotClassConstructorWorkItemPlanResult plan)
    {
        return NativeAotClassConstructorWorkItemNotReadyPrefix
            + CreateNativeAotClassConstructorWorkItemMetadata(readiness, plan);
    }

    public static string CreateFetchAppHttpRequestEndpointReadyMarker(
        FetchAppHttpRequestEndpointResult endpoint)
    {
        return FetchAppHttpRequestEndpointReadyPrefix
            + CreateFetchAppHttpRequestEndpointMetadata(
                FetchAppHttpRequestEndpointReadinessStatus.Ready,
                endpoint);
    }

    public static string CreateFetchAppHttpRequestEndpointNotReadyMarker(
        FetchAppHttpRequestEndpointReadinessStatus readiness,
        FetchAppHttpRequestEndpointResult endpoint)
    {
        return FetchAppHttpRequestEndpointNotReadyPrefix
            + CreateFetchAppHttpRequestEndpointMetadata(readiness, endpoint);
    }

    public static string CreateFetchAppHttpCompletionReadyMarker(
        FetchAppHttpCompletionPlanResult completion)
    {
        return FetchAppHttpCompletionReadyPrefix
            + CreateFetchAppHttpCompletionMetadata(
                FetchAppHttpCompletionReadinessStatus.Ready,
                completion);
    }

    public static string CreateFetchAppHttpCompletionNotReadyMarker(
        FetchAppHttpCompletionReadinessStatus readiness,
        FetchAppHttpCompletionPlanResult completion)
    {
        return FetchAppHttpCompletionNotReadyPrefix
            + CreateFetchAppHttpCompletionMetadata(readiness, completion);
    }

    public static string CreateFetchAppHttpTcpExchangeReadyMarker(
        FetchAppHttpTcpExchangePlanResult exchange)
    {
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult format =
            TryFormatFetchAppHttpTcpExchangeDiagnostic(exchange, out string diagnosticPayload);
        FetchAppHttpTcpExchangeReadinessStatus readiness =
            GetFetchAppHttpTcpExchangeReadinessStatus(exchange, format);
        return readiness == FetchAppHttpTcpExchangeReadinessStatus.Ready
            ? FetchAppHttpTcpExchangeReadyPrefix + diagnosticPayload
            : CreateFetchAppHttpTcpExchangeNotReadyMarker(readiness, exchange, format);
    }

    public static string CreateFetchAppHttpTcpExchangeNotReadyMarker(
        FetchAppHttpTcpExchangeReadinessStatus readiness,
        FetchAppHttpTcpExchangePlanResult exchange)
    {
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult format =
            TryFormatFetchAppHttpTcpExchangeDiagnostic(exchange, out _);
        return CreateFetchAppHttpTcpExchangeNotReadyMarker(readiness, exchange, format);
    }

    public static string CreateFetchAppHttpTcpExchangeNotReadyMarker(
        FetchAppHttpTcpExchangeReadinessStatus readiness,
        FetchAppHttpTcpExchangePlanResult exchange,
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult format)
    {
        return FetchAppHttpTcpExchangeNotReadyPrefix
            + CreateFetchAppHttpTcpExchangeMetadata(readiness, exchange, format);
    }

    public static string CreateVirtioNetRxTcpDispatchNotReadyMarker(
        VirtioNetRxTcpDispatchReadinessStatus readiness,
        VirtioNetReceiveRegisterWindowTcpDispatchResult dispatch,
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult format)
    {
        return VirtioNetRxTcpDispatchNotReadyPrefix
            + CreateVirtioNetRxTcpDispatchMetadata(readiness, dispatch, format);
    }

    public static string CreateHttpStaticTcpResponseNotReadyMarker(
        HttpStaticTcpResponseReadinessStatus readiness,
        HttpStaticTcpResponsePlanResult response,
        HttpStaticTcpResponsePlanDiagnosticFormatResult format)
    {
        return HttpStaticTcpResponseNotReadyPrefix
            + CreateHttpStaticTcpResponseMetadata(readiness, response, format);
    }

    public static string CreateNativeManifestFetchAppConfigPreflightReadyMarker(
        NativeManifestFetchAppConfigPreflightResult preflight)
    {
        return NativeManifestFetchAppConfigPreflightReadyPrefix
            + CreateNativeManifestFetchAppConfigPreflightMetadata(
                NativeManifestFetchAppConfigPreflightReadinessStatus.Ready,
                preflight);
    }

    public static string CreateNativeManifestFetchAppConfigPreflightNotReadyMarker(
        NativeManifestFetchAppConfigPreflightReadinessStatus readiness,
        NativeManifestFetchAppConfigPreflightResult preflight)
    {
        return NativeManifestFetchAppConfigPreflightNotReadyPrefix
            + CreateNativeManifestFetchAppConfigPreflightMetadata(readiness, preflight);
    }

    public static string CreateNativeManifestEnvironmentNotReadyMarker(
        NativeManifestEnvironmentReadinessStatus readiness,
        NativeManifestParseResult manifest,
        NativeManifestEnvironmentDiagnosticFormatResult format)
    {
        return NativeManifestEnvironmentNotReadyPrefix
            + CreateNativeManifestEnvironmentMetadata(readiness, manifest, format);
    }

    public static string CreateAppExitObserverReadyMarker(NativeAppExitLogObserverResult observer)
    {
        return AppExitObserverReadyPrefix
            + CreateAppExitObserverMetadata(AppExitObserverReadinessStatus.Ready, observer);
    }

    public static string CreateAppExitObserverNotReadyMarker(
        AppExitObserverReadinessStatus readiness,
        NativeAppExitLogObserverResult observer)
    {
        return AppExitObserverNotReadyPrefix
            + CreateAppExitObserverMetadata(readiness, observer);
    }

    public static string CreateNativeAppArgumentVectorReadyMarker(
        NativeAppArgumentVectorPlanResult argumentVector)
    {
        return NativeAppArgumentVectorReadyPrefix
            + CreateNativeAppArgumentVectorMetadata(
                NativeAppArgumentVectorReadinessStatus.Ready,
                argumentVector);
    }

    public static string CreateNativeAppArgumentVectorNotReadyMarker(
        NativeAppArgumentVectorReadinessStatus readiness,
        NativeAppArgumentVectorPlanResult argumentVector)
    {
        return NativeAppArgumentVectorNotReadyPrefix
            + CreateNativeAppArgumentVectorMetadata(readiness, argumentVector);
    }

    public static string CreateNativeAppArgumentHandoffReadyMarker(
        NativeAppArgumentHandoffPlanResult handoff)
    {
        return NativeAppArgumentHandoffReadyPrefix
            + CreateNativeAppArgumentHandoffMetadata(
                NativeAppArgumentHandoffReadinessStatus.Ready,
                handoff);
    }

    public static string CreateNativeAppArgumentHandoffNotReadyMarker(
        NativeAppArgumentHandoffReadinessStatus readiness,
        NativeAppArgumentHandoffPlanResult handoff)
    {
        return NativeAppArgumentHandoffNotReadyPrefix
            + CreateNativeAppArgumentHandoffMetadata(readiness, handoff);
    }

    public static string CreateAppLaunchNotReadyMarker(
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppLaunchStatus launchStatus,
        OsPackageFileSystemStatus packageFileStatus,
        OsPackageStatus packageStatus)
    {
        return AppLaunchNotReadyPrefix
            + "status=" + descriptorStatus
            + " launch=" + launchStatus
            + " packageFile=" + packageFileStatus
            + " package=" + packageStatus;
    }

    public static string CreateAppPayloadNotReadyMarker(
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus)
    {
        return AppPayloadNotReadyPrefix
            + "descriptor=" + descriptorStatus
            + " payload=" + payloadHeaderStatus;
    }

    public static string CreateAppExecutableMappingNotReadyMarker(
        NativeAppPayloadMappingPlanStatus mappingStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus)
    {
        return AppExecutableMappingNotReadyPrefix
            + "status=" + mappingStatus
            + " descriptor=" + descriptorStatus
            + " payload=" + payloadHeaderStatus;
    }

    public static string CreateAppExecutableMemoryNotReadyMarker(
        NativeAppExecutableMemoryPlanStatus status,
        NativeAppPayloadMappingPlanStatus mappingStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus)
    {
        return AppExecutableMemoryNotReadyPrefix
            + "status=" + status
            + " mapping=" + mappingStatus
            + " descriptor=" + descriptorStatus
            + " payload=" + payloadHeaderStatus;
    }

    public static string CreateAppInvocationNotReadyMarker(
        NativeAppInvocationPlanStatus status,
        NativeAppExecutableMemoryPlanStatus executableMemoryStatus,
        NativeAppLaunchStatus launchStatus)
    {
        return AppInvocationNotReadyPrefix
            + "status=" + status
            + " memory=" + executableMemoryStatus
            + " launch=" + launchStatus;
    }

    public static string CreateAppLifecycleNotReadyMarker(
        NativeAppLifecyclePlanStatus status,
        NativeAppInvocationPlanStatus invocationStatus,
        OSHostStatus logStatus)
    {
        return AppLifecycleNotReadyPrefix
            + "status=" + status
            + " invocation=" + invocationStatus
            + " log=" + logStatus;
    }

    public static string CreateAppEntryNotReadyMarker(
        AppEntryReadinessStatus entryStatus,
        NativeManifestLoadStatus manifestStatus,
        OsPackageStatus packageStatus,
        NativeAppLaunchStatus launchStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppEntryStagingPreflightStatus stagingStatus,
        OsPackageFileStagingStatus packageFileStagingStatus,
        NativeAppPayloadStagingStatus payloadStagingStatus,
        NativeAppPayloadMappingPlanStatus mappingStatus,
        NativeAppExecutableMemoryPlanStatus memoryStatus,
        NativeAppInvocationPlanStatus invocationStatus)
    {
        return AppEntryNotReadyPrefix
            + "status=" + entryStatus
            + " manifest=" + manifestStatus
            + " package=" + packageStatus
            + " launch=" + launchStatus
            + " descriptor=" + descriptorStatus
            + " staging=" + stagingStatus
            + " packageStaging=" + packageFileStagingStatus
            + " payloadStaging=" + payloadStagingStatus
            + " mapping=" + mappingStatus
            + " memory=" + memoryStatus
            + " invocation=" + invocationStatus;
    }

    public static string CreateAppSelectionNotReadyMarker(
        AppSelectionReadinessStatus selectionStatus,
        NativeManifestLoadStatus manifestStatus,
        OsPackageStatus packageStatus,
        NativeAppLaunchStatus launchStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        OsPackageFileSystemStatus packageFileStatus,
        OsPackageStatus descriptorPackageStatus)
    {
        return AppSelectionNotReadyPrefix
            + "status=" + selectionStatus
            + " manifest=" + manifestStatus
            + " package=" + packageStatus
            + " launch=" + launchStatus
            + " descriptor=" + descriptorStatus
            + " packageFile=" + packageFileStatus
            + " descriptorPackage=" + descriptorPackageStatus;
    }

    public static string CreateAppSelectionPreflightNotReadyMarker(
        AppSelectionPreflightReadinessStatus readiness,
        VirtioBlockNativeAppSelectionPreflightResult preflight)
    {
        return AppSelectionPreflightNotReadyPrefix
            + "status=" + readiness
            + " initialized=" + (preflight.IsInitialized ? "true" : "false")
            + " preflight=" + preflight.Status
            + " readSession=" + preflight.ReadSessionStatus
            + " pipeline=" + preflight.ReadSessionPipelineStatus
            + " block=" + preflight.BlockDeviceStatus
            + " scratch=" + preflight.ScratchPlan.Status
            + " selection=" + preflight.SelectionLoad.Status
            + " manifest=" + preflight.SelectionLoad.ManifestLoad.Status
            + " packageLoad=" + preflight.SelectionLoad.PackageLoad.Status
            + " package=" + preflight.SelectionLoad.PackageParse.Status
            + " launch=" + preflight.SelectionLoad.LaunchPlan.Status
            + " descriptor=" + preflight.SelectionLoad.Descriptor.Status;
    }

    private static AppSelectionReadinessStatus GetAppSelectionStatus(
        NativeManifestLoadResult manifestLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor)
    {
        if (!manifestLoad.Succeeded)
        {
            return AppSelectionReadinessStatus.ManifestFailed;
        }

        if (!packageParse.Succeeded)
        {
            return AppSelectionReadinessStatus.PackageFailed;
        }

        if (!launchPlan.Succeeded)
        {
            return AppSelectionReadinessStatus.LaunchPlanFailed;
        }

        return descriptor.Succeeded
            ? AppSelectionReadinessStatus.Ready
            : AppSelectionReadinessStatus.LaunchDescriptorFailed;
    }

    private static AppPackageFileStagingPreflightReadinessStatus GetAppPackageFileStagingPreflightStatus(
        VirtioBlockPackageFileStagingPreflightResult preflight)
    {
        if (!preflight.IsInitialized)
        {
            return AppPackageFileStagingPreflightReadinessStatus.NotInitialized;
        }

        if (!preflight.PackagePreflight.Succeeded ||
            preflight.Status == VirtioBlockPackageFileStagingPreflightStatus.PackagePreflightFailed)
        {
            return AppPackageFileStagingPreflightReadinessStatus.PackagePreflightFailed;
        }

        if (!preflight.PackageOpen.Succeeded ||
            preflight.Status == VirtioBlockPackageFileStagingPreflightStatus.PackageOpenFailed)
        {
            return AppPackageFileStagingPreflightReadinessStatus.PackageOpenFailed;
        }

        if (!preflight.PackageStaging.Succeeded ||
            preflight.Status == VirtioBlockPackageFileStagingPreflightStatus.PackageStagingFailed)
        {
            return AppPackageFileStagingPreflightReadinessStatus.PackageStagingFailed;
        }

        return preflight.Succeeded
            ? AppPackageFileStagingPreflightReadinessStatus.Ready
            : AppPackageFileStagingPreflightReadinessStatus.PackageStagingFailed;
    }

    private static PalExportCatalogReadinessStatus GetPalExportCatalogReadinessStatus(
        OSHostExportCatalogValidationResult validation)
    {
        if (!validation.IsInitialized)
        {
            return PalExportCatalogReadinessStatus.NotInitialized;
        }

        return validation.Succeeded
            ? PalExportCatalogReadinessStatus.Ready
            : PalExportCatalogReadinessStatus.CatalogValidationFailed;
    }

    private static NativeAotHeapBootstrapReadinessStatus GetNativeAotHeapBootstrapReadinessStatus(
        NativeAotHeapBootstrapPlanResult plan)
    {
        if (!plan.IsInitialized)
        {
            return NativeAotHeapBootstrapReadinessStatus.NotInitialized;
        }

        return plan.Succeeded
            ? NativeAotHeapBootstrapReadinessStatus.Ready
            : NativeAotHeapBootstrapReadinessStatus.PlanFailed;
    }

    private static NativeAotClassConstructorRunnerPreflightReadinessStatus
        GetNativeAotClassConstructorRunnerPreflightReadinessStatus(
            NativeAotClassConstructorRunnerPreflightResult preflight)
    {
        if (!preflight.IsInitialized)
        {
            return NativeAotClassConstructorRunnerPreflightReadinessStatus.NotInitialized;
        }

        return preflight.Succeeded
            ? NativeAotClassConstructorRunnerPreflightReadinessStatus.Ready
            : NativeAotClassConstructorRunnerPreflightReadinessStatus.PreflightFailed;
    }

    private static NativeAotClassConstructorWorkItemReadinessStatus
        GetNativeAotClassConstructorWorkItemReadinessStatus(
            NativeAotClassConstructorWorkItemPlanResult plan)
    {
        if (!plan.IsInitialized)
        {
            return NativeAotClassConstructorWorkItemReadinessStatus.NotInitialized;
        }

        return plan.Succeeded
            ? NativeAotClassConstructorWorkItemReadinessStatus.Ready
            : NativeAotClassConstructorWorkItemReadinessStatus.PlanFailed;
    }

    private static FetchAppHttpRequestEndpointReadinessStatus
        GetFetchAppHttpRequestEndpointReadinessStatus(FetchAppHttpRequestEndpointResult endpoint)
    {
        if (!endpoint.IsInitialized)
        {
            return FetchAppHttpRequestEndpointReadinessStatus.NotInitialized;
        }

        return endpoint.Succeeded
            ? FetchAppHttpRequestEndpointReadinessStatus.Ready
            : FetchAppHttpRequestEndpointReadinessStatus.EndpointFailed;
    }

    private static FetchAppHttpCompletionReadinessStatus GetFetchAppHttpCompletionReadinessStatus(
        FetchAppHttpCompletionPlanResult completion)
    {
        if (!completion.IsInitialized)
        {
            return FetchAppHttpCompletionReadinessStatus.NotInitialized;
        }

        return completion.Succeeded
            ? FetchAppHttpCompletionReadinessStatus.Ready
            : FetchAppHttpCompletionReadinessStatus.CompletionFailed;
    }

    private static FetchAppHttpTcpExchangeReadinessStatus GetFetchAppHttpTcpExchangeReadinessStatus(
        FetchAppHttpTcpExchangePlanResult exchange,
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult format)
    {
        if (!exchange.IsInitialized)
        {
            return FetchAppHttpTcpExchangeReadinessStatus.NotInitialized;
        }

        if (!exchange.Succeeded)
        {
            return FetchAppHttpTcpExchangeReadinessStatus.ExchangeFailed;
        }

        return format.Succeeded
            ? FetchAppHttpTcpExchangeReadinessStatus.Ready
            : FetchAppHttpTcpExchangeReadinessStatus.DiagnosticFormattingFailure;
    }

    private static VirtioNetRxTcpDispatchReadinessStatus GetVirtioNetRxTcpDispatchReadinessStatus(
        VirtioNetReceiveRegisterWindowTcpDispatchResult dispatch,
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult format)
    {
        if (!dispatch.IsInitialized)
        {
            return VirtioNetRxTcpDispatchReadinessStatus.NotInitialized;
        }

        if (!dispatch.Succeeded)
        {
            return VirtioNetRxTcpDispatchReadinessStatus.DispatchFailed;
        }

        return format.Succeeded
            ? VirtioNetRxTcpDispatchReadinessStatus.Ready
            : VirtioNetRxTcpDispatchReadinessStatus.DiagnosticFormattingFailure;
    }

    private static HttpStaticTcpResponseReadinessStatus GetHttpStaticTcpResponseReadinessStatus(
        HttpStaticTcpResponsePlanResult response,
        HttpStaticTcpResponsePlanDiagnosticFormatResult format)
    {
        if (!response.IsInitialized)
        {
            return HttpStaticTcpResponseReadinessStatus.NotInitialized;
        }

        if (!response.Succeeded)
        {
            return HttpStaticTcpResponseReadinessStatus.ResponsePlanFailed;
        }

        return format.Succeeded
            ? HttpStaticTcpResponseReadinessStatus.Ready
            : HttpStaticTcpResponseReadinessStatus.DiagnosticFormattingFailure;
    }

    private static NativeManifestFetchAppConfigPreflightReadinessStatus
        GetNativeManifestFetchAppConfigPreflightReadinessStatus(
            NativeManifestFetchAppConfigPreflightResult preflight)
    {
        if (!preflight.IsInitialized)
        {
            return NativeManifestFetchAppConfigPreflightReadinessStatus.NotInitialized;
        }

        return preflight.Succeeded
            ? NativeManifestFetchAppConfigPreflightReadinessStatus.Ready
            : NativeManifestFetchAppConfigPreflightReadinessStatus.PreflightFailed;
    }

    private static NativeManifestEnvironmentReadinessStatus GetNativeManifestEnvironmentReadinessStatus(
        NativeManifestParseResult manifest,
        NativeManifestEnvironmentDiagnosticFormatResult format)
    {
        if (!manifest.IsInitialized)
        {
            return NativeManifestEnvironmentReadinessStatus.NotInitialized;
        }

        if (!manifest.Succeeded || manifest.Status != NativeManifestParseStatus.Success)
        {
            return NativeManifestEnvironmentReadinessStatus.ParseFailure;
        }

        if (format.Status == NativeManifestEnvironmentDiagnosticFormatStatus.SourceRangeInvalid)
        {
            return NativeManifestEnvironmentReadinessStatus.SourceRangeInvalid;
        }

        return format.Succeeded
            ? NativeManifestEnvironmentReadinessStatus.Ready
            : NativeManifestEnvironmentReadinessStatus.DiagnosticFormattingFailure;
    }

    private static AppExitObserverReadinessStatus GetAppExitObserverReadinessStatus(
        NativeAppExitLogObserverResult observer)
    {
        if (!observer.IsInitialized)
        {
            return AppExitObserverReadinessStatus.NotInitialized;
        }

        return observer.Succeeded
            ? AppExitObserverReadinessStatus.Ready
            : AppExitObserverReadinessStatus.ObserverFailure;
    }

    private static NativeAppArgumentVectorReadinessStatus GetNativeAppArgumentVectorReadinessStatus(
        NativeAppArgumentVectorPlanResult argumentVector)
    {
        if (!argumentVector.IsInitialized)
        {
            return NativeAppArgumentVectorReadinessStatus.NotInitialized;
        }

        return argumentVector.Succeeded
            ? NativeAppArgumentVectorReadinessStatus.Ready
            : NativeAppArgumentVectorReadinessStatus.ArgumentVectorFailed;
    }

    private static NativeAppArgumentHandoffReadinessStatus GetNativeAppArgumentHandoffReadinessStatus(
        NativeAppArgumentHandoffPlanResult handoff)
    {
        if (!handoff.IsInitialized)
        {
            return NativeAppArgumentHandoffReadinessStatus.NotInitialized;
        }

        return handoff.Succeeded
            ? NativeAppArgumentHandoffReadinessStatus.Ready
            : NativeAppArgumentHandoffReadinessStatus.HandoffFailed;
    }

    private static AppSelectionPreflightReadinessStatus GetAppSelectionPreflightStatus(
        VirtioBlockNativeAppSelectionPreflightResult preflight)
    {
        if (!preflight.IsInitialized)
        {
            return AppSelectionPreflightReadinessStatus.NotInitialized;
        }

        return preflight.Status switch
        {
            VirtioBlockNativeAppSelectionPreflightStatus.Success => AppSelectionPreflightReadinessStatus.Ready,
            VirtioBlockNativeAppSelectionPreflightStatus.IncompleteReadSession =>
                AppSelectionPreflightReadinessStatus.IncompleteReadSession,
            VirtioBlockNativeAppSelectionPreflightStatus.InvalidBlockDevice =>
                AppSelectionPreflightReadinessStatus.InvalidBlockDevice,
            VirtioBlockNativeAppSelectionPreflightStatus.ScratchPlanFailed =>
                AppSelectionPreflightReadinessStatus.ScratchPlanFailed,
            _ => AppSelectionPreflightReadinessStatus.SelectionLoadFailed
        };
    }

    private static AppEntryReadinessStatus GetAppEntryStatus(
        NativeManifestLoadResult manifestLoad,
        OsPackageParseResult packageParse,
        NativeAppLaunchPlanResult launchPlan,
        NativeAppLaunchDescriptorResult descriptor,
        NativeAppEntryStagingPreflightResult stagingPreflight,
        NativeAppPayloadMappingPlanResult mappingPlan,
        NativeAppExecutableMemoryPlanResult memoryPlan,
        NativeAppInvocationPlanResult invocationPlan)
    {
        AppSelectionReadinessStatus selectionStatus = GetAppSelectionStatus(
            manifestLoad,
            packageParse,
            launchPlan,
            descriptor);
        if (selectionStatus == AppSelectionReadinessStatus.ManifestFailed ||
            selectionStatus == AppSelectionReadinessStatus.PackageFailed ||
            selectionStatus == AppSelectionReadinessStatus.LaunchPlanFailed)
        {
            return AppEntryReadinessStatus.AppPackageFailed;
        }

        if (selectionStatus == AppSelectionReadinessStatus.LaunchDescriptorFailed)
        {
            return AppEntryReadinessStatus.AppSelectionFailed;
        }

        if (!stagingPreflight.Succeeded)
        {
            return AppEntryReadinessStatus.StagingPreflightFailed;
        }

        if (!mappingPlan.Succeeded)
        {
            return AppEntryReadinessStatus.PayloadMappingFailed;
        }

        if (!memoryPlan.Succeeded)
        {
            return AppEntryReadinessStatus.ExecutableMemoryFailed;
        }

        return invocationPlan.Succeeded
            ? AppEntryReadinessStatus.Ready
            : AppEntryReadinessStatus.InvocationPlanFailed;
    }

    private static BootPageAllocatorReadinessStatus GetBootPageAllocatorReadinessStatus(
        BootPageAllocatorStateInitResult allocatorState)
    {
        if (allocatorState.Status == BootPageAllocatorStateStatus.Success &&
            !allocatorState.State.IsInitialized)
        {
            return BootPageAllocatorReadinessStatus.StateNotInitialized;
        }

        if (allocatorState.Status == BootPageAllocatorStateStatus.NoFreeNodes)
        {
            return BootPageAllocatorReadinessStatus.NoFreeNodes;
        }

        if (allocatorState.Status == BootPageAllocatorStateStatus.NoFreePages)
        {
            return BootPageAllocatorReadinessStatus.NoFreePages;
        }

        if (!allocatorState.Succeeded)
        {
            return BootPageAllocatorReadinessStatus.StateInitializationFailed;
        }

        if (allocatorState.State.FreeNodeCount <= 0)
        {
            return BootPageAllocatorReadinessStatus.NoFreeNodes;
        }

        return allocatorState.State.FreePageCount == 0
            ? BootPageAllocatorReadinessStatus.NoFreePages
            : BootPageAllocatorReadinessStatus.Ready;
    }

    private static string CreateBootPageAllocatorMetadata(
        BootPageAllocatorReadinessStatus readiness,
        BootPageAllocatorStateInitResult allocatorState)
    {
        return "status=" + readiness
            + " state=" + allocatorState.Status
            + " freeList=" + allocatorState.FreeListPlanStatus
            + " requiredNodes=" + allocatorState.RequiredNodeCapacity
            + " availableNodes=" + allocatorState.AvailableNodeCapacity
            + " visitedNodes=" + allocatorState.VisitedNodeCount
            + " invalidNode=" + allocatorState.InvalidNodeIndex
            + " initialized=" + (allocatorState.State.IsInitialized ? "true" : "false")
            + " head=" + allocatorState.State.HeadNodeIndex
            + " freeNodes=" + allocatorState.State.FreeNodeCount
            + " freePages=" + allocatorState.State.FreePageCount
            + " freeBytes=" + allocatorState.State.FreeByteCount
            + " allocatedPages=" + allocatorState.State.AllocatedPageCount
            + " allocatedBytes=" + allocatorState.State.AllocatedByteCount
            + " totalFreePages=" + allocatorState.TotalFreePages
            + " totalFreeBytes=" + allocatorState.TotalFreeBytes
            + " pageSize=" + allocatorState.PageSize;
    }

    private static string CreateAppPackageFileStagingPreflightMetadata(
        AppPackageFileStagingPreflightReadinessStatus readiness,
        VirtioBlockPackageFileStagingPreflightResult preflight)
    {
        return "status=" + readiness
            + " initialized=" + (preflight.IsInitialized ? "true" : "false")
            + " succeeded=" + (preflight.Succeeded ? "true" : "false")
            + " preflight=" + preflight.Status
            + " readSession=" + preflight.ReadSessionStatus
            + " pipeline=" + preflight.ReadSessionPipelineStatus
            + " packagePreflight=" + preflight.PackagePreflightStatus
            + " packageLoad=" + preflight.PackagePreflight.PackageLoad.Status
            + " package=" + preflight.PackagePreflight.PackageLoad.PackageStatus
            + " open=" + preflight.PackageOpenStatus
            + " openPackage=" + preflight.PackageOpen.PackageStatus
            + " staging=" + preflight.PackageStagingStatus
            + " stagingOpen=" + preflight.PackageStaging.OpenStatus
            + " stagingPackage=" + preflight.PackageStaging.PackageStatus
            + " readBytes=" + preflight.ReadSessionByteCount
            + " sourcePackageLength=" + preflight.PackageBytesRead
            + " stagedBytes=" + preflight.PackageFileBytesStaged
            + " destinationCapacity=" + preflight.StagingDestinationBytesAvailable
            + " packageBufferCapacity=" + preflight.PackagePreflight.PackageBufferBytesAvailable
            + " stagingRequiredBytes=" + preflight.PackageStaging.RequiredBytes
            + " fileLength=" + preflight.PackageStaging.FileLength
            + " sourceOverflow=" + (preflight.PackageStaging.SourceRangeOverflow ? "true" : "false");
    }

    private static string CreatePalExportCatalogMetadata(
        PalExportCatalogReadinessStatus readiness,
        OSHostExportCatalogValidationResult validation)
    {
        int firstFailingIndex = validation.HasEntryMetadata ? validation.EntryIndex : -1;
        int duplicateIndex = validation.HasEntryMetadata && validation.DuplicateEntryIndex >= 0
            ? validation.DuplicateEntryIndex
            : -1;
        string domain = validation.HasEntryMetadata ? validation.Domain.ToString() : "unavailable";
        string signature = validation.HasEntryMetadata ? validation.Signature.ToString() : "unavailable";

        return "status=" + readiness
            + " initialized=" + (validation.IsInitialized ? "true" : "false")
            + " catalog=" + validation.Status
            + " entryCount=" + validation.EntryCount
            + " firstFailingIndex=" + firstFailingIndex
            + " duplicateIndex=" + duplicateIndex
            + " domain=" + domain
            + " signature=" + signature;
    }

    private static string CreateNativeAotHeapBootstrapMetadata(
        NativeAotHeapBootstrapReadinessStatus readiness,
        NativeAotHeapBootstrapPlanResult plan)
    {
        return "status=" + readiness
            + " initialized=" + (plan.IsInitialized ? "true" : "false")
            + " plan=" + plan.Status
            + " pageSize=" + plan.PageSize
            + " allocatorFreePages=" + plan.AllocatorFreePages
            + " allocatorFreeBytes=" + plan.AllocatorFreeBytes
            + " expectedAllocatorFreeBytes=" + plan.ExpectedAllocatorFreeBytes
            + " requestedHeapPages=" + plan.RequestedHeapPages
            + " requestedHeapBytes=" + plan.RequestedHeapBytes
            + " remainingFreePages=" + plan.RemainingFreePages
            + " remainingFreeBytes=" + plan.RemainingFreeBytes
            + " objectAlignment=" + plan.ObjectAlignment
            + " requiresPageAllocatorMutation=" + (plan.RequiresPageAllocatorMutation ? "true" : "false")
            + " requiresGcRootScanning=" + (plan.RequiresGcRootScanning ? "true" : "false")
            + " requiresWriteBarriers=" + (plan.RequiresWriteBarriers ? "true" : "false")
            + " satisfiesObjectAllocationHelpers="
            + (plan.SatisfiesObjectAllocationHelpers ? "true" : "false");
    }

    private static string CreateNativeAotClassConstructorRunnerPreflightMetadata(
        NativeAotClassConstructorRunnerPreflightReadinessStatus readiness,
        NativeAotClassConstructorRunnerPreflightResult preflight)
    {
        return "status=" + readiness
            + " initialized=" + (preflight.IsInitialized ? "true" : "false")
            + " preflight=" + preflight.Status
            + " typeCount=" + preflight.TypeCount
            + " targetMetadataCount=" + preflight.TargetMetadataCount
            + " occupiedCount=" + preflight.OccupiedCount
            + " capacity=" + preflight.Capacity
            + " firstProblemIndex=" + preflight.FirstProblemIndex
            + " firstProblemState=" + preflight.FirstProblemState
            + " firstProblemFailureCode=" + preflight.FirstProblemFailureCode
            + " executesConstructors="
            + (NativeAotClassConstructorRunnerPreflight.ExecutesConstructors ? "true" : "false")
            + " usesReflection="
            + (NativeAotClassConstructorRunnerPreflight.UsesReflection ? "true" : "false")
            + " usesExceptionControlFlow="
            + (NativeAotClassConstructorRunnerPreflight.UsesExceptionControlFlow ? "true" : "false")
            + " requiresTargetMetadata="
            + (NativeAotClassConstructorRunnerPreflight.RequiresTargetMetadata ? "true" : "false");
    }

    private static string CreateNativeAotClassConstructorWorkItemMetadata(
        NativeAotClassConstructorWorkItemReadinessStatus readiness,
        NativeAotClassConstructorWorkItemPlanResult plan)
    {
        return "status=" + readiness
            + " initialized=" + (plan.IsInitialized ? "true" : "false")
            + " plan=" + plan.Status
            + " preflight=" + plan.PreflightStatus
            + " typeCount=" + plan.TypeCount
            + " targetMetadataCount=" + plan.TargetMetadataCount
            + " plannedCount=" + plan.PlannedCount
            + " skippedInitializedCount=" + plan.SkippedInitializedCount
            + " requiredWorkItemCapacity=" + plan.RequiredWorkItemCapacity
            + " availableWorkItemCapacity=" + plan.AvailableWorkItemCapacity
            + " firstProblemIndex=" + plan.FirstProblemIndex
            + " firstProblemState=" + plan.FirstProblemState
            + " firstProblemFailureCode=" + plan.FirstProblemFailureCode
            + " executesConstructors="
            + (NativeAotClassConstructorWorkItemPlanner.ExecutesConstructors ? "true" : "false")
            + " usesReflection="
            + (NativeAotClassConstructorWorkItemPlanner.UsesReflection ? "true" : "false")
            + " usesExceptionControlFlow="
            + (NativeAotClassConstructorWorkItemPlanner.UsesExceptionControlFlow ? "true" : "false")
            + " usesManagedDelegates="
            + (NativeAotClassConstructorWorkItemPlanner.UsesManagedDelegates ? "true" : "false")
            + " discoversMetadata="
            + (NativeAotClassConstructorWorkItemPlanner.DiscoversMetadata ? "true" : "false");
    }

    private static string CreateFetchAppHttpRequestEndpointMetadata(
        FetchAppHttpRequestEndpointReadinessStatus readiness,
        FetchAppHttpRequestEndpointResult endpoint)
    {
        return "status=" + readiness
            + " initialized=" + (endpoint.IsInitialized ? "true" : "false")
            + " endpoint=" + endpoint.Status
            + " port=" + endpoint.Port
            + " hostBytes=" + endpoint.HostBytesWritten
            + " pathBytes=" + endpoint.PathBytesWritten
            + " requiredHostBytes=" + endpoint.RequiredHostBytes
            + " requiredPathBytes=" + endpoint.RequiredPathBytes
            + " availableHostBytes=" + endpoint.AvailableHostBytes
            + " availablePathBytes=" + endpoint.AvailablePathBytes
            + " configuredUrl=" + (endpoint.HasConfiguredUrl ? "true" : "false")
            + " configuredHost=" + (endpoint.HasConfiguredHost ? "true" : "false")
            + " configuredPort=" + (endpoint.HasConfiguredPort ? "true" : "false")
            + " configuredPath=" + (endpoint.HasConfiguredPath ? "true" : "false");
    }

    private static string CreateFetchAppHttpCompletionMetadata(
        FetchAppHttpCompletionReadinessStatus readiness,
        FetchAppHttpCompletionPlanResult completion)
    {
        return "status=" + readiness
            + " initialized=" + (completion.IsInitialized ? "true" : "false")
            + " succeeded=" + (completion.Succeeded ? "true" : "false")
            + " completion=" + completion.Status
            + " httpPlan=" + completion.HttpPlanStatus
            + " httpStatus=" + completion.HttpStatusCode
            + " expectedStatus=" + completion.ExpectedHttpStatusCode
            + " responseBytes=" + completion.ResponseBytesRead
            + " bodyOffset=" + completion.BodyOffset
            + " bodyLength=" + completion.BodyLength
            + " markerLength=" + completion.ExpectedMarkerLength
            + " matchOffset=" + completion.MatchOffset;
    }

    private static string CreateFetchAppHttpTcpExchangeMetadata(
        FetchAppHttpTcpExchangeReadinessStatus readiness,
        FetchAppHttpTcpExchangePlanResult exchange,
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult format)
    {
        return "status=" + readiness
            + " initialized=" + (exchange.IsInitialized ? "true" : "false")
            + " succeeded=" + (exchange.Succeeded ? "true" : "false")
            + " exchange=" + exchange.Status
            + " format=" + format.Status
            + " bytesWritten=" + format.BytesWritten
            + " requiredBytes=" + format.RequiredBytes
            + " availableBytes=" + format.AvailableBytes
            + " context=" + exchange.Context.Status
            + " syn=" + exchange.Syn.Status
            + " synAck=" + exchange.SynAck.Status
            + " finalAck=" + exchange.FinalAck.Status
            + " request=" + exchange.Request.Status
            + " response=" + exchange.Response.Status
            + " completion=" + exchange.Completion.Status
            + " close=" + exchange.Close.Status
            + " hostBytes=" + exchange.HostBytes
            + " pathBytes=" + exchange.PathBytes
            + " expectedHttpStatus=" + exchange.ExpectedHttpStatusCode
            + " expectedMarkerBytes=" + exchange.ExpectedMarkerLength
            + " responseBytes=" + exchange.ResponseBytesRead
            + " closeBytes=" + exchange.CloseFrameBytes;
    }

    private static string CreateVirtioNetRxTcpDispatchMetadata(
        VirtioNetRxTcpDispatchReadinessStatus readiness,
        VirtioNetReceiveRegisterWindowTcpDispatchResult dispatch,
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult format)
    {
        return "status=" + readiness
            + " initialized=" + (dispatch.IsInitialized ? "true" : "false")
            + " succeeded=" + (dispatch.Succeeded ? "true" : "false")
            + " dispatch=" + dispatch.Status
            + " receive=" + dispatch.ReceiveDispatchStatus
            + " slab=" + dispatch.SlabCompletionStatus
            + " network=" + dispatch.NetworkDispatchStatus
            + " tcp=" + dispatch.TcpDispatchStatus
            + " action=" + dispatch.TcpAction
            + " format=" + format.Status
            + " bytesWritten=" + format.BytesWritten
            + " requiredBytes=" + format.RequiredBytes
            + " availableBytes=" + format.AvailableBytes
            + " descriptorHead=" + dispatch.DescriptorHead
            + " bufferIndex=" + dispatch.BufferIndex
            + " selectedOffset=" + dispatch.SelectedBufferOffset
            + " selectedLength=" + dispatch.SelectedBufferLength
            + " usedBytes=" + dispatch.UsedByteCount
            + " payloadBytes=" + dispatch.PayloadBytes
            + " passiveAckBytes=" + dispatch.PassiveAckBytes;
    }

    private static string CreateHttpStaticTcpResponseMetadata(
        HttpStaticTcpResponseReadinessStatus readiness,
        HttpStaticTcpResponsePlanResult response,
        HttpStaticTcpResponsePlanDiagnosticFormatResult format)
    {
        return "status=" + readiness
            + " initialized=" + (response.IsInitialized ? "true" : "false")
            + " succeeded=" + (response.Succeeded ? "true" : "false")
            + " plan=" + response.Status
            + " tcp=" + response.TcpDispatchStatus
            + " action=" + response.TcpAction
            + " http=" + response.HttpStatus
            + " request=" + response.RequestStatus
            + " response=" + response.ResponseStatus
            + " method=" + response.Method
            + " format=" + format.Status
            + " bytesWritten=" + format.BytesWritten
            + " requiredBytes=" + format.RequiredBytes
            + " availableBytes=" + format.AvailableBytes
            + " httpStatus=" + response.HttpStatusCode
            + " requestPayloadOffset=" + response.RequestPayloadOffset
            + " requestPayloadBytes=" + response.RequestPayloadLength
            + " responseBytesWritten=" + response.ResponseBytesWritten
            + " responseBytesRequired=" + response.ResponseBytesRequired
            + " responseBodyOffset=" + response.ResponseBodyOffset
            + " responseBodyBytes=" + response.ResponseBodyLength;
    }

    private static string CreateNativeManifestFetchAppConfigPreflightMetadata(
        NativeManifestFetchAppConfigPreflightReadinessStatus readiness,
        NativeManifestFetchAppConfigPreflightResult preflight)
    {
        return "status=" + readiness
            + " initialized=" + (preflight.IsInitialized ? "true" : "false")
            + " preflight=" + preflight.Status
            + " manifestLoad=" + preflight.ManifestLoadStatus
            + " manifestParse=" + preflight.ManifestParseStatus
            + " expectation=" + preflight.ExpectationStatus
            + " expectedStatus=" + preflight.ExpectedHttpStatusCode
            + " markerLength=" + preflight.ExpectedMarkerLength
            + " markerBytesWritten=" + preflight.MarkerBytesWritten
            + " markerRequiredBytes=" + preflight.MarkerRequiredBytes
            + " usesDefaultMarker=" + (preflight.UsesDefaultMarker ? "true" : "false")
            + " configuredStatus=" + (preflight.HasConfiguredStatus ? "true" : "false")
            + " configuredMarker=" + (preflight.HasConfiguredMarker ? "true" : "false")
            + " manifestBytesRead=" + preflight.ManifestBytesRead;
    }

    private static string CreateNativeManifestEnvironmentMetadata(
        NativeManifestEnvironmentReadinessStatus readiness,
        NativeManifestParseResult manifest,
        NativeManifestEnvironmentDiagnosticFormatResult format)
    {
        return "status=" + readiness
            + " initialized=" + (manifest.IsInitialized ? "true" : "false")
            + " parse=" + manifest.Status
            + " format=" + format.Status
            + " environmentCount=" + manifest.Manifest.EnvironmentCount
            + " errorOffset=" + manifest.ErrorOffset
            + " bytesWritten=" + format.BytesWritten
            + " requiredBytes=" + format.RequiredBytes
            + " availableBytes=" + format.AvailableBytes
            + " processEnvironmentMaterialized=false"
            + " hostedDotNetStarted=false"
            + " kestrelStarted=false";
    }

    private static string CreateAppExitObserverMetadata(
        AppExitObserverReadinessStatus readiness,
        NativeAppExitLogObserverResult observer)
    {
        return "status=" + readiness
            + " initialized=" + (observer.IsInitialized ? "true" : "false")
            + " succeeded=" + (observer.Succeeded ? "true" : "false")
            + " observer=" + observer.Status
            + " recordsScanned=" + observer.RecordsScanned
            + " recordIndex=" + observer.RecordIndex
            + " streamId=" + observer.StreamId
            + " flags=" + observer.Flags
            + " sequence=" + observer.Sequence
            + " bytesRead=" + observer.BytesRead
            + " requiredBytes=" + observer.RequiredBytes
            + " logStatus=" + observer.LogStatus
            + " parseAttempted=" + (observer.ParseAttempted ? "true" : "false")
            + " parseStatus=" + observer.ParseStatus
            + " exitCode=" + observer.ExitCode
            + " exitStatus=" + observer.ExitStatus
            + " launcherFailure=" + (observer.LauncherFailure ? "true" : "false");
    }

    private static string CreateNativeAppArgumentVectorMetadata(
        NativeAppArgumentVectorReadinessStatus readiness,
        NativeAppArgumentVectorPlanResult argumentVector)
    {
        return "status=" + readiness
            + " initialized=" + (argumentVector.IsInitialized ? "true" : "false")
            + " vector=" + argumentVector.Status
            + " launch=" + argumentVector.LaunchStatus
            + " argumentCount=" + argumentVector.ArgumentCount
            + " maxArgumentCount=" + NativeAppArgumentVectorPlanner.MaximumArgumentCount
            + " maxArgumentLength=" + NativeAppArgumentVectorPlanner.MaximumArgumentLength
            + " errorOffset=" + argumentVector.ErrorOffset
            + " executesApp=false"
            + " startsKestrel=false";
    }

    private static string CreateNativeAppArgumentHandoffMetadata(
        NativeAppArgumentHandoffReadinessStatus readiness,
        NativeAppArgumentHandoffPlanResult handoff)
    {
        return "status=" + readiness
            + " initialized=" + (handoff.IsInitialized ? "true" : "false")
            + " handoff=" + handoff.Status
            + " invocation=" + handoff.InvocationStatus
            + " argumentVector=" + handoff.ArgumentVectorStatus
            + " argumentCount=" + handoff.ArgumentCount
            + " argumentBytes=" + handoff.ParsedArgumentBytes
            + " parsedArgumentBytes=" + handoff.ParsedArgumentBytes
            + " rawArgumentBytes=" + handoff.RawLaunchArgumentByteLength
            + " stackBytes=0"
            + " stackBytesAvailable=false"
            + " executesApp=" + (handoff.ExecutesApp ? "true" : "false")
            + " definesFinalAbi=" + (handoff.DefinesFinalAbi ? "true" : "false")
            + " startsHostedDotNet=false"
            + " startsKestrel=false";
    }

    private static NativeManifestEnvironmentDiagnosticFormatResult
        TryFormatNativeManifestEnvironmentDiagnostic(
            NativeManifestParseResult manifest,
            ReadOnlySpan<byte> source,
            out string diagnosticPayload)
    {
        diagnosticPayload = string.Empty;

        if (!manifest.IsInitialized || !manifest.Succeeded)
        {
            return NativeManifestEnvironmentDiagnosticFormatResult.Fail(
                NativeManifestEnvironmentDiagnosticFormatStatus.InvalidResult,
                manifest.Status,
                requiredBytes: 0,
                availableBytes: NativeManifestEnvironmentDiagnosticMarkerCapacity);
        }

        NativeManifestEnvironmentDiagnosticFormatResult query =
            NativeManifestEnvironmentDiagnosticFormatter.QueryRequiredBytes(manifest, source);
        if (query.Status != NativeManifestEnvironmentDiagnosticFormatStatus.Success)
        {
            return query;
        }

        if (query.RequiredBytes > NativeManifestEnvironmentDiagnosticMarkerCapacity)
        {
            return NativeManifestEnvironmentDiagnosticFormatResult.Fail(
                NativeManifestEnvironmentDiagnosticFormatStatus.DestinationTooSmall,
                manifest.Status,
                query.RequiredBytes,
                NativeManifestEnvironmentDiagnosticMarkerCapacity);
        }

        Span<byte> marker = stackalloc byte[NativeManifestEnvironmentDiagnosticMarkerCapacity];
        NativeManifestEnvironmentDiagnosticFormatResult format =
            NativeManifestEnvironmentDiagnosticFormatter.FormatResult(
                manifest,
                source,
                marker[..query.RequiredBytes]);
        if (!format.Succeeded)
        {
            return format;
        }

        diagnosticPayload = Encoding.ASCII.GetString(marker[..format.BytesWritten]);
        return format;
    }

    private static FetchAppHttpTcpExchangePlanDiagnosticFormatResult
        TryFormatFetchAppHttpTcpExchangeDiagnostic(
            FetchAppHttpTcpExchangePlanResult exchange,
            out string diagnosticPayload)
    {
        diagnosticPayload = string.Empty;

        FetchAppHttpTcpExchangePlanDiagnosticFormatResult query =
            FetchAppHttpTcpExchangePlanDiagnosticFormatter.QueryRequiredBytes(exchange);
        if (query.Status != FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.Success)
        {
            return query;
        }

        if (query.RequiredBytes > FetchAppHttpTcpExchangeDiagnosticMarkerCapacity)
        {
            return FetchAppHttpTcpExchangePlanDiagnosticFormatResult.Fail(
                FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.DestinationTooSmall,
                exchange.Status,
                query.RequiredBytes,
                FetchAppHttpTcpExchangeDiagnosticMarkerCapacity);
        }

        Span<byte> marker = stackalloc byte[FetchAppHttpTcpExchangeDiagnosticMarkerCapacity];
        FetchAppHttpTcpExchangePlanDiagnosticFormatResult format =
            FetchAppHttpTcpExchangePlanDiagnosticFormatter.FormatResult(
                exchange,
                marker[..query.RequiredBytes]);
        if (!format.Succeeded)
        {
            return format;
        }

        diagnosticPayload = Encoding.ASCII.GetString(marker[..format.BytesWritten]);
        return format;
    }

    private static VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult
        TryFormatVirtioNetRxTcpDispatchDiagnostic(
            VirtioNetReceiveRegisterWindowTcpDispatchResult dispatch,
            out string diagnosticPayload)
    {
        diagnosticPayload = string.Empty;

        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult query =
            VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.QueryRequiredBytes(dispatch);
        if (query.Status !=
            VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success)
        {
            return query;
        }

        if (query.RequiredBytes > VirtioNetRxTcpDispatchDiagnosticMarkerCapacity)
        {
            return VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult.Fail(
                VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.DestinationTooSmall,
                dispatch.Status,
                query.RequiredBytes,
                VirtioNetRxTcpDispatchDiagnosticMarkerCapacity);
        }

        Span<byte> marker = stackalloc byte[VirtioNetRxTcpDispatchDiagnosticMarkerCapacity];
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult format =
            VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.FormatResult(
                dispatch,
                marker[..query.RequiredBytes]);
        if (!format.Succeeded)
        {
            return format;
        }

        diagnosticPayload = Encoding.ASCII.GetString(marker[..format.BytesWritten]);
        return format;
    }

    private static HttpStaticTcpResponsePlanDiagnosticFormatResult
        TryFormatHttpStaticTcpResponseDiagnostic(
            HttpStaticTcpResponsePlanResult response,
            out string diagnosticPayload)
    {
        diagnosticPayload = string.Empty;

        HttpStaticTcpResponsePlanDiagnosticFormatResult query =
            HttpStaticTcpResponsePlanDiagnosticFormatter.QueryRequiredBytes(response);
        if (query.Status != HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success)
        {
            return query;
        }

        if (query.RequiredBytes > HttpStaticTcpResponseDiagnosticMarkerCapacity)
        {
            return HttpStaticTcpResponsePlanDiagnosticFormatResult.Fail(
                HttpStaticTcpResponsePlanDiagnosticFormatStatus.DestinationTooSmall,
                response.Status,
                query.RequiredBytes,
                HttpStaticTcpResponseDiagnosticMarkerCapacity);
        }

        Span<byte> marker = stackalloc byte[HttpStaticTcpResponseDiagnosticMarkerCapacity];
        HttpStaticTcpResponsePlanDiagnosticFormatResult format =
            HttpStaticTcpResponsePlanDiagnosticFormatter.FormatResult(
                response,
                marker[..query.RequiredBytes]);
        if (!format.Succeeded)
        {
            return format;
        }

        diagnosticPayload = Encoding.ASCII.GetString(marker[..format.BytesWritten]);
        return format;
    }

    private static bool IsUefiGoneSimulationProbeEnabled()
    {
        return Probes.EnableUefiGoneSimulationProbe;
    }

    private static void EmitImplementedPhases(ISerialSink serial, BootEventRingBuffer events)
    {
        for (int index = 0; index < BootPhaseCatalog.ImplementedPhaseCount; index++)
        {
            BootPhaseInfo phase = BootPhaseCatalog.GetImplementedPhase(index);
            Emit(serial, events, phase.StartMarker);
            Emit(serial, events, phase.PassMarker);
        }
    }

    private static void Emit(ISerialSink serial, BootEventRingBuffer events, string line)
    {
        events.Record(line);
        serial.WriteLine(line);
    }
#endif
}
