using OS.Tooling;

internal static partial class BootSequencePlanTests
{
    private const string EntryType = "OS.Boot.EfiEntry";
    private const string BootAuthority = "OS.Boot.BootSequence";

    internal static bool HasNoStdlibBootAuthorityBoundary() => Authority(
        HostSourceCatalog.Efi(EfiProfile.Create(null, new Dictionary<string, bool>())));

    private static bool Authority(CSharpSourceCatalog catalog) =>
        catalog.Compilation.References.Count() == 0
        && HostBootSourceGuard.Authority(catalog, BootAuthority, BootSequenceAuthority)
        && HostBootSourceGuard.Authority(catalog, "OS.Kernel.Diagnostics.Probes", ProbesAuthority);

    private static CSharpSourceCatalog BootProfile(params string[] flags) =>
        HostSourceCatalog.Efi(EfiProfile.Create("Diagnostic", flags.ToDictionary(flag => flag, _ => true)));

    [HostTest("boot.sequence M01 stage-one plan preserves EFI linkage boundary", HostLane.Fast, 3)]
    private static bool TestM01StageOnePlanPreservesEfiLinkageBoundary()
    {
        var ordinary = HostSourceCatalog.Efi(EfiProfile.Create(null, new Dictionary<string, bool>()));
        var enabled = BootProfile("EnableM01LoadedImageStackProbe");
        var disabled = BootProfile();
        foreach (var (catalog, active) in new[] { (ordinary, true), (enabled, true), (disabled, false) })
        {
            _ = catalog.Type("OS.Boot.BootSequencePlan");
            if (!Authority(catalog) || !HostBootSourceGuard.CallsConfined(catalog, EntryType,
                    EntryType + "+M01StageOneAdapter", "OS.Boot.M01LoadedImageStackIntegration",
                    ["PrepareFromStaticScratch", "RecordTransferFromStaticState", "ObserveContinuationRspFromStaticState",
                        "ReportUnexpectedReturnFromStaticState"],
                    ["Prepare", "PrepareWithStubByteMode", "RecordTransfer", "ObserveContinuationRsp"], active)
                || !HostBootSourceGuard.CallsConfined(catalog, EntryType, EntryType, BootAuthority,
                    [], ["RunM01LoadedImageStackIntegration"], false))
                return HostCheck.Result(false);
        }
        return HostCheck.Result(true);
    }

    [HostTest("boot.sequence M01 stage-two plan preserves EFI linkage boundary", HostLane.Fast, 6)]
    private static bool TestM01StageTwoPlanPreservesEfiLinkageBoundary() => HostCheck.Result(
        IntegrationBoundary(BootProfile("EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe"),
            "M01LiveAllocatorIntegration"));

    [HostTest("boot.sequence M01 stage-three plan preserves EFI linkage boundary", HostLane.Fast, 12)]
    private static bool TestM01StageThreePlanPreservesEfiLinkageBoundary() => HostCheck.Result(
        IntegrationBoundary(BootProfile("EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe",
            "EnableM01OwnedPagingProbe"), "M01LivePagingIntegration"));

    private static bool IntegrationBoundary(CSharpSourceCatalog catalog, string integration)
    {
        _ = catalog.Type("OS.Boot.BootSequencePlan");
        return Authority(catalog)
            && (integration == "M01LiveAllocatorIntegration"
                ? HostBootSourceGuard.CallsConfined(catalog, EntryType, EntryType + "+M01StageOneAdapter", "OS.Boot." + integration,
                    ["Execute"], [], true, "ExecuteStageTwoCoordinator")
                : HostBootSourceGuard.CallsConfined(catalog, EntryType, EntryType, "OS.Boot." + integration,
                    [], ["Execute"], false))
            && HostBootSourceGuard.CallsConfined(catalog, EntryType, EntryType, BootAuthority,
                [], ["Run" + integration], false);
    }

