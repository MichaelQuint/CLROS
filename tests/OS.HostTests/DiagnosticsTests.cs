using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static class DiagnosticsTests
{
    private const string M01016TestName = "m01.016";
    private const string M01016ReadyMarker = "CLROS_M01_016_READY";

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostic log emits stable serial prefixes", TestDiagnosticLogPrefixes, ref failures);
        Run("panic formatter emits optional machine-readable fields", TestPanicFormatterFields, ref failures);
        Run("panic formatter emits page fault register dump", TestPageFaultRegisterDump, ref failures);
        Run("panic formatter emits invalid opcode register dump", TestInvalidOpcodeRegisterDump, ref failures);
        Run("panic report return mode is host-testable", TestPanicReturnMode, ref failures);
        Run("probes expose grouped safe defaults", TestProbeGroupsAndSafeDefaults, ref failures);
        Run("probes enumerate all definitions", TestProbeCatalogEnumeratesAllDefinitions, ref failures);
        Run("probes catalog reports destination capacity", TestProbeCatalogReportsDestinationCapacity, ref failures);
        Run("probes catalog formatter formats all definitions", TestProbeCatalogFormatterFormatsAllDefinitions, ref failures);
        Run("probes catalog formatter reports destination capacity", TestProbeCatalogFormatterReportsDestinationCapacity, ref failures);
        Run("probes catalog formatter keeps dangerous probes disabled", TestProbeCatalogFormatterKeepsDangerousProbesDisabled, ref failures);
        Run("probes keep dangerous toggles disabled", TestDangerousProbeDefaults, ref failures);
        failures += RunM01016();

        if (failures != 0)
        {
            throw new InvalidOperationException("Diagnostics host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static int RunM01016()
    {
        int failures = 0;
        Console.WriteLine("TEST_START " + M01016TestName);

        Run(
            "m01.016 switches are exact compile-time false catalog entries",
            TestM01016SwitchesAndCatalog,
            ref failures);
        Run(
            "m01.016 stage policy rejects impossible combinations",
            TestM01016StagePolicy,
            ref failures);
        Run(
            "m01.016 null fault remains deferred to m02.014",
            TestM01016NullFaultDeferral,
            ref failures);
        Run(
            "m01.016 marker is host-only and initializer remains unique",
            TestM01016HostOnlyMarker,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL "
                + M01016TestName
                + " failures="
                + failures);
            return failures;
        }

        Console.WriteLine(M01016ReadyMarker);
        Console.WriteLine("TEST_PASS " + M01016TestName);
        return 0;
    }

    private static bool TestM01016SwitchesAndCatalog()
    {
        if (nameof(Probes.EnableM01LoadedImageStackProbe) !=
                "EnableM01LoadedImageStackProbe" ||
            nameof(Probes.EnableM01LiveAllocatorProbe) !=
                "EnableM01LiveAllocatorProbe" ||
            nameof(Probes.EnableM01OwnedPagingProbe) !=
                "EnableM01OwnedPagingProbe" ||
            !IsCompileTimeFalseProbe(
                nameof(Probes.EnableM01LoadedImageStackProbe)) ||
            !IsCompileTimeFalseProbe(
                nameof(Probes.EnableM01LiveAllocatorProbe)) ||
            !IsCompileTimeFalseProbe(
                nameof(Probes.EnableM01OwnedPagingProbe)) ||
            Probes.ProbeCatalogCount != typeof(Probes).GetMethods(BindingFlags.Public | BindingFlags.Static).Count(method => method.ReturnType == typeof(ProbeDefinition) && method.GetParameters().Length == 0) ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM03FirstRealExitBootServicesProbe)) ||
            !Probes.TryGetProbe(16, out ProbeDefinition firstExit) ||
            firstExit.Name != nameof(Probes.EnableM03FirstRealExitBootServicesProbe) ||
            firstExit.EnabledByDefault || firstExit.Risk != ProbeRisk.Destructive ||
            firstExit.Group != Probes.UefiExitBootServicesGroup ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM03FirmwareAdapterRevocationProbe)) ||
            !Probes.TryGetProbe(17, out ProbeDefinition revocation) ||
            revocation.Name != nameof(Probes.EnableM03FirmwareAdapterRevocationProbe) ||
            revocation.EnabledByDefault || revocation.Risk != ProbeRisk.Destructive ||
            revocation.Group != Probes.UefiExitBootServicesGroup ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM04ClassConstructorExecutionProbe)) ||
            !Probes.TryGetProbe(18, out ProbeDefinition constructors) ||
            constructors.Name != nameof(Probes.EnableM04ClassConstructorExecutionProbe) ||
            constructors.EnabledByDefault || constructors.Risk != ProbeRisk.Destructive ||
            constructors.Group != "runtime.cctor" ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM04InterfaceDispatchProbe)) ||
            !Probes.TryGetProbe(19, out ProbeDefinition dispatch) ||
            dispatch.Name != nameof(Probes.EnableM04InterfaceDispatchProbe) ||
            dispatch.EnabledByDefault || dispatch.Risk != ProbeRisk.Destructive ||
            dispatch.Group != "runtime.dispatch" ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM04X64UnwindMetadataProbe)) ||
            !Probes.TryGetProbe(20, out ProbeDefinition unwind) ||
            unwind.Name != nameof(Probes.EnableM04X64UnwindMetadataProbe) ||
            unwind.EnabledByDefault || unwind.Risk != ProbeRisk.Destructive ||
            unwind.Group != "runtime.unwind-metadata" ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM03OwnedSubsystemSurvivalProbe)) ||
            !Probes.TryGetProbe(21, out ProbeDefinition survival) ||
            survival.Name != nameof(Probes.EnableM03OwnedSubsystemSurvivalProbe) ||
            survival.EnabledByDefault || survival.Risk != ProbeRisk.Destructive ||
            survival.Group != Probes.UefiExitBootServicesGroup ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM03OwnedSubsystemPanicProbe)) ||
            !Probes.TryGetProbe(22, out ProbeDefinition panic) ||
            panic.Name != nameof(Probes.EnableM03OwnedSubsystemPanicProbe) ||
            panic.EnabledByDefault || panic.Risk != ProbeRisk.Destructive ||
            panic.Group != Probes.UefiExitBootServicesGroup ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM04SimpleThrowCatchProbe)) ||
            !Probes.TryGetProbe(23, out ProbeDefinition simpleCatch) ||
            simpleCatch.Name != nameof(Probes.EnableM04SimpleThrowCatchProbe) ||
            simpleCatch.EnabledByDefault || simpleCatch.Risk != ProbeRisk.Destructive ||
            simpleCatch.Group != "runtime.exception" ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM04SingleFinallyCatchProbe)) ||
            !Probes.TryGetProbe(24, out ProbeDefinition singleFinally) ||
            singleFinally.Name != nameof(Probes.EnableM04SingleFinallyCatchProbe) ||
            singleFinally.EnabledByDefault || singleFinally.Risk != ProbeRisk.Destructive ||
            singleFinally.Group != "runtime.exception" ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM04RethrowProbe)) ||
            !Probes.TryGetProbe(25, out ProbeDefinition rethrow) ||
            rethrow.Name != nameof(Probes.EnableM04RethrowProbe) ||
            rethrow.EnabledByDefault || rethrow.Risk != ProbeRisk.Destructive ||
            rethrow.Group != "runtime.exception" ||
            !IsCompileTimeFalseProbe(nameof(Probes.EnableM05PhysicalMappingProbe)) ||
            !Probes.TryGetProbe(26, out ProbeDefinition mapping) ||
            mapping.Name != nameof(Probes.EnableM05PhysicalMappingProbe) ||
            mapping.EnabledByDefault || mapping.Risk != ProbeRisk.Destructive ||
            mapping.Group != "platform.mapping")
        {
            return false;
        }

        if (!Probes.TryGetProbe(
                13,
                out ProbeDefinition loadedImageStack) ||
            !Probes.TryGetProbe(
                14,
                out ProbeDefinition liveAllocator) ||
            !Probes.TryGetProbe(
                15,
                out ProbeDefinition ownedPaging))
        {
            return false;
        }

        ProbeDefinition[] definitions =
        [
            loadedImageStack,
            liveAllocator,
            ownedPaging
        ];
        string[] expectedNames =
        [
            nameof(Probes.EnableM01LoadedImageStackProbe),
            nameof(Probes.EnableM01LiveAllocatorProbe),
            nameof(Probes.EnableM01OwnedPagingProbe)
        ];
        for (int index = 0; index < definitions.Length; index++)
        {
            ProbeDefinition definition = definitions[index];
            if (definition.Name != expectedNames[index] ||
                definition.Group != Probes.M01LiveMemoryGroup ||
                definition.EnabledByDefault ||
                definition.Risk != ProbeRisk.Destructive ||
                definition.Description.Length == 0)
            {
                return false;
            }
        }

        ProbeDefinition[] catalog =
            new ProbeDefinition[Probes.ProbeCatalogCount];
        ProbeCatalogCopyResult copy =
            Probes.CopyProbeCatalog(catalog);
        return copy.Succeeded
            && CatalogNameCount(
                catalog,
                nameof(Probes.EnableM01LoadedImageStackProbe)) == 1
            && CatalogNameCount(
                catalog,
                nameof(Probes.EnableM01LiveAllocatorProbe)) == 1
            && CatalogNameCount(
                catalog,
                nameof(Probes.EnableM01OwnedPagingProbe)) == 1
            && Probes.GetConfiguredM01LiveProbePolicyStatus() ==
                M01LiveProbePolicyStatus.Success;
    }

    private static bool TestM01016StagePolicy()
    {
        return (byte)M01LiveProbePolicyStatus.Uninitialized == 0
            && (byte)M01LiveProbePolicyStatus.Success == 1
            && (byte)M01LiveProbePolicyStatus
                .LiveAllocatorRequiresLoadedImageStack == 2
            && (byte)M01LiveProbePolicyStatus
                .OwnedPagingRequiresLiveAllocator == 3
            && default(M01LiveProbePolicyStatus) ==
                M01LiveProbePolicyStatus.Uninitialized
            && default(M01LiveProbePolicyStatus) !=
                M01LiveProbePolicyStatus.Success
            && Probes.ValidateM01LiveProbePolicy(
                false,
                false,
                false) == M01LiveProbePolicyStatus.Success
            && Probes.ValidateM01LiveProbePolicy(
                true,
                false,
                false) == M01LiveProbePolicyStatus.Success
            && Probes.ValidateM01LiveProbePolicy(
                true,
                true,
                false) == M01LiveProbePolicyStatus.Success
            && Probes.ValidateM01LiveProbePolicy(
                true,
                true,
                true) == M01LiveProbePolicyStatus.Success
            && Probes.ValidateM01LiveProbePolicy(
                false,
                true,
                false) ==
                    M01LiveProbePolicyStatus
                        .LiveAllocatorRequiresLoadedImageStack
            && Probes.ValidateM01LiveProbePolicy(
                false,
                true,
                true) ==
                    M01LiveProbePolicyStatus
                        .LiveAllocatorRequiresLoadedImageStack
            && Probes.ValidateM01LiveProbePolicy(
                false,
                false,
                true) ==
                    M01LiveProbePolicyStatus
                        .OwnedPagingRequiresLiveAllocator
            && Probes.ValidateM01LiveProbePolicy(
                true,
                false,
                true) ==
                    M01LiveProbePolicyStatus
                        .OwnedPagingRequiresLiveAllocator;
    }

    private static bool TestM01016NullFaultDeferral()
    {
        return !Probes.EnableNullPointerPanicProbe
            && !NullPageFaultProbePlan.EnabledByDefault
            && !NullPageFaultProbePlan.PerformsLiveFault
            && !NullPageFaultProbePlan.DereferencesPageZero
            && !NullPageFaultProbePlan.EmitsExecutableBytes
            && !NullPageFaultProbePlan.ExecutesProbe
            && !NullPageFaultProbePlan.IsGuestEvidence
            && NullPageFaultProbePlan.ExecutionDeferredToM02014;
    }

    private static bool TestM01016HostOnlyMarker()
    {
        HostCheck.SingleFixture(typeof(DiagnosticsTests), nameof(RunAll));
        string repositoryRoot =
            HostToolProcess.FindRepositoryRoot();
        string testPath = Path.GetFullPath(
            Path.Combine(
                repositoryRoot,
                @"tests\OS.HostTests\DiagnosticsTests.cs"));
        string testSource = File.ReadAllText(testPath);
        string readyMarker =
            "CLROS_" + "M01_016_READY";
        string aggregateSignature =
            "private static int RunM01016()";
        string nextMethodSignature =
            "private static bool TestM01016SwitchesAndCatalog()";
        int aggregateStart = testSource.IndexOf(
            aggregateSignature,
            StringComparison.Ordinal);
        int aggregateEnd = testSource.IndexOf(
            nextMethodSignature,
            StringComparison.Ordinal);
        if (CountOccurrences(testSource, readyMarker) != 1 ||
            aggregateStart < 0 ||
            aggregateEnd <= aggregateStart)
        {
            return false;
        }

        string aggregateSource = testSource.Substring(
            aggregateStart,
            aggregateEnd - aggregateStart);
        string startEmission =
            "Console.WriteLine(\"TEST_" +
            "START \" + M01016TestName);";
        string failureEmission =
            "\"TEST_" + "FAIL \"";
        string failureReturn =
            "return failures;";
        string readyEmission =
            "Console.WriteLine(M01016ReadyMarker);";
        string passEmission =
            "Console.WriteLine(\"TEST_" +
            "PASS \" + M01016TestName);";
        int startIndex = aggregateSource.IndexOf(
            startEmission,
            StringComparison.Ordinal);
        int failureIndex = aggregateSource.IndexOf(
            failureEmission,
            StringComparison.Ordinal);
        int returnIndex = aggregateSource.IndexOf(
            failureReturn,
            StringComparison.Ordinal);
        int readyIndex = aggregateSource.IndexOf(
            readyEmission,
            StringComparison.Ordinal);
        int passIndex = aggregateSource.IndexOf(
            passEmission,
            StringComparison.Ordinal);
        if (CountOccurrences(aggregateSource, startEmission) != 1 ||
            CountOccurrences(aggregateSource, failureEmission) != 1 ||
            CountOccurrences(aggregateSource, failureReturn) != 1 ||
            CountOccurrences(aggregateSource, readyEmission) != 1 ||
            CountOccurrences(aggregateSource, passEmission) != 1 ||
            startIndex >= failureIndex ||
            failureIndex >= returnIndex ||
            returnIndex >= readyIndex ||
            readyIndex >= passIndex)
        {
            return false;
        }

        string[] roots =
        [
            "src",
            "std",
            "apps",
            "tools",
            "tests",
            "scripts"
        ];
        foreach (string rootName in roots)
        {
            string rootPath = Path.Combine(
                repositoryRoot,
                rootName);
            if (!Directory.Exists(rootPath))
                continue;

            foreach (string file in Directory.EnumerateFiles(
                rootPath,
                "*",
                SearchOption.AllDirectories))
            {
                string fullPath = Path.GetFullPath(file);
                if (fullPath.Equals(
                        testPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    IsGeneratedBuildPath(fullPath))
                {
                    continue;
                }

                if (!IsMarkerSearchSource(fullPath))
                    continue;

                if (File.ReadAllText(fullPath).Contains(
                    readyMarker,
                    StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsMarkerSearchSource(string fullPath)
    {
        string extension = Path.GetExtension(fullPath);
        return extension.Equals(
                ".cs",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".ps1",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".psm1",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".psd1",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".cmd",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".bat",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".sh",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".csproj",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".props",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".targets",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompileTimeFalseProbe(string fieldName)
    {
        FieldInfo? field = typeof(Probes).GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.Static);
        return field is not null
            && field.FieldType == typeof(bool)
            && field.IsLiteral
            && !field.IsInitOnly
            && field.GetRawConstantValue() is bool enabled
            && !enabled;
    }

    private static bool IsGeneratedBuildPath(string fullPath)
    {
        string separator = Path.DirectorySeparatorChar.ToString();
        return fullPath.Contains(
                separator + "bin" + separator,
                StringComparison.OrdinalIgnoreCase)
            || fullPath.Contains(
                separator + "obj" + separator,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool TestDiagnosticLogPrefixes()
    {
        DiagnosticTestSerialSink serial = new DiagnosticTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        DiagnosticLog log = new DiagnosticLog(serial, events);

        log.Info("kernel ready");
        log.Warn("probe skipped");
        log.Panic("fatal path");

        string[] eventSnapshot = events.Snapshot();

        return serial.Count == 3
            && serial.LineAt(0) == "[info] kernel ready"
            && serial.LineAt(1) == "[warn] probe skipped"
            && serial.LineAt(2) == "[PANIC] fatal path"
            && eventSnapshot.Length == 3
            && eventSnapshot[0] == serial.LineAt(0)
            && eventSnapshot[1] == serial.LineAt(1)
            && eventSnapshot[2] == serial.LineAt(2);
    }

    private static bool TestPanicFormatterFields()
    {
        string withoutVector = PanicFormatter.Format("diagnostics", "manual-probe", null);
        string withVector = PanicFormatter.Format(
            "idt-probe",
            PanicFormatter.InvalidOpcodeProbe,
            6);

        return withoutVector == "OS_SMOKE_FAIL subsystem=diagnostics reason=manual-probe"
            && withVector == "OS_SMOKE_FAIL subsystem=idt-probe reason=invalid-opcode-probe vector=6";
    }

    private static bool TestPageFaultRegisterDump()
    {
        CpuFaultFrame frame = CreateFrame(vector: 14, hasErrorCode: true, errorCode: 0x0000000000000002, cr2: 0x0000000000001000);
        string dump = PanicFormatter.FormatFault("idt", PanicFormatter.NullDereferenceProbe, frame);

        return dump == "OS_SMOKE_FAIL subsystem=idt reason=null-dereference-probe vector=14"
            + " errorCodePresent=true errorCode=0x0000000000000002"
            + " rip=0x0000000010002000 rsp=0x0000000000700000 rbp=0x0000000000700100"
            + " rflags=0x0000000000000202 cr2=0x0000000000001000 cr3=0x00000000001AA000"
            + " rax=0x1111111111111111 rbx=0x2222222222222222 rcx=0x3333333333333333"
            + " rdx=0x4444444444444444 rsi=0x5555555555555555 rdi=0x6666666666666666"
            + " r8=0x8888888888888888 r9=0x9999999999999999 r10=0xAAAAAAAAAAAAAAAA"
            + " r11=0xBBBBBBBBBBBBBBBB r12=0xCCCCCCCCCCCCCCCC r13=0xDDDDDDDDDDDDDDDD"
            + " r14=0xEEEEEEEEEEEEEEEE r15=0xFFFFFFFFFFFFFFFF";
    }

    private static bool TestInvalidOpcodeRegisterDump()
    {
        CpuFaultFrame frame = CreateFrame(vector: 6, hasErrorCode: false, errorCode: 0, cr2: 0);
        string dump = PanicFormatter.FormatFault("idt", PanicFormatter.InvalidOpcodeProbe, frame);

        return dump.Contains("OS_SMOKE_FAIL subsystem=idt reason=invalid-opcode-probe vector=6", StringComparison.Ordinal)
            && dump.Contains("errorCodePresent=false errorCode=0x0000000000000000", StringComparison.Ordinal)
            && dump.Contains("rip=0x0000000010002000", StringComparison.Ordinal)
            && dump.Contains("rsp=0x0000000000700000", StringComparison.Ordinal)
            && dump.Contains("rflags=0x0000000000000202", StringComparison.Ordinal)
            && dump.Contains("cr2=0x0000000000000000", StringComparison.Ordinal)
            && dump.Contains("cr3=0x00000000001AA000", StringComparison.Ordinal)
            && dump.Contains("r15=0xFFFFFFFFFFFFFFFF", StringComparison.Ordinal);
    }

    private static bool TestPanicReturnMode()
    {
        DiagnosticTestSerialSink serial = new DiagnosticTestSerialSink();
        DiagnosticLog log = new DiagnosticLog(serial);
        PanicContext nullDereference = new PanicContext(
            "memory",
            PanicFormatter.NullDereferenceProbe,
            14);

        PanicReportAction action = Panic.Report(log, nullDereference, PanicMode.ReturnToKernel);

        return action == PanicReportAction.ReturnedToKernel
            && serial.Count == 1
            && serial.LineAt(0) == "[PANIC] OS_SMOKE_FAIL subsystem=memory reason=null-dereference-probe vector=14";
    }

    private static bool TestProbeGroupsAndSafeDefaults()
    {
        ProbeDefinition boot = Probes.GetBootSmokeProbe();
        ProbeDefinition serial = Probes.GetSerialSmokeProbe();
        ProbeDefinition efi = Probes.GetEfiSmokeProbe();
        ProbeDefinition memoryMap = Probes.GetMemoryMapSmokeProbe();
        ProbeDefinition simulation = Probes.GetUefiGoneSimulationProbe();
        ProbeDefinition exitBootServicesFreshMap = Probes.GetExitBootServicesFreshMapProbe();
        ProbeDefinition acpiPhysicalDiscovery = Probes.GetAcpiPhysicalDiscoveryProbe();

        return boot.Group == Probes.BootSmokeGroup
            && boot.Name == nameof(Probes.EmitBootSmokeMarkers)
            && boot.EnabledByDefault
            && boot.Risk == ProbeRisk.Safe
            && boot.Description.Length > 0
            && serial.Group == Probes.SerialSmokeGroup
            && serial.EnabledByDefault
            && serial.Risk == ProbeRisk.Safe
            && efi.Group == Probes.EfiSmokeGroup
            && efi.EnabledByDefault
            && efi.Risk == ProbeRisk.Safe
            && memoryMap.Group == Probes.MemoryMapSmokeGroup
            && memoryMap.EnabledByDefault
            && memoryMap.Risk == ProbeRisk.Safe
            && simulation.Group == Probes.EfiSmokeGroup
            && !simulation.EnabledByDefault
            && simulation.Risk == ProbeRisk.BoundarySimulation
            && exitBootServicesFreshMap.Group == Probes.UefiExitBootServicesGroup
            && exitBootServicesFreshMap.Name == nameof(Probes.EnableExitBootServicesFreshMapProbe)
            && !exitBootServicesFreshMap.EnabledByDefault
            && exitBootServicesFreshMap.Risk == ProbeRisk.BoundarySimulation
            && exitBootServicesFreshMap.Description.Length > 0
            && acpiPhysicalDiscovery.Group == Probes.AcpiDiscoveryGroup
            && acpiPhysicalDiscovery.Name == nameof(Probes.EnableAcpiPhysicalDiscoveryProbe)
            && !acpiPhysicalDiscovery.EnabledByDefault
            && acpiPhysicalDiscovery.Risk == ProbeRisk.DeviceIo
            && acpiPhysicalDiscovery.Description.Length > 0;
    }

    private static bool TestDangerousProbeDefaults()
    {
        ProbeDefinition nullPointer = Probes.GetNullPointerPanicProbe();
        ProbeDefinition invalidOpcode = Probes.GetInvalidOpcodePanicProbe();
        ProbeDefinition block = Probes.GetVirtioBlockReadProbe();
        ProbeDefinition packetDump = Probes.GetPacketDumpProbe();
        ProbeDefinition network = Probes.GetVirtioNetworkProbe();
        ProbeDefinition fetchExchange = Probes.GetFetchAppHttpTcpExchangeProbe();
        ProbeDefinition loadedImageStack =
            Probes.GetM01LoadedImageStackProbe();
        ProbeDefinition liveAllocator =
            Probes.GetM01LiveAllocatorProbe();
        ProbeDefinition ownedPaging =
            Probes.GetM01OwnedPagingProbe();

        return !nullPointer.EnabledByDefault
            && nullPointer.Group == Probes.PanicSmokeGroup
            && nullPointer.Risk == ProbeRisk.Destructive
            && !invalidOpcode.EnabledByDefault
            && invalidOpcode.Group == Probes.PanicSmokeGroup
            && invalidOpcode.Risk == ProbeRisk.Destructive
            && !block.EnabledByDefault
            && block.Group == Probes.StorageSmokeGroup
            && block.Risk == ProbeRisk.DeviceIo
            && !packetDump.EnabledByDefault
            && packetDump.Group == Probes.NetworkSmokeGroup
            && packetDump.Risk == ProbeRisk.DeviceIo
            && !network.EnabledByDefault
            && network.Group == Probes.NetworkSmokeGroup
            && network.Risk == ProbeRisk.DeviceIo
            && !fetchExchange.EnabledByDefault
            && fetchExchange.Group == Probes.NetworkSmokeGroup
            && fetchExchange.Name == nameof(Probes.EnableFetchAppHttpTcpExchangeProbe)
            && fetchExchange.Risk == ProbeRisk.DeviceIo
            && fetchExchange.Description.Contains("FetchApp", StringComparison.Ordinal)
            && !loadedImageStack.EnabledByDefault
            && loadedImageStack.Group == Probes.M01LiveMemoryGroup
            && loadedImageStack.Name ==
                nameof(Probes.EnableM01LoadedImageStackProbe)
            && loadedImageStack.Risk == ProbeRisk.Destructive
            && !liveAllocator.EnabledByDefault
            && liveAllocator.Group == Probes.M01LiveMemoryGroup
            && liveAllocator.Name ==
                nameof(Probes.EnableM01LiveAllocatorProbe)
            && liveAllocator.Risk == ProbeRisk.Destructive
            && !ownedPaging.EnabledByDefault
            && ownedPaging.Group == Probes.M01LiveMemoryGroup
            && ownedPaging.Name ==
                nameof(Probes.EnableM01OwnedPagingProbe)
            && ownedPaging.Risk == ProbeRisk.Destructive;
    }

    private static bool TestProbeCatalogEnumeratesAllDefinitions()
    {
        ProbeDefinition[] catalog = new ProbeDefinition[Probes.ProbeCatalogCount];
        ProbeCatalogCopyResult result = Probes.CopyProbeCatalog(catalog);

        bool indexedMatchesCatalog = true;
        for (int i = 0; i < Probes.ProbeCatalogCount; i++)
        {
            if (!Probes.TryGetProbe(i, out ProbeDefinition indexed) ||
                indexed.Name != catalog[i].Name ||
                indexed.Group != catalog[i].Group ||
                indexed.EnabledByDefault != catalog[i].EnabledByDefault ||
                indexed.Risk != catalog[i].Risk)
            {
                indexedMatchesCatalog = false;
                break;
            }
        }

        return result.Succeeded
            && result.Status == ProbeCatalogStatus.Success
            && result.RequiredCount == Probes.ProbeCatalogCount
            && result.AvailableCount == catalog.Length
            && result.WrittenCount == Probes.ProbeCatalogCount
            && indexedMatchesCatalog
            && CatalogContains(catalog, nameof(Probes.EmitBootSmokeMarkers), ProbeRisk.Safe, enabled: true)
            && CatalogContains(catalog, nameof(Probes.EnableInvalidOpcodePanicProbe), ProbeRisk.Destructive, enabled: false)
            && CatalogContains(catalog, nameof(Probes.EnableVirtioBlockReadProbe), ProbeRisk.DeviceIo, enabled: false)
            && CatalogContains(catalog, nameof(Probes.EnableFetchAppHttpTcpExchangeProbe), ProbeRisk.DeviceIo, enabled: false)
            && CatalogContains(catalog, nameof(Probes.EnableM01LoadedImageStackProbe), ProbeRisk.Destructive, enabled: false)
            && CatalogContains(catalog, nameof(Probes.EnableM01LiveAllocatorProbe), ProbeRisk.Destructive, enabled: false)
            && CatalogContains(catalog, nameof(Probes.EnableM01OwnedPagingProbe), ProbeRisk.Destructive, enabled: false);
    }

    private static bool TestProbeCatalogReportsDestinationCapacity()
    {
        ProbeDefinition[] shortCatalog = new ProbeDefinition[Probes.ProbeCatalogCount - 1];
        ProbeCatalogCopyResult shortResult = Probes.CopyProbeCatalog(shortCatalog);
        ProbeCatalogCopyResult defaultResult = default;

        return !shortResult.Succeeded
            && shortResult.Status == ProbeCatalogStatus.DestinationTooSmall
            && shortResult.RequiredCount == Probes.ProbeCatalogCount
            && shortResult.AvailableCount == shortCatalog.Length
            && shortResult.WrittenCount == 0
            && !defaultResult.Succeeded
            && !Probes.TryGetProbe(-1, out _)
            && !Probes.TryGetProbe(Probes.ProbeCatalogCount, out _);
    }

    private static bool TestProbeCatalogFormatterFormatsAllDefinitions()
    {
        ProbeDefinition[] catalog = new ProbeDefinition[Probes.ProbeCatalogCount];
        ProbeCatalogCopyResult copy = Probes.CopyProbeCatalog(catalog);
        if (!copy.Succeeded)
        {
            return false;
        }

        ProbeCatalogDiagnosticFormatResult query =
            ProbeCatalogDiagnosticFormatter.QueryCatalogRequiredBytes(catalog);
        if (query.Status != ProbeCatalogDiagnosticFormatStatus.Success ||
            query.RequiredBytes <= 0)
        {
            return false;
        }

        byte[] destination = new byte[query.RequiredBytes];
        ProbeCatalogDiagnosticFormatResult format =
            ProbeCatalogDiagnosticFormatter.FormatCatalog(catalog, destination);
        if (!format.Succeeded ||
            format.BytesWritten != query.RequiredBytes ||
            format.DefinitionsFormatted != Probes.ProbeCatalogCount)
        {
            return false;
        }

        string formatted = Encoding.ASCII.GetString(destination, 0, format.BytesWritten);
        if (CountOccurrences(formatted, "PROBE_CATALOG name=") != Probes.ProbeCatalogCount ||
            !formatted.Contains(
                "PROBE_CATALOG name=EmitBootSmokeMarkers group=boot.smoke enabled=true risk=safe requires=none destructive=false",
                StringComparison.Ordinal) ||
            !formatted.Contains(
                "PROBE_CATALOG name=EnableUefiGoneSimulationProbe group=efi.smoke enabled=false risk=boundary-simulation requires=boundary-simulation destructive=false",
                StringComparison.Ordinal))
        {
            return false;
        }

        for (int index = 0; index < catalog.Length; index++)
        {
            if (!FormattedCatalogContainsDefinition(formatted, in catalog[index]))
            {
                return false;
            }
        }

        return ProbeCatalogDiagnosticFormatter.UsesSharedAsciiTextBuilder &&
            ProbeCatalogDiagnosticFormatter.UsesCallerProvidedDestination &&
            !ProbeCatalogDiagnosticFormatter.AllocatesManagedStorage &&
            !ProbeCatalogDiagnosticFormatter.ExecutesProbes &&
            !ProbeCatalogDiagnosticFormatter.PerformsLiveDeviceIo &&
            !ProbeCatalogDiagnosticFormatter.StartsKestrel;
    }

    private static bool TestProbeCatalogFormatterReportsDestinationCapacity()
    {
        ProbeDefinition definition = Probes.GetBootSmokeProbe();
        ProbeCatalogDiagnosticFormatResult query =
            ProbeCatalogDiagnosticFormatter.QueryDefinitionRequiredBytes(in definition);
        if (query.Status != ProbeCatalogDiagnosticFormatStatus.Success ||
            query.RequiredBytes <= 1)
        {
            return false;
        }

        byte[] shortDefinitionDestination = new byte[query.RequiredBytes - 1];
        ProbeCatalogDiagnosticFormatResult shortDefinition =
            ProbeCatalogDiagnosticFormatter.FormatDefinition(
                in definition,
                shortDefinitionDestination);

        ProbeDefinition[] catalog = new ProbeDefinition[Probes.ProbeCatalogCount];
        ProbeCatalogCopyResult copy = Probes.CopyProbeCatalog(catalog);
        ProbeCatalogDiagnosticFormatResult catalogQuery =
            ProbeCatalogDiagnosticFormatter.QueryCatalogRequiredBytes(catalog);
        byte[] shortCatalogDestination = new byte[catalogQuery.RequiredBytes - 1];
        ProbeCatalogDiagnosticFormatResult shortCatalog =
            ProbeCatalogDiagnosticFormatter.FormatCatalog(
                catalog,
                shortCatalogDestination);

        ProbeDefinition defaultDefinition = default;
        ProbeCatalogDiagnosticFormatResult defaultQuery =
            ProbeCatalogDiagnosticFormatter.QueryDefinitionRequiredBytes(in defaultDefinition);
        ProbeCatalogDiagnosticFormatResult defaultFormat =
            ProbeCatalogDiagnosticFormatter.FormatDefinition(
                in defaultDefinition,
                stackalloc byte[128]);
        ProbeCatalogDiagnosticFormatResult defaultResult = default;

        return copy.Succeeded
            && shortDefinition.Status == ProbeCatalogDiagnosticFormatStatus.DestinationTooSmall
            && shortDefinition.RequiredBytes == query.RequiredBytes
            && shortDefinition.AvailableBytes == shortDefinitionDestination.Length
            && shortDefinition.BytesWritten == 0
            && catalogQuery.Status == ProbeCatalogDiagnosticFormatStatus.Success
            && shortCatalog.Status == ProbeCatalogDiagnosticFormatStatus.DestinationTooSmall
            && shortCatalog.RequiredBytes == catalogQuery.RequiredBytes
            && shortCatalog.AvailableBytes == shortCatalogDestination.Length
            && shortCatalog.DefinitionsFormatted == 0
            && defaultQuery.Status == ProbeCatalogDiagnosticFormatStatus.InvalidDefinition
            && defaultFormat.Status == ProbeCatalogDiagnosticFormatStatus.InvalidDefinition
            && !defaultResult.Succeeded;
    }

    private static bool TestProbeCatalogFormatterKeepsDangerousProbesDisabled()
    {
        if (!TryFormatProbeCatalogForTest(out string formatted))
        {
            return false;
        }

        string[] lines = formatted.Split("\r\n", StringSplitOptions.None);
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            if ((line.Contains("risk=destructive", StringComparison.Ordinal) ||
                line.Contains("risk=device-io", StringComparison.Ordinal) ||
                line.Contains("risk=boundary-simulation", StringComparison.Ordinal)) &&
                !line.Contains("enabled=false", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return formatted.Contains(
                "PROBE_CATALOG name=EnableNullPointerPanicProbe group=panic.smoke enabled=false risk=destructive requires=destructive-probe destructive=true",
                StringComparison.Ordinal)
            && formatted.Contains(
                "PROBE_CATALOG name=EnableInvalidOpcodePanicProbe group=panic.smoke enabled=false risk=destructive requires=destructive-probe destructive=true",
                StringComparison.Ordinal)
            && formatted.Contains(
                "PROBE_CATALOG name=EnableVirtioBlockReadProbe group=storage.smoke enabled=false risk=device-io requires=device-io destructive=false",
                StringComparison.Ordinal)
            && formatted.Contains(
                "PROBE_CATALOG name=EnableFetchAppHttpTcpExchangeProbe group=network.smoke enabled=false risk=device-io requires=device-io destructive=false",
                StringComparison.Ordinal)
            && formatted.Contains(
                "PROBE_CATALOG name=EnableM01LoadedImageStackProbe group=m01.live-memory enabled=false risk=destructive requires=destructive-probe destructive=true",
                StringComparison.Ordinal)
            && formatted.Contains(
                "PROBE_CATALOG name=EnableM01LiveAllocatorProbe group=m01.live-memory enabled=false risk=destructive requires=destructive-probe destructive=true",
                StringComparison.Ordinal)
            && formatted.Contains(
                "PROBE_CATALOG name=EnableM01OwnedPagingProbe group=m01.live-memory enabled=false risk=destructive requires=destructive-probe destructive=true",
                StringComparison.Ordinal);
    }

    private static bool CatalogContains(
        ReadOnlySpan<ProbeDefinition> catalog,
        string name,
        ProbeRisk risk,
        bool enabled)
    {
        for (int i = 0; i < catalog.Length; i++)
        {
            ProbeDefinition definition = catalog[i];
            if (definition.Name == name &&
                definition.Risk == risk &&
                definition.EnabledByDefault == enabled)
            {
                return true;
            }
        }

        return false;
    }

    private static int CatalogNameCount(
        ReadOnlySpan<ProbeDefinition> catalog,
        string name)
    {
        int count = 0;
        for (int index = 0; index < catalog.Length; index++)
        {
            if (catalog[index].Name == name)
                count++;
        }

        return count;
    }

    private static bool FormattedCatalogContainsDefinition(
        string formattedCatalog,
        in ProbeDefinition definition)
    {
        ProbeCatalogDiagnosticFormatResult query =
            ProbeCatalogDiagnosticFormatter.QueryDefinitionRequiredBytes(in definition);
        if (query.Status != ProbeCatalogDiagnosticFormatStatus.Success)
        {
            return false;
        }

        byte[] destination = new byte[query.RequiredBytes];
        ProbeCatalogDiagnosticFormatResult format =
            ProbeCatalogDiagnosticFormatter.FormatDefinition(in definition, destination);
        if (!format.Succeeded)
        {
            return false;
        }

        string formattedDefinition = Encoding.ASCII.GetString(destination, 0, format.BytesWritten);
        return formattedCatalog.Contains(formattedDefinition, StringComparison.Ordinal);
    }

    private static bool TryFormatProbeCatalogForTest(out string formatted)
    {
        ProbeDefinition[] catalog = new ProbeDefinition[Probes.ProbeCatalogCount];
        ProbeCatalogCopyResult copy = Probes.CopyProbeCatalog(catalog);
        ProbeCatalogDiagnosticFormatResult query =
            ProbeCatalogDiagnosticFormatter.QueryCatalogRequiredBytes(catalog);
        if (!copy.Succeeded ||
            query.Status != ProbeCatalogDiagnosticFormatStatus.Success)
        {
            formatted = string.Empty;
            return false;
        }

        byte[] destination = new byte[query.RequiredBytes];
        ProbeCatalogDiagnosticFormatResult format =
            ProbeCatalogDiagnosticFormatter.FormatCatalog(catalog, destination);
        if (!format.Succeeded)
        {
            formatted = string.Empty;
            return false;
        }

        formatted = Encoding.ASCII.GetString(destination, 0, format.BytesWritten);
        return true;
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;

        while (offset < source.Length)
        {
            int index = source.IndexOf(value, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            offset = index + value.Length;
        }

        return count;
    }

    private static CpuFaultFrame CreateFrame(int vector, bool hasErrorCode, ulong errorCode, ulong cr2)
    {
        return new CpuFaultFrame(
            vector,
            hasErrorCode,
            errorCode,
            rip: 0x0000000010002000,
            rsp: 0x0000000000700000,
            rbp: 0x0000000000700100,
            rflags: 0x0000000000000202,
            cr2,
            cr3: 0x00000000001AA000,
            rax: 0x1111111111111111,
            rbx: 0x2222222222222222,
            rcx: 0x3333333333333333,
            rdx: 0x4444444444444444,
            rsi: 0x5555555555555555,
            rdi: 0x6666666666666666,
            r8: 0x8888888888888888,
            r9: 0x9999999999999999,
            r10: 0xAAAAAAAAAAAAAAAA,
            r11: 0xBBBBBBBBBBBBBBBB,
            r12: 0xCCCCCCCCCCCCCCCC,
            r13: 0xDDDDDDDDDDDDDDDD,
            r14: 0xEEEEEEEEEEEEEEEE,
            r15: 0xFFFFFFFFFFFFFFFF);
    }

    private sealed class DiagnosticTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count
        {
            get { return _lines.Count; }
        }

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }
    }
}
