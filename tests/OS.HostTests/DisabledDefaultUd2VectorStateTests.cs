using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Diagnostics;

internal static class DisabledDefaultUd2VectorStateTests
{
    private const string TestName = "m02.013";
    private const ulong Ud2Address = 0x0020_0000;
    private const string HostReadyMarker =
        "CLROS_M02_013_HOST_READY requestBytes=32 resultBytes=72 " +
        "ud2Bytes=2 vector=6 errorCodePresent=false errorCode=0 " +
        "auditFields=23 probeEnabled=false executionRequested=false " +
        "executionAttempted=false syntheticObservation=true " +
        "guestExecution=false faultTriggered=false idtrLoaded=false " +
        "postExitBootServices=false";

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase("value layouts ordinals and defaults are exact",
            TestValueContracts, ref failures);
        RunCase("request header flags and reserved bytes are strict",
            TestRequestIntegrity, ref failures);
        RunCase("dangerous enabled requested and attempted states reject",
            TestDangerousStateRejection, ref failures);
        RunCase("authoritative probe state dominates caller validation",
            TestAuthoritativeProbeGate, ref failures);
        RunCase("all prerequisite attestations are required in order",
            TestPrerequisiteAttestations, ref failures);
        RunCase("exact two byte canonical range is enforced",
            TestCanonicalUd2Range, ref failures);
        RunCase("expected vector error metadata and rip are fixed",
            TestExpectedFrameRequirements, ref failures);
        RunCase("all cpu fault frame fields are audited in order",
            TestEveryFrameField, ref failures);
        RunCase("first mismatch is deterministic",
            TestFirstMismatch, ref failures);
        RunCase("value only calls allocate no managed memory",
            TestZeroAllocation, ref failures);
        RunCase("exact source compiles without the bcl",
            TestNoStdLibCompile, ref failures);
        RunCase("no stdlib authorities have one source definition",
            TestNoStdLibAuthorityFactoring, ref failures);
        RunCase("source firewall prevents hardware behavior",
            TestSourceFirewall, ref failures);
        RunCase("legacy 0f0bc3 plan remains unreferenced and uninvoked",
            TestLegacyPlanNeverInvoked, ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-tests failures=" + failures);
            throw new InvalidOperationException(
                "M02-013 disabled-default UD2 host tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {
        HostCheck.Run(TestName + " " + name, () =>
        {
            try
            {
                if (test())
                {
                    return true;
                }
                Console.WriteLine("M02_013_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "M02_013_CASE_FAIL " + Normalize(name) + " " +
                    exception.GetType().Name + ": " + exception.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestValueContracts()
    {
        DisabledDefaultUd2VectorStateRequest emptyRequest = default;
        DisabledDefaultUd2VectorStateResult emptyResult = default;

        return Enum.GetUnderlyingType(
                typeof(DisabledDefaultUd2VectorStateStatus)) == typeof(byte)
            && Enum.GetUnderlyingType(
                typeof(DisabledDefaultUd2VectorField)) == typeof(byte)
            && Enum.GetUnderlyingType(
                typeof(DisabledDefaultUd2VectorStateOperation)) == typeof(byte)
            && EnumValuesAreSequential<DisabledDefaultUd2VectorStateStatus>(20)
            && EnumValuesAreSequential<DisabledDefaultUd2VectorField>(24)
            && EnumValuesAreSequential<DisabledDefaultUd2VectorStateOperation>(3)
            && Unsafe.SizeOf<DisabledDefaultUd2VectorStateRequest>() == 32
            && Unsafe.SizeOf<DisabledDefaultUd2VectorStateResult>() == 72
            && DisabledDefaultUd2VectorStateRequest.StructureSize == 32
            && DisabledDefaultUd2VectorStateRequest.StructureVersion == 1
            && DisabledDefaultUd2VectorStateResult.StructureSize == 72
            && DisabledDefaultUd2VectorStateResult.StructureVersion == 1
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                DisabledDefaultUd2VectorStateRequest>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                DisabledDefaultUd2VectorStateResult>()
            && HasRequestOffsets()
            && HasResultOffsets()
            && DisabledDefaultUd2VectorState.InvalidOpcodeVector == 6
            && DisabledDefaultUd2VectorState.InvalidOpcodeVector ==
                FaultVectorCatalog.InvalidOpcode
            && DisabledDefaultUd2VectorState.Ud2InstructionByteCount == 2
            && DisabledDefaultUd2VectorState.CpuFaultFrameFieldCount == 23
            && DisabledDefaultUd2VectorState.EnabledByDefault ==
                Probes.EnableInvalidOpcodePanicProbe
            && !DisabledDefaultUd2VectorState.EnabledByDefault
            && !DisabledDefaultUd2VectorState.PerformsLiveFault
            && !DisabledDefaultUd2VectorState.ExecutesProbe
            && !DisabledDefaultUd2VectorState.EmitsExecutableBytes
            && !DisabledDefaultUd2VectorState.PerformsIo
            && !DisabledDefaultUd2VectorState.LoadsIdtr
            && !DisabledDefaultUd2VectorState.InstallsHandler
            && !DisabledDefaultUd2VectorState.IsGuestEvidence
            && DisabledDefaultUd2VectorState.UsesSyntheticObservation
            && !emptyRequest.IsInitialized
            && !emptyResult.IsInitialized
            && emptyResult.Status ==
                DisabledDefaultUd2VectorStateStatus.Uninitialized
            && emptyResult.Operation ==
                DisabledDefaultUd2VectorStateOperation.Uninitialized
            && emptyResult.FirstMismatchField ==
                DisabledDefaultUd2VectorField.None
            && emptyResult.ComparedFieldCount == 0
            && !emptyResult.ReadyDisabled
            && !emptyResult.ObservationAccepted
            && !emptyResult.Succeeded
            && !emptyResult.IsGuestEvidence;
    }

    private static bool TestRequestIntegrity()
    {
        DisabledDefaultUd2VectorStateRequest request = CreateReadyRequest();
        DisabledDefaultUd2VectorStateResult ready =
            DisabledDefaultUd2VectorState.Plan(request);
        if (!request.IsInitialized ||
            request.EnabledByDefault ||
            request.ExecutionRequested ||
            request.ExecutionAttempted ||
            !request.OwnedSerialReady ||
            !request.OwnedGdtTssReady ||
            !request.OwnedIdtReady ||
            !request.InvalidOpcodeHandlerReady ||
            request.Ud2Address != Ud2Address ||
            !ready.ReadyDisabled ||
            !ready.Succeeded ||
            ready.ObservationAccepted ||
            !SameRequest(request, ready.Request) ||
            ready.FirstMismatchField != DisabledDefaultUd2VectorField.None ||
            ready.ComparedFieldCount != 0 ||
            ready.ExpectedValue != 0 ||
            ready.ObservedValue != 0 ||
            ready.IsGuestEvidence)
        {
            return false;
        }

        int[] hostileRequestOffsets =
        [
            0, 4, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
            24, 25, 26, 27, 28, 29, 30, 31
        ];
        for (int index = 0; index < hostileRequestOffsets.Length; index++)
        {
            DisabledDefaultUd2VectorStateRequest hostile = MutateRequestByte(
                request,
                hostileRequestOffsets[index],
                0xa5);
            DisabledDefaultUd2VectorStateResult rejected =
                DisabledDefaultUd2VectorState.Plan(hostile);
            if (hostile.IsInitialized ||
                !rejected.IsInitialized ||
                rejected.Status !=
                    DisabledDefaultUd2VectorStateStatus.InvalidRequestState ||
                rejected.Operation !=
                    DisabledDefaultUd2VectorStateOperation.Plan ||
                rejected.Succeeded ||
                !SameRequest(hostile, rejected.Request))
            {
                return false;
            }
        }

        if (DisabledDefaultUd2VectorState.Plan(default).Status !=
            DisabledDefaultUd2VectorStateStatus.InvalidRequestState)
        {
            return false;
        }

        int[] hostileResultOffsets = [0, 4, 6, 7, 8, 9, 10, 12, 64];
        for (int index = 0; index < hostileResultOffsets.Length; index++)
        {
            DisabledDefaultUd2VectorStateResult hostile = MutateResultByte(
                ready,
                hostileResultOffsets[index],
                0xa5);
            if (hostile.IsInitialized || hostile.Succeeded)
            {
                return false;
            }
        }

        DisabledDefaultUd2VectorStateResult forgedMissingPrerequisite =
            MutateResultByte(ready, 26, 0);
        DisabledDefaultUd2VectorStateResult forgedNullAddress =
            MutateResultByte(ready, 34, 0);
        if (!forgedMissingPrerequisite.IsInitialized ||
            forgedMissingPrerequisite.Succeeded ||
            !forgedNullAddress.IsInitialized ||
            forgedNullAddress.Succeeded)
        {
            return false;
        }

        return true;
    }

    private static bool TestDangerousStateRejection()
    {
        DisabledDefaultUd2VectorStateResult enabled =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                enabled: true,
                requested: false,
                attempted: false,
                serial: true,
                gdtTss: true,
                idt: true,
                handler: true));
        DisabledDefaultUd2VectorStateResult requested =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                enabled: false,
                requested: true,
                attempted: false,
                serial: true,
                gdtTss: true,
                idt: true,
                handler: true));
        DisabledDefaultUd2VectorStateResult attempted =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                enabled: false,
                requested: false,
                attempted: true,
                serial: true,
                gdtTss: true,
                idt: true,
                handler: true));
        DisabledDefaultUd2VectorStateResult all =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                enabled: true,
                requested: true,
                attempted: true,
                serial: false,
                gdtTss: false,
                idt: false,
                handler: false));
        DisabledDefaultUd2VectorStateResult requestedBeforeAttempted =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                enabled: false,
                requested: true,
                attempted: true,
                serial: true,
                gdtTss: true,
                idt: true,
                handler: true));

        return IsPlanFailure(enabled,
                DisabledDefaultUd2VectorStateStatus.EnabledByDefaultRejected)
            && IsPlanFailure(requested,
                DisabledDefaultUd2VectorStateStatus.ExecutionRequestedRejected)
            && IsPlanFailure(attempted,
                DisabledDefaultUd2VectorStateStatus.ExecutionAttemptedRejected)
            && IsPlanFailure(all,
                DisabledDefaultUd2VectorStateStatus.EnabledByDefaultRejected)
            && IsPlanFailure(requestedBeforeAttempted,
                DisabledDefaultUd2VectorStateStatus.ExecutionRequestedRejected)
            && !Probes.EnableInvalidOpcodePanicProbe;
    }

    private static bool TestAuthoritativeProbeGate()
    {
        string source = File.ReadAllText(Path.Combine(
                HostToolProcess.FindRepositoryRoot(),
                @"src\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorState.cs"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        return source.Contains(
                "public const bool EnabledByDefault =\n" +
                "        Probes.EnableInvalidOpcodePanicProbe;",
                StringComparison.Ordinal)
            && source.Contains(
                "if (EnabledByDefault || request.EnabledByDefault)",
                StringComparison.Ordinal)
            && source.Contains(
                "if (!request.IsInitialized ||\n" +
                "            EnabledByDefault ||\n" +
                "            request.EnabledByDefault ||",
                StringComparison.Ordinal)
            && CountOccurrences(
                source,
                "if (EnabledByDefault || request.EnabledByDefault)") == 1
            && CountOccurrences(
                source,
                "EnabledByDefault ||\n" +
                "            request.EnabledByDefault ||") == 1;
    }

    private static bool TestPrerequisiteAttestations()
    {
        DisabledDefaultUd2VectorStateResult noSerial =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                false, false, false, false, true, true, true));
        DisabledDefaultUd2VectorStateResult noGdtTss =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                false, false, false, true, false, true, true));
        DisabledDefaultUd2VectorStateResult noIdt =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                false, false, false, true, true, false, true));
        DisabledDefaultUd2VectorStateResult noHandler =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                false, false, false, true, true, true, false));
        DisabledDefaultUd2VectorStateResult none =
            DisabledDefaultUd2VectorState.Plan(CreateRequest(
                false, false, false, false, false, false, false));

        return IsPlanFailure(noSerial,
                DisabledDefaultUd2VectorStateStatus.OwnedSerialUnavailable)
            && IsPlanFailure(noGdtTss,
                DisabledDefaultUd2VectorStateStatus.OwnedGdtTssUnavailable)
            && IsPlanFailure(noIdt,
                DisabledDefaultUd2VectorStateStatus.OwnedIdtUnavailable)
            && IsPlanFailure(noHandler,
                DisabledDefaultUd2VectorStateStatus
                    .InvalidOpcodeHandlerUnavailable)
            && IsPlanFailure(none,
                DisabledDefaultUd2VectorStateStatus.OwnedSerialUnavailable)
            && DisabledDefaultUd2VectorState.Plan(CreateReadyRequest())
                .ReadyDisabled;
    }

    private static bool TestCanonicalUd2Range()
    {
        return ExpectAddressStatus(
                0,
                DisabledDefaultUd2VectorStateStatus.NullUd2Address)
            && ExpectAddressStatus(
                0x0000_8000_0000_0000UL,
                DisabledDefaultUd2VectorStateStatus.NonCanonicalUd2Address)
            && ExpectAddressStatus(
                0xffff_7fff_ffff_ffffUL,
                DisabledDefaultUd2VectorStateStatus.NonCanonicalUd2Address)
            && ExpectAddressStatus(
                ulong.MaxValue,
                DisabledDefaultUd2VectorStateStatus.Ud2AddressSpanOverflow)
            && ExpectAddressStatus(
                0x0000_7fff_ffff_ffffUL,
                DisabledDefaultUd2VectorStateStatus.NonCanonicalUd2AddressSpan)
            && ExpectReadyAddress(1)
            && ExpectReadyAddress(0x0000_7fff_ffff_fffeUL)
            && ExpectReadyAddress(0xffff_8000_0000_0000UL)
            && ExpectReadyAddress(0xffff_ffff_ffff_fffeUL);
    }

    private static bool TestExpectedFrameRequirements()
    {
        DisabledDefaultUd2VectorStateRequest request = CreateReadyRequest();
        CpuFaultFrame expected = CreateFrame();

        DisabledDefaultUd2VectorStateResult vector =
            DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                request,
                WithField(expected, DisabledDefaultUd2VectorField.Vector, 7),
                expected);
        DisabledDefaultUd2VectorStateResult metadata =
            DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                request,
                WithField(expected,
                    DisabledDefaultUd2VectorField.HasErrorCode, 1),
                expected);
        DisabledDefaultUd2VectorStateResult error =
            DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                request,
                WithField(expected, DisabledDefaultUd2VectorField.ErrorCode, 1),
                expected);
        DisabledDefaultUd2VectorStateResult rip =
            DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                request,
                WithField(expected, DisabledDefaultUd2VectorField.Rip,
                    Ud2Address + 2),
                expected);

        return IsInvalidExpected(vector,
                DisabledDefaultUd2VectorField.Vector, 6, 7)
            && IsInvalidExpected(metadata,
                DisabledDefaultUd2VectorField.HasErrorCode, 0, 1)
            && IsInvalidExpected(error,
                DisabledDefaultUd2VectorField.ErrorCode, 0, 1)
            && IsInvalidExpected(rip,
                DisabledDefaultUd2VectorField.Rip,
                Ud2Address,
                Ud2Address + 2)
            && DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                default, expected, expected).Status ==
                DisabledDefaultUd2VectorStateStatus.InvalidRequestState;
    }

    private static bool TestEveryFrameField()
    {
        DisabledDefaultUd2VectorStateRequest request = CreateReadyRequest();
        CpuFaultFrame expected = CreateFrame();
        DisabledDefaultUd2VectorStateResult accepted =
            DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                request,
                expected,
                expected);
        if (!accepted.ObservationAccepted ||
            !accepted.Succeeded ||
            accepted.ReadyDisabled ||
            accepted.Status !=
                DisabledDefaultUd2VectorStateStatus.ObservationAccepted ||
            accepted.Operation != DisabledDefaultUd2VectorStateOperation
                .AuditSyntheticObservation ||
            accepted.FirstMismatchField !=
                DisabledDefaultUd2VectorField.None ||
            accepted.ComparedFieldCount != 23 ||
            accepted.ExpectedValue != 0 ||
            accepted.ObservedValue != 0 ||
            accepted.IsGuestEvidence)
        {
            return false;
        }

        for (byte value = 1; value <= 23; value++)
        {
            DisabledDefaultUd2VectorField field =
                (DisabledDefaultUd2VectorField)value;
            ulong replacement = ReplacementValue(expected, field);
            CpuFaultFrame observed = WithField(expected, field, replacement);
            DisabledDefaultUd2VectorStateResult mismatch =
                DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                    request,
                    expected,
                    observed);
            DisabledDefaultUd2VectorStateStatus expectedStatus = field switch
            {
                DisabledDefaultUd2VectorField.Vector =>
                    DisabledDefaultUd2VectorStateStatus.VectorMismatch,
                DisabledDefaultUd2VectorField.HasErrorCode =>
                    DisabledDefaultUd2VectorStateStatus
                        .ErrorCodeMetadataMismatch,
                DisabledDefaultUd2VectorField.ErrorCode =>
                    DisabledDefaultUd2VectorStateStatus.ErrorCodeMismatch,
                _ => DisabledDefaultUd2VectorStateStatus.RegisterStateMismatch
            };

            if (!mismatch.IsInitialized ||
                mismatch.Succeeded ||
                mismatch.Status != expectedStatus ||
                mismatch.Operation != DisabledDefaultUd2VectorStateOperation
                    .AuditSyntheticObservation ||
                mismatch.FirstMismatchField != field ||
                mismatch.ComparedFieldCount != value ||
                mismatch.ExpectedValue != FieldValue(expected, field) ||
                mismatch.ObservedValue != FieldValue(observed, field) ||
                !SameRequest(request, mismatch.Request))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestFirstMismatch()
    {
        DisabledDefaultUd2VectorStateRequest request = CreateReadyRequest();
        CpuFaultFrame expected = CreateFrame();

        CpuFaultFrame vectorAndR15 = WithField(
            WithField(expected, DisabledDefaultUd2VectorField.R15,
                expected.R15 + 1),
            DisabledDefaultUd2VectorField.Vector,
            7);
        CpuFaultFrame metadataAndRsp = WithField(
            WithField(expected, DisabledDefaultUd2VectorField.Rsp,
                expected.Rsp + 1),
            DisabledDefaultUd2VectorField.HasErrorCode,
            1);
        CpuFaultFrame errorAndRip = WithField(
            WithField(expected, DisabledDefaultUd2VectorField.Rip,
                expected.Rip + 1),
            DisabledDefaultUd2VectorField.ErrorCode,
            1);
        CpuFaultFrame ripAndRsp = WithField(
            WithField(expected, DisabledDefaultUd2VectorField.Rsp,
                expected.Rsp + 1),
            DisabledDefaultUd2VectorField.Rip,
            expected.Rip + 1);

        return HasFirstMismatch(request, expected, vectorAndR15,
                DisabledDefaultUd2VectorField.Vector, 1)
            && HasFirstMismatch(request, expected, metadataAndRsp,
                DisabledDefaultUd2VectorField.HasErrorCode, 2)
            && HasFirstMismatch(request, expected, errorAndRip,
                DisabledDefaultUd2VectorField.ErrorCode, 3)
            && HasFirstMismatch(request, expected, ripAndRsp,
                DisabledDefaultUd2VectorField.Rip, 4);
    }

    private static bool TestZeroAllocation()
    {
        DisabledDefaultUd2VectorStateRequest request = CreateReadyRequest();
        CpuFaultFrame expected = CreateFrame();
        DisabledDefaultUd2VectorStateResult warmPlan =
            DisabledDefaultUd2VectorState.Plan(request);
        DisabledDefaultUd2VectorStateResult warmAudit =
            DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                request, expected, expected);
        if (!warmPlan.ReadyDisabled || !warmAudit.ObservationAccepted)
        {
            return false;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        DisabledDefaultUd2VectorStateResult lastPlan = default;
        DisabledDefaultUd2VectorStateResult lastAudit = default;
        for (int index = 0; index < 4096; index++)
        {
            lastPlan = DisabledDefaultUd2VectorState.Plan(request);
            lastAudit = DisabledDefaultUd2VectorState
                .AuditSyntheticObservation(request, expected, expected);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return lastPlan.ReadyDisabled &&
            lastAudit.ObservationAccepted &&
            after == before;
    }

    private static bool TestNoStdLibCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m02-013-nostdlib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibRuntime.cs"),
                NoStdLibRuntimeSource,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            string productionSource = Path.Combine(
                repositoryRoot,
                @"src\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorState.cs");
            string probeAuthoritySource = Path.Combine(
                repositoryRoot,
                @"src\OS.Kernel\Diagnostics\InvalidOpcodeProbeAuthority.cs");
            string vectorAuthoritySource = Path.Combine(
                repositoryRoot,
                @"src\OS.Kernel\Diagnostics\InvalidOpcodeVectorAuthority.cs");
            string cpuFaultFrameSource = Path.Combine(
                repositoryRoot,
                @"src\OS.Kernel\Diagnostics\CpuFaultFrame.cs");
            File.WriteAllText(
                Path.Combine(workspace, "M02.013.NoStdLib.csproj"),
                NoStdLibProjectSource.Replace(
                    "__PROBE_AUTHORITY_SOURCE__",
                    EscapeXml(probeAuthoritySource),
                    StringComparison.Ordinal)
                .Replace(
                    "__VECTOR_AUTHORITY_SOURCE__",
                    EscapeXml(vectorAuthoritySource),
                    StringComparison.Ordinal)
                .Replace(
                    "__CPU_FAULT_FRAME_SOURCE__",
                    EscapeXml(cpuFaultFrameSource),
                    StringComparison.Ordinal)
                .Replace(
                    "__PRODUCTION_SOURCE__",
                    EscapeXml(productionSource),
                    StringComparison.Ordinal),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            ProcessStartInfo start = new("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workspace
            };
            start.ArgumentList.Add("build");
            start.ArgumentList.Add("M02.013.NoStdLib.csproj");
            start.ArgumentList.Add("--nologo");
            start.ArgumentList.Add("-m:1");
            start.ArgumentList.Add("--no-dependencies");
            start.ArgumentList.Add("--disable-build-servers");
            start.ArgumentList.Add("-v:minimal");
            start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
            start.ArgumentList.Add("-p:NuGetAudit=false");
            start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            start.Environment["DOTNET_NOLOGO"] = "1";
            start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
            start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

            using Process process = Process.Start(start) ??
                throw new InvalidOperationException(
                    "Failed to start focused M02-013 no-stdlib build.");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                return false;
            }

            string output = stdout.GetAwaiter().GetResult() +
                stderr.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                Console.WriteLine("M02_013_NOSTDLIB_FAIL " +
                    output.Replace('\r', ' ').Replace('\n', ' ').Trim());
                return false;
            }

            return File.Exists(Path.Combine(
                workspace,
                "out",
                "CLROS.M02.DisabledDefaultUd2VectorState.dll"));
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static bool TestNoStdLibAuthorityFactoring()
    {
        string diagnostics = Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            @"src\OS.Kernel\Diagnostics");
        string probes = File.ReadAllText(
            Path.Combine(diagnostics, "Probes.cs"));
        string probeAuthority = File.ReadAllText(
            Path.Combine(diagnostics, "InvalidOpcodeProbeAuthority.cs"));
        string vectors = File.ReadAllText(
            Path.Combine(diagnostics, "FaultVectorInfo.cs"));
        string vectorAuthority = File.ReadAllText(
            Path.Combine(diagnostics, "InvalidOpcodeVectorAuthority.cs"));
        string panicFormatter = File.ReadAllText(
            Path.Combine(diagnostics, "PanicFormatter.cs"));
        string cpuFaultFrame = File.ReadAllText(
            Path.Combine(diagnostics, "CpuFaultFrame.cs"));

        int probeDefinitionCount = 0;
        int vectorDefinitionCount = 0;
        int frameDefinitionCount = 0;
        string[] sources = Directory.GetFiles(diagnostics, "*.cs");
        for (int index = 0; index < sources.Length; index++)
        {
            string source = File.ReadAllText(sources[index]);
            probeDefinitionCount += CountOccurrences(
                source,
                "public const bool EnableInvalidOpcodePanicProbe");
            vectorDefinitionCount += CountOccurrences(
                source,
                "public const byte InvalidOpcode = 6;");
            frameDefinitionCount += CountOccurrences(
                source,
                "public readonly struct CpuFaultFrame");
        }

        string[] frameProperties =
        [
            "public int Vector { get; }",
            "public bool HasErrorCode { get; }",
            "public ulong ErrorCode { get; }",
            "public ulong Rip { get; }",
            "public ulong Rsp { get; }",
            "public ulong Rbp { get; }",
            "public ulong Rflags { get; }",
            "public ulong Cr2 { get; }",
            "public ulong Cr3 { get; }",
            "public ulong Rax { get; }",
            "public ulong Rbx { get; }",
            "public ulong Rcx { get; }",
            "public ulong Rdx { get; }",
            "public ulong Rsi { get; }",
            "public ulong Rdi { get; }",
            "public ulong R8 { get; }",
            "public ulong R9 { get; }",
            "public ulong R10 { get; }",
            "public ulong R11 { get; }",
            "public ulong R12 { get; }",
            "public ulong R13 { get; }",
            "public ulong R14 { get; }",
            "public ulong R15 { get; }"
        ];
        for (int index = 0; index < frameProperties.Length; index++)
        {
            if (CountOccurrences(cpuFaultFrame, frameProperties[index]) != 1)
            {
                return false;
            }
        }

        return probes.Contains("public static partial class Probes",
                StringComparison.Ordinal)
            && !probes.Contains(
                "public const bool EnableInvalidOpcodePanicProbe",
                StringComparison.Ordinal)
            && probeAuthority.Contains(
                "public const bool EnableInvalidOpcodePanicProbe = false;",
                StringComparison.Ordinal)
            && vectors.Contains(
                "public static partial class FaultVectorCatalog",
                StringComparison.Ordinal)
            && !vectors.Contains(
                "public const byte InvalidOpcode = 6;",
                StringComparison.Ordinal)
            && vectorAuthority.Contains(
                "public const byte InvalidOpcode = 6;",
                StringComparison.Ordinal)
            && !panicFormatter.Contains(
                "public readonly struct CpuFaultFrame",
                StringComparison.Ordinal)
            && frameProperties.Length ==
                DisabledDefaultUd2VectorState.CpuFaultFrameFieldCount
            && probeDefinitionCount == 1
            && vectorDefinitionCount == 1
            && frameDefinitionCount == 1
            && NoStdLibAuthoritySourceIsSafe(probeAuthority)
            && NoStdLibAuthoritySourceIsSafe(vectorAuthority)
            && NoStdLibAuthoritySourceIsSafe(cpuFaultFrame);
    }

    private static bool NoStdLibAuthoritySourceIsSafe(string source)
    {
        string[] forbidden =
        [
            "string", "Span<", "Nullable<", "throw ", "delegate",
            "Console.", "Marshal.", "NativeMemory", "new byte["
        ];
        for (int index = 0; index < forbidden.Length; index++)
        {
            if (source.Contains(
                    forbidden[index],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestSourceFirewall()
    {
        string source = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            @"src\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorState.cs"));
        string liveMarker = "CLROS_M02_013_" + "READY";
        string[] forbidden =
        [
            "throw ",
            "new byte[",
            "stackalloc",
            "Span<",
            "ReadOnlySpan",
            "delegate*",
            "DllImport",
            "Marshal.",
            "NativeMemory",
            "VirtualProtect",
            "mprotect",
            "FaultProbeStubPlan",
            "IDTRContinuedExecution",
            "EfiEntry",
            "BootSequence",
            "Console.",
            "0x0f",
            "0x0b",
            "0xc3",
            HostReadyMarker,
            liveMarker,
            "TEST_PASS m02.013"
        ];

        for (int index = 0; index < forbidden.Length; index++)
        {
            if (source.Contains(
                    forbidden[index],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return source.Contains(
                "Probes.EnableInvalidOpcodePanicProbe",
                StringComparison.Ordinal)
            && source.Contains("PerformsLiveFault = false",
                StringComparison.Ordinal)
            && source.Contains("ExecutesProbe = false",
                StringComparison.Ordinal)
            && source.Contains("EmitsExecutableBytes = false",
                StringComparison.Ordinal)
            && source.Contains("PerformsIo = false",
                StringComparison.Ordinal)
            && source.Contains("LoadsIdtr = false",
                StringComparison.Ordinal)
            && source.Contains("IsGuestEvidence = false",
                StringComparison.Ordinal)
            && source.Contains("Ud2InstructionByteCount = 2",
                StringComparison.Ordinal)
            && !source.Contains("unsafe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TestLegacyPlanNeverInvoked()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            @"src\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorState.cs"));
        string legacy = File.ReadAllText(Path.Combine(
            root,
            @"src\OS.Kernel\Diagnostics\FaultProbeStubPlan.cs"));
        byte[] untouched = [0xa5, 0xa5, 0xa5, 0xa5];
        DisabledDefaultUd2VectorStateRequest request = CreateReadyRequest();
        CpuFaultFrame frame = CreateFrame();

        DisabledDefaultUd2VectorStateResult planned =
            DisabledDefaultUd2VectorState.Plan(request);
        DisabledDefaultUd2VectorStateResult audited =
            DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                request, frame, frame);

        return planned.ReadyDisabled
            && audited.ObservationAccepted
            && untouched.AsSpan().SequenceEqual(
                new byte[] { 0xa5, 0xa5, 0xa5, 0xa5 })
            && legacy.Contains("TwoByteOpcodePrefix = 0x0f",
                StringComparison.OrdinalIgnoreCase)
            && legacy.Contains("Ud2Opcode = 0x0b",
                StringComparison.OrdinalIgnoreCase)
            && legacy.Contains("RetOpcode = 0xc3",
                StringComparison.OrdinalIgnoreCase)
            && !source.Contains("FaultProbeStubPlan", StringComparison.Ordinal)
            && !source.Contains(".Emit(", StringComparison.Ordinal)
            && !source.Contains("delegate", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasRequestOffsets()
    {
        return HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_structureSize", 0)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_structureVersion", 4)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_isInitialized", 6)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_enabledByDefault", 7)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_executionRequested", 8)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_executionAttempted", 9)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_ownedSerialReady", 10)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_ownedGdtTssReady", 11)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_ownedIdtReady", 12)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_invalidOpcodeHandlerReady", 13)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_reserved0", 14)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_ud2Address", 16)
            && HasOffset<DisabledDefaultUd2VectorStateRequest>(
                "_reserved1", 24);
    }

    private static bool HasResultOffsets()
    {
        return HasOffset<DisabledDefaultUd2VectorStateResult>(
                "_structureSize", 0)
            && HasOffset<DisabledDefaultUd2VectorStateResult>(
                "_structureVersion", 4)
            && HasOffset<DisabledDefaultUd2VectorStateResult>(
                "_isInitialized", 6)
            && HasOffset<DisabledDefaultUd2VectorStateResult>("_status", 7)
            && HasOffset<DisabledDefaultUd2VectorStateResult>("_operation", 8)
            && HasOffset<DisabledDefaultUd2VectorStateResult>(
                "_firstMismatchField", 9)
            && HasOffset<DisabledDefaultUd2VectorStateResult>(
                "_comparedFieldCount", 10)
            && HasOffset<DisabledDefaultUd2VectorStateResult>("_reserved0", 12)
            && HasOffset<DisabledDefaultUd2VectorStateResult>("_request", 16)
            && HasOffset<DisabledDefaultUd2VectorStateResult>(
                "_expectedValue", 48)
            && HasOffset<DisabledDefaultUd2VectorStateResult>(
                "_observedValue", 56)
            && HasOffset<DisabledDefaultUd2VectorStateResult>("_reserved1", 64);
    }

    private static bool HasOffset<T>(string field, int expected)
        where T : struct
    {
        return Marshal.OffsetOf<T>(field).ToInt32() == expected;
    }

    private static DisabledDefaultUd2VectorStateRequest CreateReadyRequest(
        ulong address = Ud2Address)
    {
        return DisabledDefaultUd2VectorStateRequest.CreateDisabled(
            address,
            callerAttestsOwnedSerialReady: true,
            callerAttestsOwnedGdtTssReady: true,
            callerAttestsOwnedIdtReady: true,
            callerAttestsInvalidOpcodeHandlerReady: true);
    }

    private static DisabledDefaultUd2VectorStateRequest CreateRequest(
        bool enabled,
        bool requested,
        bool attempted,
        bool serial,
        bool gdtTss,
        bool idt,
        bool handler)
    {
        return DisabledDefaultUd2VectorStateRequest.CreateForValidation(
            Ud2Address,
            enabled,
            requested,
            attempted,
            serial,
            gdtTss,
            idt,
            handler);
    }

    private static bool ExpectAddressStatus(
        ulong address,
        DisabledDefaultUd2VectorStateStatus expectedStatus)
    {
        DisabledDefaultUd2VectorStateResult result =
            DisabledDefaultUd2VectorState.Plan(CreateReadyRequest(address));
        return IsPlanFailure(result, expectedStatus) &&
            result.Request.Ud2Address == address;
    }

    private static bool ExpectReadyAddress(ulong address)
    {
        DisabledDefaultUd2VectorStateResult result =
            DisabledDefaultUd2VectorState.Plan(CreateReadyRequest(address));
        return result.ReadyDisabled &&
            result.Request.Ud2Address == address;
    }

    private static bool IsPlanFailure(
        DisabledDefaultUd2VectorStateResult result,
        DisabledDefaultUd2VectorStateStatus expectedStatus)
    {
        return result.IsInitialized &&
            result.Operation == DisabledDefaultUd2VectorStateOperation.Plan &&
            result.Status == expectedStatus &&
            result.FirstMismatchField == DisabledDefaultUd2VectorField.None &&
            result.ComparedFieldCount == 0 &&
            result.ExpectedValue == 0 &&
            result.ObservedValue == 0 &&
            !result.Succeeded &&
            !result.IsGuestEvidence;
    }

    private static bool IsInvalidExpected(
        DisabledDefaultUd2VectorStateResult result,
        DisabledDefaultUd2VectorField field,
        ulong expected,
        ulong observed)
    {
        return result.IsInitialized &&
            result.Operation == DisabledDefaultUd2VectorStateOperation
                .AuditSyntheticObservation &&
            result.Status ==
                DisabledDefaultUd2VectorStateStatus.InvalidExpectedState &&
            result.FirstMismatchField == field &&
            result.ComparedFieldCount == 0 &&
            result.ExpectedValue == expected &&
            result.ObservedValue == observed &&
            !result.Succeeded;
    }

    private static bool HasFirstMismatch(
        DisabledDefaultUd2VectorStateRequest request,
        CpuFaultFrame expected,
        CpuFaultFrame observed,
        DisabledDefaultUd2VectorField field,
        ushort comparedCount)
    {
        DisabledDefaultUd2VectorStateResult result =
            DisabledDefaultUd2VectorState.AuditSyntheticObservation(
                request,
                expected,
                observed);
        return result.FirstMismatchField == field &&
            result.ComparedFieldCount == comparedCount &&
            result.ExpectedValue == FieldValue(expected, field) &&
            result.ObservedValue == FieldValue(observed, field) &&
            !result.Succeeded;
    }

    private static CpuFaultFrame CreateFrame()
    {
        return new CpuFaultFrame(
            vector: 6,
            hasErrorCode: false,
            errorCode: 0,
            rip: Ud2Address,
            rsp: 0x1111_1111_1111_1111,
            rbp: 0x2222_2222_2222_2222,
            rflags: 0x0000_0000_0000_0246,
            cr2: 0x3333_3333_3333_3333,
            cr3: 0x4444_4444_4444_4000,
            rax: 0x5555_5555_5555_5555,
            rbx: 0x6666_6666_6666_6666,
            rcx: 0x7777_7777_7777_7777,
            rdx: 0x8888_8888_8888_8888,
            rsi: 0x9999_9999_9999_9999,
            rdi: 0xaaaa_aaaa_aaaa_aaaa,
            r8: 0xbbbb_bbbb_bbbb_bbbb,
            r9: 0xcccc_cccc_cccc_cccc,
            r10: 0xdddd_dddd_dddd_dddd,
            r11: 0xeeee_eeee_eeee_eeee,
            r12: 0xffff_ffff_ffff_fff0,
            r13: 0x0123_4567_89ab_cdef,
            r14: 0xfedc_ba98_7654_3210,
            r15: 0x1357_9bdf_2468_ace0);
    }

    private static CpuFaultFrame WithField(
        in CpuFaultFrame source,
        DisabledDefaultUd2VectorField field,
        ulong value)
    {
        return new CpuFaultFrame(
            field == DisabledDefaultUd2VectorField.Vector
                ? (int)value : source.Vector,
            field == DisabledDefaultUd2VectorField.HasErrorCode
                ? value != 0 : source.HasErrorCode,
            field == DisabledDefaultUd2VectorField.ErrorCode
                ? value : source.ErrorCode,
            field == DisabledDefaultUd2VectorField.Rip ? value : source.Rip,
            field == DisabledDefaultUd2VectorField.Rsp ? value : source.Rsp,
            field == DisabledDefaultUd2VectorField.Rbp ? value : source.Rbp,
            field == DisabledDefaultUd2VectorField.Rflags
                ? value : source.Rflags,
            field == DisabledDefaultUd2VectorField.Cr2 ? value : source.Cr2,
            field == DisabledDefaultUd2VectorField.Cr3 ? value : source.Cr3,
            field == DisabledDefaultUd2VectorField.Rax ? value : source.Rax,
            field == DisabledDefaultUd2VectorField.Rbx ? value : source.Rbx,
            field == DisabledDefaultUd2VectorField.Rcx ? value : source.Rcx,
            field == DisabledDefaultUd2VectorField.Rdx ? value : source.Rdx,
            field == DisabledDefaultUd2VectorField.Rsi ? value : source.Rsi,
            field == DisabledDefaultUd2VectorField.Rdi ? value : source.Rdi,
            field == DisabledDefaultUd2VectorField.R8 ? value : source.R8,
            field == DisabledDefaultUd2VectorField.R9 ? value : source.R9,
            field == DisabledDefaultUd2VectorField.R10 ? value : source.R10,
            field == DisabledDefaultUd2VectorField.R11 ? value : source.R11,
            field == DisabledDefaultUd2VectorField.R12 ? value : source.R12,
            field == DisabledDefaultUd2VectorField.R13 ? value : source.R13,
            field == DisabledDefaultUd2VectorField.R14 ? value : source.R14,
            field == DisabledDefaultUd2VectorField.R15 ? value : source.R15);
    }

    private static ulong ReplacementValue(
        in CpuFaultFrame frame,
        DisabledDefaultUd2VectorField field)
    {
        return field switch
        {
            DisabledDefaultUd2VectorField.Vector => 7,
            DisabledDefaultUd2VectorField.HasErrorCode => 1,
            DisabledDefaultUd2VectorField.ErrorCode => 1,
            _ => FieldValue(frame, field) ^ 0x5a5a_5a5a_5a5a_5a5aUL
        };
    }

    private static ulong FieldValue(
        in CpuFaultFrame frame,
        DisabledDefaultUd2VectorField field)
    {
        return field switch
        {
            DisabledDefaultUd2VectorField.Vector => (ulong)frame.Vector,
            DisabledDefaultUd2VectorField.HasErrorCode =>
                frame.HasErrorCode ? 1UL : 0UL,
            DisabledDefaultUd2VectorField.ErrorCode => frame.ErrorCode,
            DisabledDefaultUd2VectorField.Rip => frame.Rip,
            DisabledDefaultUd2VectorField.Rsp => frame.Rsp,
            DisabledDefaultUd2VectorField.Rbp => frame.Rbp,
            DisabledDefaultUd2VectorField.Rflags => frame.Rflags,
            DisabledDefaultUd2VectorField.Cr2 => frame.Cr2,
            DisabledDefaultUd2VectorField.Cr3 => frame.Cr3,
            DisabledDefaultUd2VectorField.Rax => frame.Rax,
            DisabledDefaultUd2VectorField.Rbx => frame.Rbx,
            DisabledDefaultUd2VectorField.Rcx => frame.Rcx,
            DisabledDefaultUd2VectorField.Rdx => frame.Rdx,
            DisabledDefaultUd2VectorField.Rsi => frame.Rsi,
            DisabledDefaultUd2VectorField.Rdi => frame.Rdi,
            DisabledDefaultUd2VectorField.R8 => frame.R8,
            DisabledDefaultUd2VectorField.R9 => frame.R9,
            DisabledDefaultUd2VectorField.R10 => frame.R10,
            DisabledDefaultUd2VectorField.R11 => frame.R11,
            DisabledDefaultUd2VectorField.R12 => frame.R12,
            DisabledDefaultUd2VectorField.R13 => frame.R13,
            DisabledDefaultUd2VectorField.R14 => frame.R14,
            DisabledDefaultUd2VectorField.R15 => frame.R15,
            _ => 0
        };
    }

    private static DisabledDefaultUd2VectorStateRequest MutateRequestByte(
        DisabledDefaultUd2VectorStateRequest request,
        int offset,
        byte value)
    {
        Span<DisabledDefaultUd2VectorStateRequest> one =
            MemoryMarshal.CreateSpan(ref request, 1);
        MemoryMarshal.AsBytes(one)[offset] = value;
        return request;
    }

    private static DisabledDefaultUd2VectorStateResult MutateResultByte(
        DisabledDefaultUd2VectorStateResult result,
        int offset,
        byte value)
    {
        Span<DisabledDefaultUd2VectorStateResult> one =
            MemoryMarshal.CreateSpan(ref result, 1);
        MemoryMarshal.AsBytes(one)[offset] = value;
        return result;
    }

    private static bool SameRequest(
        DisabledDefaultUd2VectorStateRequest first,
        DisabledDefaultUd2VectorStateRequest second)
    {
        ReadOnlySpan<DisabledDefaultUd2VectorStateRequest> firstSpan =
            MemoryMarshal.CreateReadOnlySpan(ref first, 1);
        ReadOnlySpan<DisabledDefaultUd2VectorStateRequest> secondSpan =
            MemoryMarshal.CreateReadOnlySpan(ref second, 1);
        return MemoryMarshal.AsBytes(firstSpan)
            .SequenceEqual(MemoryMarshal.AsBytes(secondSpan));
    }

    private static bool EnumValuesAreSequential<T>(int expectedCount)
        where T : struct, Enum
    {
        Array values = Enum.GetValues(typeof(T));
        if (values.Length != expectedCount)
        {
            return false;
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (Convert.ToByte(values.GetValue(index)) != index)
            {
                return false;
            }
        }

        return true;
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int offset = 0;
        while (true)
        {
            int found = source.IndexOf(value, offset, StringComparison.Ordinal);
            if (found < 0)
            {
                return count;
            }

            count++;
            offset = found + value.Length;
        }
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return value.Replace(' ', '_').ToUpperInvariant();
    }

    private const string NoStdLibRuntimeSource =
        """
        namespace System
        {
            public class Object { }
            public abstract class ValueType : Object { }
            public abstract class Enum : ValueType { }
            public abstract class Delegate : Object { }
            public abstract class MulticastDelegate : Delegate { }
            public class Attribute : Object { }
            public struct Void { }
            public struct Boolean { }
            public struct Byte { }
            public struct Int32 { }
            public struct UInt16 { }
            public struct UInt32 { }
            public struct UInt64 { }
            public enum AttributeTargets { }
            public sealed class AttributeUsageAttribute : Attribute
            {
                public AttributeUsageAttribute(AttributeTargets validOn) { }
                public bool AllowMultiple { get; set; }
                public bool Inherited { get; set; }
            }
        }

        namespace System.Runtime.CompilerServices
        {
            public sealed class IsReadOnlyAttribute : System.Attribute { }
            public sealed class CompilerGeneratedAttribute : System.Attribute { }
        }

        """;

    private const string NoStdLibProjectSource =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <AssemblyName>CLROS.M02.DisabledDefaultUd2VectorState</AssemblyName>
            <TargetFramework>net10.0</TargetFramework>
            <LangVersion>14.0</LangVersion>
            <OutputType>Library</OutputType>
            <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
            <ImplicitUsings>disable</ImplicitUsings>
            <Nullable>disable</Nullable>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
            <NoStdLib>true</NoStdLib>
            <NoConfig>true</NoConfig>
            <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
            <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
            <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
            <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles>
            <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
            <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
            <EnableNETAnalyzers>false</EnableNETAnalyzers>
            <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
            <DefineConstants>NOSTDLIB_EFI</DefineConstants>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <Deterministic>true</Deterministic>
            <DebugType>none</DebugType>
            <DebugSymbols>false</DebugSymbols>
            <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
            <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
            <OutputPath>$(MSBuildProjectDirectory)\out\</OutputPath>
          </PropertyGroup>
          <ItemGroup>
            <Compile Include="NoStdLibRuntime.cs" />
            <Compile Include="__PROBE_AUTHORITY_SOURCE__"
                     Link="Diagnostics\InvalidOpcodeProbeAuthority.cs" />
            <Compile Include="__VECTOR_AUTHORITY_SOURCE__"
                     Link="Diagnostics\InvalidOpcodeVectorAuthority.cs" />
            <Compile Include="__CPU_FAULT_FRAME_SOURCE__"
                     Link="Diagnostics\CpuFaultFrame.cs" />
            <Compile Include="__PRODUCTION_SOURCE__"
                     Link="Diagnostics\DisabledDefaultUd2VectorState.cs" />
          </ItemGroup>
          <Target Name="CustomizeReferences"
                  BeforeTargets="BeforeCompile"
                  AfterTargets="FindReferenceAssembliesForReferences">
            <ItemGroup>
              <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
              <ReferencePath Remove="@(ReferencePath)" />
            </ItemGroup>
          </Target>
        </Project>
        """;
}
