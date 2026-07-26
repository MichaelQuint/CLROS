using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
using OS.Kernel.Runtime;

internal static class BootSequencePlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("boot.sequence plan exposes ordered marker bytes", TestOrderedMarkerBytes, ref failures);
        Run("boot.sequence M01 stage-one plan exposes exact success bytes", TestM01StageOneSuccessMarkerBytes, ref failures);
        Run("boot.sequence M01 stage-one plan preserves default bytes", TestM01StageOnePlanPreservesDefaultBytes, ref failures);
        Run("boot.sequence M01 stage-one plan preserves EFI linkage boundary", TestM01StageOnePlanPreservesEfiLinkageBoundary, ref failures);
        Run("boot.phase catalog documents implemented phases", TestBootPhaseCatalog, ref failures);
        Run("boot.phase catalog marker text matches plan", TestBootPhaseMarkersMatchPlan, ref failures);
        Run("boot.sequence run emits plan lines in order", TestBootSequenceUsesPlan, ref failures);
        Run("boot.sequence validates handoff and emits ready marker", TestBootSequenceHandoffReadyMarker, ref failures);
        Run("boot.sequence rejects invalid handoff before phases", TestBootSequenceRejectsInvalidHandoff, ref failures);
        Run("boot.sequence handoff validator rejects source capability and count issues", TestBootSequenceHandoffValidatorFailures, ref failures);
        Run("boot.sequence handoff validator validates ACPI RSDP metadata", TestBootSequenceHandoffValidatorAcpiRsdp, ref failures);
        Run("boot.sequence memory map accepted", TestBootSequenceMemoryMapAccepted, ref failures);
        Run("boot.sequence memory map missing rejected", TestBootSequenceMemoryMapMissingRejected, ref failures);
        Run("boot.sequence memory map invalid rejected", TestBootSequenceMemoryMapInvalidRejected, ref failures);
        Run("boot.sequence fault infra readiness emits ready marker", TestFaultInfrastructureReadyMarker, ref failures);
        Run("boot.sequence fault infra readiness emits failure marker", TestFaultInfrastructureFailureMarker, ref failures);
        Run("boot.sequence default markers unchanged by fault readiness helper", TestDefaultMarkersUnchangedByFaultReadinessHelper, ref failures);
        Run("boot.sequence exitboot transition emits ready marker", TestExitBootServicesTransitionReadyMarker, ref failures);
        Run("boot.sequence exitboot transition blocks non-success plan", TestExitBootServicesTransitionBlockedMarker, ref failures);
        Run("boot.sequence exitboot transition leaves default markers unchanged", TestDefaultMarkersUnchangedByExitBootServicesHelper, ref failures);
        Run("boot.sequence exitboot preflight emits ready marker", TestExitBootServicesPreflightReadyMarker, ref failures);
        Run("boot.sequence exitboot preflight blocks missing post-firmware diagnostics", TestExitBootServicesPreflightBlocksMissingDiagnostics, ref failures);
        Run("boot.sequence exitboot preflight blocks non-success transition plan", TestExitBootServicesPreflightBlocksNonSuccessPlan, ref failures);
        Run("boot.sequence exitboot preflight leaves default markers unchanged", TestDefaultMarkersUnchangedByExitBootServicesPreflight, ref failures);
        Run("boot.sequence early diagnostics readiness emits ready marker", TestEarlyDiagnosticsReadyMarker, ref failures);
        Run("boot.sequence early diagnostics readiness rejects serial backend failure", TestEarlyDiagnosticsRejectsSerialBackendFailure, ref failures);
        Run("boot.sequence early diagnostics readiness rejects descriptor readiness failure", TestEarlyDiagnosticsRejectsDescriptorReadinessFailure, ref failures);
        Run("boot.sequence early diagnostics readiness leaves default markers unchanged", TestDefaultMarkersUnchangedByEarlyDiagnosticsHelper, ref failures);
        Run("boot.sequence diagnostic stubs materialized emits ready marker", TestDiagnosticStubMaterializationReadyMarker, ref failures);
        Run("boot.sequence diagnostic stubs materialized rejects materializer failure", TestDiagnosticStubMaterializationRejectsMaterializerFailure, ref failures);
        Run("boot.sequence diagnostic stubs materialized leaves default markers unchanged", TestDefaultMarkersUnchangedByDiagnosticStubMaterializationHelper, ref failures);
        Run("boot.sequence post-firmware diagnostics readiness emits ready marker", TestPostFirmwareDiagnosticsReadyMarker, ref failures);
        Run("boot.sequence post-firmware diagnostics readiness blocks firmware-console-only path", TestPostFirmwareDiagnosticsBlocksFirmwareConsoleOnly, ref failures);
        Run("boot.sequence post-firmware diagnostics readiness blocks firmware-services-unavailable path", TestPostFirmwareDiagnosticsBlocksFirmwareServicesUnavailable, ref failures);
        Run("boot.sequence post-firmware diagnostics readiness leaves default markers unchanged", TestDefaultMarkersUnchangedByPostFirmwareDiagnosticsHelper, ref failures);
        Run("boot.sequence controlled fault probes emit ready marker", TestControlledFaultProbesReadyMarker, ref failures);
        Run("boot.sequence controlled fault probes reject early diagnostics failure", TestControlledFaultProbesRejectEarlyDiagnosticsFailure, ref failures);
        Run("boot.sequence controlled fault probes reject probe readiness failure", TestControlledFaultProbesRejectProbeReadinessFailure, ref failures);
        Run("boot.sequence controlled fault probes leave default markers unchanged", TestDefaultMarkersUnchangedByControlledFaultProbeHelper, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Boot sequence plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestOrderedMarkerBytes()
    {
        string[] expected =
        [
            "TEST_START boot.sequence",
            "BOOT_PHASE PhaseA.Audit START",
            "BOOT_PHASE PhaseA.Audit PASS",
            "BOOT_PHASE PhaseA.Diagnostics START",
            "BOOT_PHASE PhaseA.Diagnostics PASS",
            SerialSmokeMarkers.BootOk,
            SerialSmokeMarkers.TestPass + " boot.sequence",
            SerialSmokeMarkers.OsSmokePass
        ];

        if (BootSequencePlan.NonSmokeLineCount != 5 || BootSequencePlan.LineCount != expected.Length)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            if (BootSequence.CreatePlanLine(lineIndex) != expected[lineIndex])
            {
                return false;
            }
        }

        return BootSequence.CreatePlanLine(BootSequencePlan.LineCount).Length == 0;
    }

    private static bool TestM01StageOneSuccessMarkerBytes()
    {
        string[] expected =
        [
            "TEST_START m01.stage1",
            "M01_LOADED_IMAGE_CAPTURED",
            "M01_STACK_SWITCH_STUB_READY",
            "M01_STACK_ARENA_ALLOCATED",
            "M01_STACK_GUARD_RESERVED",
            "M01_STAGE1_MAP_RECAPTURED",
            "M01_STAGE1_RESERVATIONS_VALIDATED",
            "M01_OWNED_STACK_READY",
            "M01_OWNED_STACK_SWITCHED",
            "M01_RSP_CONTAINED",
            "TEST_PASS m01.stage1"
        ];

        if (BootSequencePlan.M01StageOneSuccessLineCount != expected.Length)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            string expectedLine = expected[lineIndex];
            if (BootSequencePlan.GetM01StageOneSuccessLineLength(lineIndex) != expectedLine.Length
                || BootSequencePlan.GetM01StageOneSuccessLineByte(lineIndex, -1) != 0)
            {
                return false;
            }

            for (int offset = 0; offset < expectedLine.Length; offset++)
            {
                if (BootSequencePlan.GetM01StageOneSuccessLineByte(lineIndex, offset)
                    != (byte)expectedLine[offset])
                {
                    return false;
                }
            }

            if (BootSequencePlan.GetM01StageOneSuccessLineByte(lineIndex, expectedLine.Length) != 0)
            {
                return false;
            }
        }

        return BootSequencePlan.GetM01StageOneSuccessLineLength(-1) == 0
            && BootSequencePlan.GetM01StageOneSuccessLineLength(expected.Length) == 0
            && BootSequencePlan.GetM01StageOneSuccessLineByte(-1, 0) == 0
            && BootSequencePlan.GetM01StageOneSuccessLineByte(expected.Length, 0) == 0;
    }

    private static bool TestM01StageOnePlanPreservesDefaultBytes()
    {
        string[] expected =
        [
            "TEST_START boot.sequence",
            "BOOT_PHASE PhaseA.Audit START",
            "BOOT_PHASE PhaseA.Audit PASS",
            "BOOT_PHASE PhaseA.Diagnostics START",
            "BOOT_PHASE PhaseA.Diagnostics PASS",
            "OS_BOOT_OK",
            "TEST_PASS boot.sequence",
            "OS_SMOKE_PASS"
        ];

        if (BootSequencePlan.NonSmokeLineCount != 5
            || BootSequencePlan.LineCount != expected.Length)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < expected.Length; lineIndex++)
        {
            string expectedLine = expected[lineIndex];
            if (BootSequencePlan.GetLineLength(lineIndex) != expectedLine.Length)
            {
                return false;
            }

            for (int offset = 0; offset < expectedLine.Length; offset++)
            {
                if (BootSequencePlan.GetLineByte(lineIndex, offset) != (byte)expectedLine[offset])
                {
                    return false;
                }
            }
        }

        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);

        return result.Succeeded
            && serial.Count == expected.Length
            && !serial.Contains("TEST_START m01.stage1")
            && !serial.Contains("TEST_PASS m01.stage1")
            && !serial.ContainsPrefix("M01_");
    }

    private static bool TestM01StageOnePlanPreservesEfiLinkageBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(repositoryRoot, "src", "OS", "OS.csproj");
        System.Xml.Linq.XDocument project = System.Xml.Linq.XDocument.Load(projectPath);

        bool defaultCompileItemsDisabled = project
            .Descendants("EnableDefaultCompileItems")
            .Any(element => string.Equals(element.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase));
        bool planLinked = false;
        bool integrationLinked = false;

        foreach (System.Xml.Linq.XElement compile in project.Descendants("Compile"))
        {
            string? include = compile.Attribute("Include")?.Value;
            if (string.IsNullOrWhiteSpace(include))
            {
                continue;
            }

            string normalized = include.Replace('/', '\\');
            if (normalized.Contains('*'))
            {
                return false;
            }

            if (string.Equals(
                Path.GetFileName(normalized),
                "BootSequence.cs",
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(
                Path.GetFileName(normalized),
                "M01LoadedImageStackIntegration.cs",
                StringComparison.OrdinalIgnoreCase))
            {
                integrationLinked = true;
            }

            if (string.Equals(
                normalized,
                @"..\OS.Kernel\Boot\BootSequencePlan.cs",
                StringComparison.OrdinalIgnoreCase))
            {
                planLinked = true;
            }
        }

        string efiEntryPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "Boot",
            "EfiEntry.cs");
        string efiEntrySource = File.ReadAllText(efiEntryPath);

        return defaultCompileItemsDisabled
            && planLinked
            && !integrationLinked
            && !efiEntrySource.Contains(
                "RunM01LoadedImageStackIntegration",
                StringComparison.Ordinal)
            && !efiEntrySource.Contains(
                "M01LoadedImageStackIntegration.Prepare",
                StringComparison.Ordinal);
    }

    private static bool TestBootSequenceUsesPlan()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        string[] eventSnapshot = events.Snapshot();
        return eventSnapshot.Length == BootSequencePlan.LineCount
            && eventSnapshot[0] == BootSequence.CreatePlanLine(0)
            && eventSnapshot[^1] == BootSequence.CreatePlanLine(BootSequencePlan.LineCount - 1);
    }

    private static bool TestBootSequenceHandoffReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(CreateValidOwnedHandoffRecord(), serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount + 1)
        {
            return false;
        }

        string[] eventSnapshot = events.Snapshot();
        return serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == BootSequenceHandoffValidator.ReadyMarker
            && serial.LineAt(2) == BootSequence.CreatePlanLine(1)
            && serial.LineAt(serial.Count - 1) == SerialSmokeMarkers.OsSmokePass
            && eventSnapshot.Length == serial.Count
            && eventSnapshot[1] == BootSequenceHandoffValidator.ReadyMarker;
    }

    private static bool TestBootSequenceRejectsInvalidHandoff()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootHandoffRecord record = CreateValidOwnedHandoffRecord();
        record.MemoryMap.Flags = 0;

        BootResult result = BootSequence.Run(record, serial, events);
        string expectedFailMarker = BootSequenceHandoffValidator.CreateFailMarker(
            BootSequenceHandoffStatus.MemoryMapNotOwned);

        return !result.Succeeded
            && result.Reason.Contains(nameof(BootSequenceHandoffStatus.MemoryMapNotOwned), StringComparison.Ordinal)
            && serial.Count == 2
            && serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == expectedFailMarker;
    }

    private static bool TestBootSequenceHandoffValidatorFailures()
    {
        BootHandoffRecord missingCapability = CreateValidOwnedHandoffRecord();
        missingCapability.Capabilities = BootHandoffRecord.CapabilityTextOutput;

        BootHandoffRecord sourceMatchesOwned = CreateValidOwnedHandoffRecord();
        sourceMatchesOwned.MemoryMap.SourceBufferAddress = sourceMatchesOwned.MemoryMap.BufferAddress;

        BootHandoffRecord tooManyDescriptors = CreateValidOwnedHandoffRecord();
        tooManyDescriptors.MemoryMap.DescriptorCount = BootSequenceHandoffValidator.MaximumDescriptorCount + 1;
        tooManyDescriptors.MemoryMap.MapSize =
            tooManyDescriptors.MemoryMap.DescriptorCount * tooManyDescriptors.MemoryMap.DescriptorSize;
        tooManyDescriptors.MemoryMap.BufferSize = tooManyDescriptors.MemoryMap.MapSize;
        tooManyDescriptors.MemoryMap.SourceBufferSize = tooManyDescriptors.MemoryMap.MapSize;

        BootSequenceHandoffValidationResult missingCapabilityResult =
            BootSequence.ValidateHandoff(missingCapability);
        BootSequenceHandoffValidationResult sourceMatchesOwnedResult =
            BootSequence.ValidateHandoff(sourceMatchesOwned);
        BootSequenceHandoffValidationResult tooManyDescriptorsResult =
            BootSequence.ValidateHandoff(tooManyDescriptors);

        return !missingCapabilityResult.Succeeded
            && missingCapabilityResult.Status == BootSequenceHandoffStatus.MissingRequiredCapability
            && missingCapabilityResult.MissingCapabilities == BootCapabilities.MemoryMap
            && !sourceMatchesOwnedResult.Succeeded
            && sourceMatchesOwnedResult.Status == BootSequenceHandoffStatus.SourceMatchesOwnedBuffer
            && !tooManyDescriptorsResult.Succeeded
            && tooManyDescriptorsResult.Status == BootSequenceHandoffStatus.InvalidDescriptorCount;
    }

    private static bool TestBootSequenceHandoffValidatorAcpiRsdp()
    {
        BootHandoffRecord validAcpi = CreateValidOwnedHandoffRecord();
        validAcpi.SetAcpiRsdp(
            0x7fe00000,
            FirmwareAcpiRsdpRecord.TableKindAcpi20,
            FirmwareAcpiRsdpRecord.ScanStatusOk);

        BootHandoffRecord invalidAcpi = CreateValidOwnedHandoffRecord();
        invalidAcpi.Capabilities |= BootHandoffRecord.CapabilityAcpiRsdp;
        invalidAcpi.AcpiRsdp.Flags = FirmwareAcpiRsdpRecord.FlagPresent;
        invalidAcpi.AcpiRsdp.TableKind = FirmwareAcpiRsdpRecord.TableKindAcpi20;
        invalidAcpi.AcpiRsdp.ScanStatus = FirmwareAcpiRsdpRecord.ScanStatusOk;

        BootSequenceHandoffValidationResult validResult = BootSequence.ValidateHandoff(validAcpi);
        BootSequenceHandoffValidationResult invalidResult = BootSequence.ValidateHandoff(invalidAcpi);

        return validResult.Succeeded
            && validResult.Status == BootSequenceHandoffStatus.Success
            && invalidResult.Status == BootSequenceHandoffStatus.InvalidAcpiRsdp;
    }

    private static bool TestBootSequenceMemoryMapAccepted()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(
            CreateValidOwnedHandoffRecord(),
            CreateValidMemoryMapSummary(),
            serial,
            events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount + 2)
        {
            return false;
        }

        string[] eventSnapshot = events.Snapshot();
        return serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == BootSequenceHandoffValidator.ReadyMarker
            && serial.LineAt(2) == BootSequence.BootMemoryMapAccepted
            && serial.LineAt(3) == BootSequence.CreatePlanLine(1)
            && serial.LineAt(serial.Count - 1) == SerialSmokeMarkers.OsSmokePass
            && eventSnapshot.Length == serial.Count
            && eventSnapshot[2] == BootSequence.BootMemoryMapAccepted;
    }

    private static bool TestBootSequenceMemoryMapMissingRejected()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(
            CreateValidOwnedHandoffRecord(),
            default,
            serial,
            events);

        string expectedFailMarker = BootSequence.CreateMemoryMapRejectedMarker(
            BootMemoryMapAcceptanceStatus.MissingSummary);

        return !result.Succeeded
            && result.Reason.Contains(nameof(BootMemoryMapAcceptanceStatus.MissingSummary), StringComparison.Ordinal)
            && serial.Count == 3
            && serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == BootSequenceHandoffValidator.ReadyMarker
            && serial.LineAt(2) == expectedFailMarker;
    }

    private static bool TestBootSequenceMemoryMapInvalidRejected()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        EfiMemoryMapSummary invalidSummary = new EfiMemoryMapSummary(
            descriptorCount: 3,
            totalPages: 0,
            totalBytes: 0,
            conventionalPages: 0,
            lowestPhysicalAddress: 0x100000,
            highestPhysicalEndExclusive: 0x200000);

        BootResult result = BootSequence.Run(
            CreateValidOwnedHandoffRecord(),
            invalidSummary,
            serial,
            events);

        string expectedFailMarker = BootSequence.CreateMemoryMapRejectedMarker(
            BootMemoryMapAcceptanceStatus.ZeroPages);

        return !result.Succeeded
            && result.Reason.Contains(nameof(BootMemoryMapAcceptanceStatus.ZeroPages), StringComparison.Ordinal)
            && serial.Count == 3
            && serial.LineAt(0) == BootSequence.CreatePlanLine(0)
            && serial.LineAt(1) == BootSequenceHandoffValidator.ReadyMarker
            && serial.LineAt(2) == expectedFailMarker;
    }

    private static bool TestFaultInfrastructureReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunFaultInfrastructureReadiness(
            CreateReadyFaultInfrastructureResult(),
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.FaultInfrastructureReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.FaultInfrastructureReady;
    }

    private static bool TestFaultInfrastructureFailureMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        FaultInfrastructureReadinessResult readiness =
            FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
                serialDiagnosticsReady: false,
                idtTableReady: true,
                IdtTablePlanStatus.Ok,
                idtrReady: true,
                IdtrDescriptorStatus.Ok,
                gdtReady: true,
                tssIstReady: true,
                doubleFaultIstReady: true);

        BootResult result = BootSequence.RunFaultInfrastructureReadiness(readiness, serial, events);
        string expectedMarker = BootSequence.CreateFaultInfrastructureNotReadyMarker(
            FaultInfrastructureReadinessStatus.SerialDiagnosticsNotReady);
        string[] eventSnapshot = events.Snapshot();

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(FaultInfrastructureReadinessStatus.SerialDiagnosticsNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker;
    }

    private static bool TestDefaultMarkersUnchangedByFaultReadinessHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.FaultInfrastructureReady)
            && !serial.ContainsPrefix(BootSequence.FaultInfrastructureNotReadyPrefix);
    }

    private static bool TestExitBootServicesTransitionReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        BootResult result = BootSequence.RunExitBootServicesTransition(
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
            firmware,
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return result.Succeeded
            && firmware.Mode == FirmwareServicesMode.BootServicesExited
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.ExitBootServicesTransitionReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.ExitBootServicesTransitionReady;
    }

    private static bool TestExitBootServicesTransitionBlockedMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        BootResult result = BootSequence.RunExitBootServicesTransition(
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.RetryRequired),
            firmware,
            serial,
            events);
        string expectedMarker = BootSequence.CreateExitBootServicesTransitionBlockedMarker(
            UefiExitBootServicesPlanStatus.RetryRequired);
        string[] eventSnapshot = events.Snapshot();

        return !result.Succeeded
            && result.Reason.Contains(nameof(UefiExitBootServicesPlanStatus.RetryRequired), StringComparison.Ordinal)
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker;
    }

    private static bool TestDefaultMarkersUnchangedByExitBootServicesHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.ExitBootServicesTransitionReady)
            && !serial.ContainsPrefix(BootSequence.ExitBootServicesTransitionBlockedPrefix);
    }

    private static bool TestExitBootServicesPreflightReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        BootResult result = BootSequence.RunExitBootServicesPreflight(
            CreateReadyPostFirmwareDiagnosticsResult(),
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
            firmware,
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return result.Succeeded
            && firmware.Mode == FirmwareServicesMode.BootServicesExited
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.ExitBootServicesPreflightReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.ExitBootServicesPreflightReady;
    }

    private static bool TestExitBootServicesPreflightBlocksMissingDiagnostics()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        SerialDiagnosticsGuard guard = SerialDiagnosticsGuard.CreateFirmwareConsoleOnly();
        SerialDiagnosticsGuardResult diagnostics =
            guard.RequirePostFirmwareDiagnostics("exitboot.preflight");

        BootResult result = BootSequence.RunExitBootServicesPreflight(
            diagnostics,
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
            firmware,
            serial,
            events);
        string expectedMarker = BootSequence.CreateExitBootServicesPreflightBlockedMarker(
            SerialDiagnosticsGuardStatus.FirmwareConsoleOnly,
            UefiExitBootServicesPlanStatus.Success);
        string[] eventSnapshot = events.Snapshot();

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(SerialDiagnosticsGuardStatus.FirmwareConsoleOnly),
                StringComparison.Ordinal)
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker;
    }

    private static bool TestExitBootServicesPreflightBlocksNonSuccessPlan()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        BootResult result = BootSequence.RunExitBootServicesPreflight(
            CreateReadyPostFirmwareDiagnosticsResult(),
            CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.RetryRequired),
            firmware,
            serial,
            events);
        string expectedMarker = BootSequence.CreateExitBootServicesPreflightBlockedMarker(
            SerialDiagnosticsGuardStatus.OwnedSerialReady,
            UefiExitBootServicesPlanStatus.RetryRequired);
        string[] eventSnapshot = events.Snapshot();

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(UefiExitBootServicesPlanStatus.RetryRequired),
                StringComparison.Ordinal)
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker;
    }

    private static bool TestDefaultMarkersUnchangedByExitBootServicesPreflight()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.ExitBootServicesPreflightReady)
            && !serial.ContainsPrefix(BootSequence.ExitBootServicesPreflightBlockedPrefix);
    }

    private static bool TestEarlyDiagnosticsReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunEarlyDiagnosticsReadiness(
            CreateReadyCom1BackendResult(),
            CreateReadyDescriptorInstallResult(),
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.EarlyDiagnosticsReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.EarlyDiagnosticsReady;
    }

    private static bool TestEarlyDiagnosticsRejectsSerialBackendFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunEarlyDiagnosticsReadiness(
            Com1PortIoBackendPlanResult.InvalidSerialConfiguration(
                SerialPortConfigurationStatus.InvalidBasePort,
                Com1PortIoBackendPlan.RequiredExecutableStubBytes,
                availableStubBytes: 0),
            CreateReadyDescriptorInstallResult(),
            serial,
            events);
        string expectedMarker = BootSequence.CreateEarlyDiagnosticsNotReadyMarker(
            EarlyDiagnosticsReadinessStatus.SerialBackendNotReady);

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(EarlyDiagnosticsReadinessStatus.SerialBackendNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker;
    }

    private static bool TestEarlyDiagnosticsRejectsDescriptorReadinessFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunEarlyDiagnosticsReadiness(
            CreateReadyCom1BackendResult(),
            DescriptorTableInstallReadinessResult.Fail(
                DescriptorTableInstallReadinessStatus.IdtTableNotReady,
                IdtTablePlanStatus.MissingRequiredVector,
                IdtrDescriptorStatus.Ok,
                GdtDescriptorPlanStatus.Ok,
                TssIstPlanStatus.Ok),
            serial,
            events);
        string expectedMarker = BootSequence.CreateEarlyDiagnosticsNotReadyMarker(
            EarlyDiagnosticsReadinessStatus.DescriptorTablesNotReady);

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(EarlyDiagnosticsReadinessStatus.DescriptorTablesNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker;
    }

    private static bool TestDefaultMarkersUnchangedByEarlyDiagnosticsHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.EarlyDiagnosticsReady)
            && !serial.ContainsPrefix(BootSequence.EarlyDiagnosticsNotReadyPrefix);
    }

    private static bool TestDiagnosticStubMaterializationReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunDiagnosticStubMaterializationReadiness(
            ExecutableStubBundleMaterializerResult.Success(),
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.DiagnosticStubsMaterialized
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.DiagnosticStubsMaterialized;
    }

    private static bool TestDiagnosticStubMaterializationRejectsMaterializerFailure()
    {
        SequenceTestSerialSink failedSerial = new SequenceTestSerialSink();
        BootEventRingBuffer failedEvents = new BootEventRingBuffer(16);

        ExecutableStubBundleMaterializerResult failedMaterializer =
            ExecutableStubBundleMaterializerResult.Fail(
                ExecutableStubBundleMaterializerStatus.InvalidBundlePlan,
                ExecutableStubBundlePlanStatus.InvalidMemoryPlan,
                ExecutableStubMemoryPlanStatus.Success);

        BootResult failedResult = BootSequence.RunDiagnosticStubMaterializationReadiness(
            failedMaterializer,
            failedSerial,
            failedEvents);
        string expectedFailedMarker = BootSequence.CreateDiagnosticStubsNotMaterializedMarker(
            DiagnosticStubMaterializationReadinessStatus.MaterializerFailed);
        string[] failedEventSnapshot = failedEvents.Snapshot();

        SequenceTestSerialSink defaultSerial = new SequenceTestSerialSink();
        BootEventRingBuffer defaultEvents = new BootEventRingBuffer(16);

        BootResult defaultResult = BootSequence.RunDiagnosticStubMaterializationReadiness(
            default(ExecutableStubBundleMaterializerResult),
            defaultSerial,
            defaultEvents);
        string expectedDefaultMarker = BootSequence.CreateDiagnosticStubsNotMaterializedMarker(
            DiagnosticStubMaterializationReadinessStatus.MaterializerFailed);
        string[] defaultEventSnapshot = defaultEvents.Snapshot();

        return !failedResult.Succeeded
            && failedResult.Reason.Contains(
                nameof(DiagnosticStubMaterializationReadinessStatus.MaterializerFailed),
                StringComparison.Ordinal)
            && failedSerial.Count == 1
            && failedSerial.LineAt(0) == expectedFailedMarker
            && failedEventSnapshot.Length == 1
            && failedEventSnapshot[0] == expectedFailedMarker
            && !defaultResult.Succeeded
            && defaultResult.Reason.Contains(
                nameof(DiagnosticStubMaterializationReadinessStatus.MaterializerFailed),
                StringComparison.Ordinal)
            && defaultSerial.Count == 1
            && defaultSerial.LineAt(0) == expectedDefaultMarker
            && defaultEventSnapshot.Length == 1
            && defaultEventSnapshot[0] == expectedDefaultMarker;
    }

    private static bool TestDefaultMarkersUnchangedByDiagnosticStubMaterializationHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.DiagnosticStubsMaterialized)
            && !serial.ContainsPrefix(BootSequence.DiagnosticStubsNotMaterializedPrefix);
    }

    private static bool TestPostFirmwareDiagnosticsReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        guard.MarkOwnedSerialReady();
        SerialDiagnosticsGuardResult readiness =
            guard.RequirePostFirmwareDiagnostics("exitboot.diagnostics");

        BootResult result = BootSequence.RunPostFirmwareDiagnosticsReadiness(readiness, serial, events);
        string[] eventSnapshot = events.Snapshot();

        return result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.PostFirmwareDiagnosticsReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.PostFirmwareDiagnosticsReady;
    }

    private static bool TestPostFirmwareDiagnosticsBlocksFirmwareConsoleOnly()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        SerialDiagnosticsGuard guard = SerialDiagnosticsGuard.CreateFirmwareConsoleOnly();
        SerialDiagnosticsGuardResult readiness =
            guard.RequirePostFirmwareDiagnostics("exitboot.diagnostics");

        BootResult result = BootSequence.RunPostFirmwareDiagnosticsReadiness(readiness, serial, events);
        string expectedMarker = BootSequence.CreatePostFirmwareDiagnosticsBlockedMarker(
            SerialDiagnosticsGuardStatus.FirmwareConsoleOnly);
        string[] eventSnapshot = events.Snapshot();

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(SerialDiagnosticsGuardStatus.FirmwareConsoleOnly),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker;
    }

    private static bool TestPostFirmwareDiagnosticsBlocksFirmwareServicesUnavailable()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult readiness =
            guard.RequirePostFirmwareDiagnostics("exitboot.diagnostics");

        BootResult result = BootSequence.RunPostFirmwareDiagnosticsReadiness(readiness, serial, events);
        string expectedMarker = BootSequence.CreatePostFirmwareDiagnosticsBlockedMarker(
            SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable);
        string[] eventSnapshot = events.Snapshot();

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == expectedMarker;
    }

    private static bool TestDefaultMarkersUnchangedByPostFirmwareDiagnosticsHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.PostFirmwareDiagnosticsReady)
            && !serial.ContainsPrefix(BootSequence.PostFirmwareDiagnosticsBlockedPrefix);
    }

    private static bool TestControlledFaultProbesReadyMarker()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunControlledFaultProbeReadiness(
            EarlyDiagnosticsReadinessStatus.Ready,
            CreateReadyFaultProbeReadinessResult(),
            serial,
            events);

        string[] eventSnapshot = events.Snapshot();
        return result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == BootSequence.ControlledFaultProbesReady
            && eventSnapshot.Length == 1
            && eventSnapshot[0] == BootSequence.ControlledFaultProbesReady;
    }

    private static bool TestControlledFaultProbesRejectEarlyDiagnosticsFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunControlledFaultProbeReadiness(
            EarlyDiagnosticsReadinessStatus.SerialBackendNotReady,
            CreateReadyFaultProbeReadinessResult(),
            serial,
            events);
        string expectedMarker = BootSequence.CreateControlledFaultProbesNotReadyMarker(
            ControlledFaultProbeReadinessStatus.EarlyDiagnosticsNotReady);

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(ControlledFaultProbeReadinessStatus.EarlyDiagnosticsNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker;
    }

    private static bool TestControlledFaultProbesRejectProbeReadinessFailure()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.RunControlledFaultProbeReadiness(
            EarlyDiagnosticsReadinessStatus.Ready,
            FaultProbeReadinessResult.Fail(FaultProbeReadinessStatus.MemoryPlanTooSmall),
            serial,
            events);
        string expectedMarker = BootSequence.CreateControlledFaultProbesNotReadyMarker(
            ControlledFaultProbeReadinessStatus.FaultProbePlanNotReady);

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(ControlledFaultProbeReadinessStatus.FaultProbePlanNotReady),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker;
    }

    private static bool TestDefaultMarkersUnchangedByControlledFaultProbeHelper()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);

        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return false;
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return false;
            }
        }

        return !serial.Contains(BootSequence.ControlledFaultProbesReady)
            && !serial.ContainsPrefix(BootSequence.ControlledFaultProbesNotReadyPrefix);
    }

    private static bool TestBootPhaseCatalog()
    {
        BootPhaseInfo audit = BootPhaseCatalog.GetImplementedPhase(0);
        BootPhaseInfo diagnostics = BootPhaseCatalog.GetImplementedPhase(1);
        BootPhaseInfo futureRuntime = BootPhaseCatalog.Get(BootPhaseId.PhaseD_Runtime);

        return BootPhaseCatalog.ImplementedPhaseCount == 2
            && BootPhaseCatalog.KnownPhaseCount == 8
            && audit.Id == BootPhaseId.PhaseA_Audit
            && audit.Name == "PhaseA.Audit"
            && audit.MarkerPrefix == BootPhaseCatalog.PhaseMarkerPrefix
            && audit.Precondition.Length > 0
            && audit.Postcondition.Length > 0
            && diagnostics.Id == BootPhaseId.PhaseA_Diagnostics
            && diagnostics.Name == "PhaseA.Diagnostics"
            && diagnostics.Precondition.Length > 0
            && diagnostics.Postcondition.Length > 0
            && futureRuntime.Id == BootPhaseId.PhaseD_Runtime
            && futureRuntime.Precondition.Length > 0
            && futureRuntime.Postcondition.Length > 0;
    }

    private static bool TestBootPhaseMarkersMatchPlan()
    {
        BootPhaseInfo audit = BootPhaseCatalog.GetImplementedPhase(0);
        BootPhaseInfo diagnostics = BootPhaseCatalog.GetImplementedPhase(1);

        return audit.StartMarker == BootSequence.CreatePlanLine(1)
            && audit.PassMarker == BootSequence.CreatePlanLine(2)
            && diagnostics.StartMarker == BootSequence.CreatePlanLine(3)
            && diagnostics.PassMarker == BootSequence.CreatePlanLine(4);
    }

    private static BootHandoffRecord CreateValidOwnedHandoffRecord()
    {
        return BootHandoffRecord.CreateUefiWithOwnedMemoryMap(
            0x100000,
            4096,
            0x900000,
            128 * 1024,
            48 * 3,
            0x77,
            48,
            1,
            3);
    }

    private static EfiMemoryMapSummary CreateValidMemoryMapSummary()
    {
        return new EfiMemoryMapSummary(
            descriptorCount: 3,
            totalPages: 48,
            totalBytes: 48 * EfiMemoryMapNormalizer.PageSize,
            conventionalPages: 32,
            lowestPhysicalAddress: 0x100000,
            highestPhysicalEndExclusive: 0x130000);
    }

    private static FaultInfrastructureReadinessResult CreateReadyFaultInfrastructureResult()
    {
        return FaultInfrastructureReadiness.RequireReadyForPrivilegedInstall(
            serialDiagnosticsReady: true,
            IdtTablePlanResult.Success(),
            IdtrDescriptor.Create(0xffff800000100000, IdtTablePlan.EncodedByteLength),
            gdtReady: true,
            tssIstReady: true,
            doubleFaultIstReady: true);
    }

    private static UefiExitBootServicesPlanResult CreateExitBootServicesPlan(
        UefiExitBootServicesPlanStatus status)
    {
        return UefiExitBootServicesPlanResult.Create(
            status,
            EfiBootHandoffStatus.Success,
            shouldRetry: status == UefiExitBootServicesPlanStatus.RetryRequired,
            attemptIndex: 1,
            maximumRetryCount: 3);
    }

    private static SerialDiagnosticsGuardResult CreateReadyPostFirmwareDiagnosticsResult()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        guard.MarkOwnedSerialReady();
        return guard.RequirePostFirmwareDiagnostics("exitboot.preflight");
    }

    private static Com1PortIoBackendPlanResult CreateReadyCom1BackendResult()
    {
        return Com1PortIoBackendPlanResult.Success(
            Com1PortIoBackendPlan.RequiredExecutableStubBytes,
            availableStubBytes: 4096);
    }

    private static DescriptorTableInstallReadinessResult CreateReadyDescriptorInstallResult()
    {
        return DescriptorTableInstallReadinessResult.Pass(
            IdtTablePlanStatus.Ok,
            IdtrDescriptorStatus.Ok,
            GdtDescriptorPlanStatus.Ok,
            TssIstPlanStatus.Ok);
    }

    private static FaultProbeReadinessResult CreateReadyFaultProbeReadinessResult()
    {
        byte[] invalidOpcodeBuffer = new byte[FaultProbeStubPlan.InvalidOpcodeRequiredByteLength];
        byte[] nullWriteBuffer = new byte[FaultProbeStubPlan.NullWriteRequiredByteLength];
        ExecutableStubMemoryPlanResult memoryPlan =
            ExecutableStubMemoryPlan.Create((nuint)FaultProbeReadiness.RequiredPayloadBytes);

        return FaultProbeReadiness.RequireReady(invalidOpcodeBuffer, nullWriteBuffer, memoryPlan);
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool Contains(string value)
        {
            return _lines.Contains(value);
        }

        public bool ContainsPrefix(string value)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