    [HostTest("boot.sequence Linux syscall source order follows startup and service stop", HostLane.Fast, 52)]
    private static bool TestLinuxSyscallSourceOrder()
    {
        var catalog = BootProfile("EnableM16005Probe");
        const string owner = EntryType + "+M03FirstExitAdapter";
        bool Phase(string method, params string[] calls) => HostBootSourceGuard.OrderedCalls(catalog, owner, method, BootAuthority, calls)
            && HostBootSourceGuard.CallsConfined(catalog, EntryType, owner, BootAuthority, calls, [], true, method);
        return HostCheck.Result(Phase("Prepare",
                "StageLinuxElfPackage", "PrepareLinuxElfStartup", "PrepareLinuxUserBoundary")
            && Phase("Enter",
                "RunFirstRealExitBootServicesTransition")
            && Phase("Diagnose",
                "RunOwnedLinuxElfPackage", "RunOwnedLinuxElfStartup", "RunOwnedWorkloadServiceLoop", "RunOwnedLinuxSyscalls")
            && HostBootSourceGuard.Callback(catalog, owner, "Prepare", "Enter", "OS.Boot.FirstExitImageReferences",
                "EntryCallback", "OS.Boot.FirstExitOwnedStackStub", "entry")
            && HostBootSourceGuard.Callback(catalog, owner, "Prepare", "Diagnose", "OS.Boot.FirstExitImageReferences",
                "DiagnosticCallback", "OS.Boot.FirstExitPermanentCr3Stub", "diagnostic"));
    }

    // Independent d47 API oracle: original member tokens were checked against
    // the active compilation before recording these symbol signatures.
    private static readonly string[] BootSequenceAuthority =
    [
        "private static bool IsFirstRealExitBootServicesEnabled()",
        "public static OS.Boot.FirstExitReturn RunFirstRealExitBootServicesTransition(ref OS.Boot.FirstExitOwnedArenaState arena, byte* code)",
        "public static OS.Boot.OwnedAllocatorRoundTripStatus RunOwnedSubsystemAllocatorRoundTrip(ref OS.Boot.OwnedAllocatorRoundTripState state, ref OS.Boot.FirstExitDiagnosticState diagnostic, ref OS.Boot.FirstExitOwnedArenaState arena, OS.Boot.FirstExitDiagnosticMemory memory, OS.Boot.M01LiveAllocatorStaticState* owner)",
        "public static OS.Kernel.Runtime.CompilerClassConstructorStatus RunCompilerClassConstructors(byte* storage, ulong capacity)",
        "public static OS.Kernel.Runtime.CompilerInterfaceDispatchStatus RunInterfaceDispatch(byte* storage, ulong capacity, ulong receiver, ulong cell, out OS.Kernel.Runtime.CompilerInterfaceDispatchResolution resolution)",
        "public static OS.Kernel.Runtime.X64FrameUnwindResult RunConstructorFailureUnwind(OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity, byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input)",
        "public static OS.Kernel.Runtime.X64FrameUnwindResult RunFilteredCatchUnwind(OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity, byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input)",
        "public static OS.Kernel.Runtime.X64FrameUnwindResult RunFrameDispatchUnwind(OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity, byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input)",
        "public static OS.Kernel.Runtime.X64FrameUnwindResult RunNestedFinallyCatchUnwind(OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity, byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input)",
        "public static OS.Kernel.Runtime.X64FrameUnwindResult RunNormalFinallyUnwind(OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity, byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input)",
        "public static OS.Kernel.Runtime.X64FrameUnwindResult RunRethrowUnwind(OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity, byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input)",
        "public static OS.Kernel.Runtime.X64FrameUnwindResult RunSimpleThrowCatchUnwind(OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity, byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input)",
        "public static OS.Kernel.Runtime.X64FrameUnwindResult RunSingleFinallyCatchUnwind(OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity, byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input)",
        "public static OS.Kernel.Runtime.X64FrameUnwindResult RunUnhandledExceptionUnwind(OS.Boot.EfiLoadedImageRangeCapturePlan capture, OS.Boot.M01LoadedImageVirtualMappingEvidence mapping, byte* owner, ulong ownerCapacity, ulong handle, byte* metadata, ulong metadataCapacity, byte* stack, ulong stackBytes, OS.Kernel.Runtime.X64UnwindContext input)",
        "public static OS.Kernel.Runtime.X64UnwindMetadataResult RunX64UnwindMetadata(byte* storage, ulong capacity, ulong instructionPointer)",
        "public static bool IsConstructorFailureEnabled()",
        "public static bool IsEhCatalogEnabled()",
        "public static bool IsFilteredCatchEnabled()",
        "public static bool IsFirmwareAdapterRevocationEnabled()",
        "public static bool IsFrameDispatchEnabled()",
        "public static bool IsNestedFinallyCatchEnabled()",
        "public static bool IsNormalFinallyEnabled()",
        "public static bool IsRethrowEnabled()",
        "public static bool IsSimpleThrowCatchEnabled()",
        "public static bool IsSingleFinallyCatchEnabled()",
        "public static bool IsUnhandledExceptionEnabled()",
        "public static bool IsWorkloadServiceLoopEnabled()",
        "public static bool PrepareCooperativeSwitch(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare)",
        "public static bool PrepareLinuxDescriptors(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare)",
        "public static bool PrepareLinuxElfStartup(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare)",
        "public static bool PrepareLinuxMemory(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare)",
        "public static bool PrepareLinuxUserBoundary(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare)",
        "public static bool PrepareNetworkService(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare)",
        "public static bool PrepareOwnedReadOnlyDisk(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare)",
        "public static bool PrepareOwnedRealtime(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare)",
        "public static bool PrepareOwnedWritableDisk(bool firmwareAvailable, void* context, delegate*<void*, bool> prepare)",
        "public static bool ReportVerifiedExitCompletion(void* context, delegate*<void*, byte*, uint, bool> write)",
        "public static bool RunOwnedAcpiSelection(bool ownedSubsystemsComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedApicSetup(bool pciEnumerationComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedArpReceive(bool originalNetComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedBarDma(bool pciEnumerationComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedBlockMsix(bool packageAndApicComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedCooperativeSwitch(bool ownedChecksComplete, bool firmwareRevoked, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedDiskStaticFileApplication(bool startupReady, bool serviceStopped, bool firmwareRevoked, bool apicReady, bool networkReady, bool diskPrepared, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedFatRead(bool gptPartitionComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedGptPartition(bool liveBlockContractComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedKernelThreads(bool ownedChecksComplete, bool apicReady, bool firmwareRevoked, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedLinuxDescriptors(bool startupReady, bool serviceStopped, bool firmwareRevoked, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedLinuxElfPackage(bool ownedChecksComplete, bool firmwareRevoked, void* context, delegate*<void*, bool> verify)",
        "public static bool RunOwnedLinuxElfStartup(bool packageReady, bool firmwareRevoked, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedLinuxMemory(bool startupReady, bool serviceStopped, bool firmwareRevoked, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedLinuxSignals(bool startupReady, bool serviceStopped, bool firmwareRevoked, bool apicReady, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedLinuxSockets(bool startupReady, bool serviceStopped, bool firmwareRevoked, bool apicReady, bool nicReady, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedLinuxSyscalls(bool startupReady, bool serviceStopped, bool firmwareRevoked, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedLinuxThreads(bool startupReady, bool serviceStopped, bool firmwareRevoked, bool apicReady, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedLiveBlockContract(bool blockReadComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedMonotonicClock(bool ownedSubsystemsComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedNativeAotRuntime(bool startupReady, bool serviceStopped, bool firmwareRevoked, bool apicReady, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedNativeAotSuspension(bool startupReady, bool serviceStopped, bool firmwareRevoked, bool apicReady, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedNativeAotThreadPool(bool startupReady, bool serviceStopped, bool firmwareRevoked, bool apicReady, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedNetworkService(bool originalArpComplete, bool serviceLoopComplete, bool firmwareRevoked, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedPackageRead(bool fatReadComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedPciEnumeration(bool ownedSubsystemsComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedSchedulerTimer(bool ownedChecksComplete, bool apicReady, bool firmwareRevoked, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedStaticFileApplication(bool startupReady, bool serviceStopped, bool firmwareRevoked, bool apicReady, bool networkReady, void* context, delegate*<void*, bool> run)",
        "public static bool RunOwnedVirtioBlockQueueSetup(bool pciEnumerationComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedVirtioBlockRead(bool blockQueueSetupComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedVirtioNegotiation(bool pciEnumerationComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedVirtioNet(bool originalPollingComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedVirtqueueInterrupt(bool apicAndNegotiationComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedVirtqueuePolling(bool virtioNegotiationComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool RunOwnedWorkloadServiceLoop(bool packageReady, bool firmwareRevoked, void* context, delegate*<void*, bool> run)",
        "public static bool RunPostFirmwarePhysicalMapping(bool ownedSubsystemsComplete, void* context, delegate*<void*, bool> execute)",
        "public static bool StageLinuxElfPackage(bool firmwareAvailable, void* context, delegate*<void*, bool> stage)",
    ];
    private static readonly string[] ProbesAuthority =
    [
        "public const bool EnableCheckedReferenceBarrierProbe",
        "public const bool EnableDoubleFaultIstProbe",
        "public const bool EnableFirmwareEntropySeedProbe",
        "public const bool EnableGCStaticRootMaterializationProbe",
        "public const bool EnableInvalidOpcodePanicProbe",
        "public const bool EnableM03FirmwareAdapterRevocationProbe",
        "public const bool EnableM03FirstRealExitBootServicesProbe",
        "public const bool EnableM03OwnedSubsystemPanicProbe",
        "public const bool EnableM03OwnedSubsystemSurvivalProbe",
        "public const bool EnableM04ClassConstructorExecutionProbe",
        "public const bool EnableM04ConstructorFailureProbe",
        "public const bool EnableM04EhCatalogProbe",
        "public const bool EnableM04FilteredCatchProbe",
        "public const bool EnableM04FrameDispatchProbe",
        "public const bool EnableM04InterfaceDispatchProbe",
        "public const bool EnableM04NestedFinallyCatchProbe",
        "public const bool EnableM04NormalFinallyProbe",
        "public const bool EnableM04RethrowProbe",
        "public const bool EnableM04SeededCollectorChurnProbe",
        "public const bool EnableM04SimpleThrowCatchProbe",
        "public const bool EnableM04SingleFinallyCatchProbe",
        "public const bool EnableM04UnhandledExceptionProbe",
        "public const bool EnableM04X64UnwindMetadataProbe",
        "public const bool EnableM05ApicSetupProbe",
        "public const bool EnableM05BarDmaProbe",
        "public const bool EnableM05MonotonicClockProbe",
        "public const bool EnableM05OwnedAcpiSelectionProbe",
        "public const bool EnableM05PciEnumerationProbe",
        "public const bool EnableM05PhysicalMappingProbe",
        "public const bool EnableM05VirtioNegotiationProbe",
        "public const bool EnableM05VirtqueueInterruptProbe",
        "public const bool EnableM05VirtqueuePollingProbe",
        "public const bool EnableM06BlockMsixPollingRecoveryProbe",
        "public const bool EnableM06BlockMsixProbe",
        "public const bool EnableM06FatReadProbe",
        "public const bool EnableM06GptPartitionProbe",
        "public const bool EnableM06LiveBlockContractProbe",
        "public const bool EnableM06PackageReadProbe",
        "public const bool EnableM06VirtioBlockQueueSetupProbe",
        "public const bool EnableM06VirtioBlockReadProbe",
        "public const bool EnableM07026Probe",
        "public const bool EnableM07027Probe",
        "public const bool EnableM07028Probe",
        "public const bool EnableM07ArpReceiveProbe",
        "public const bool EnableM07VirtioNetProbe",
        "public const bool EnableM09004Probe",
        "public const bool EnableM09005Probe",
        "public const bool EnableM09006Probe",
        "public const bool EnableM09009Probe",
        "public const bool EnableM09010Probe",
        "public const bool EnableM09011Probe",
        "public const bool EnableM09012Probe",
        "public const bool EnableM10004Probe",
        "public const bool EnableM10017Probe",
        "public const bool EnableM10018FullProbe",
        "public const bool EnableM10018Probe",
        "public const bool EnableM10018TimingProbe",
        "public const bool EnableM16002Probe",
        "public const bool EnableM16003Probe",
        "public const bool EnableM16004Probe",
        "public const bool EnableM16005Probe",
        "public const bool EnableM16006Probe",
        "public const bool EnableM16007Probe",
        "public const bool EnableM16008Probe",
        "public const bool EnableM16009Probe",
        "public const bool EnableM16010Probe",
        "public const bool EnableM16011Probe",
        "public const bool EnableM16012Probe",
        "public const bool EnableM16013Probe",
        "public const bool EnableM16014Probe",
        "public const bool EnableM16019InvalidProbe",
        "public const bool EnableM16019UnavailableProbe",
        "public const bool EnableM16023Probe",
        "public const bool EnableM16024Probe",
        "public const bool EnableM16025Probe",
        "public const bool EnableM16027Probe",
        "public const bool EnableM16027TimingProbe",
        "public const bool EnableMemoryMapCaptureProbe",
        "public const bool EnableNullPointerPanicProbe",
        "public const bool EnableStringAllocationBeforeBindProbe",
        "public const bool EnableStringAllocationExhaustionProbe",
        "public const bool EnableStringAllocationInvalidLengthProbe",
        "public const bool EnableStringAllocationProbe",
        "public const bool EnableTypeManagerModuleRegistrationProbe",
        "public const bool EnableTypedExitBootServicesCallProbe",
        "public const bool EnableUnexpectedVectorPanicProbe",
    ];
}
