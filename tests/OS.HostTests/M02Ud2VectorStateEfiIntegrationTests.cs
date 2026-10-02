using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static class M02Ud2VectorStateEfiIntegrationTests
{
    private const string TestName = "m02.013.efi";
    private const string GuestStartMarker = "TEST_START m02.013";
    private const string GuestPassMarker = "TEST_PASS m02.013";
    private const string GuestFailureHeading =
        "M02_UD2_VECTOR_STATE_VERIFY_FAILED";
    private const string GuestFailureDetail =
        "TEST_FAIL m02.013 vector_or_register_mismatch";
    private const ulong ValidCodeAddress = 0x0020_0000UL;
    private const ulong ValidDataAddress = 0x0030_0000UL;
    private const int ExpectedSourceInputCount = 88;
    private const int ExpectedCompileCount = 75;
    private const string ExpectedSourceClosureSha256 =
        "502857efe5c5789c31fc5b4d51726522cbbea366c48cd90bf634c3f343098f70";
    private const string ExpectedProjectSha256 =
        "622ab7e9bc2f37730cc87c04f2b26086f81bc62e4f64b10831d61568d7825944";
    private const string ExpectedEntrySha256 =
        "5de2785ecb4c3d8d261ea112f7ce8758e348785ff88e372a77f4f2d1240ce778";
    private const string ExpectedCompileSha256 =
        "fafc07e210dde2a3a8e2d537f54032b2809cb01258a23b4d513e1d868dc74a9b";
    private const string ExpectedTransitionSha256 =
        "f73c8f78fd1d5a1e0fd54c3fbe80716b40c61f1c05bc9f51ced8a505d61c407e";
    private const string HostReadyMarker =
        "CLROS_M02_013_EFI_HOST_READY codePages=2 dataPages=5 " +
        "normalizedPage=true ownedIdt=true transitionReadBack=true " +
        "ownedGdtVerified=true zeroTails=true cleanup=true " +
        "terminalEntry=C+0x1800 context=D+0x1000 " +
        "guestExecution=false faultTriggered=false " +
        "postExitBootServices=false";

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "M02-013 C and D allocation topology is exact",
            TestAllocationAndCompositeTopology,
            ref failures);
        RunCase(
            "M02-013 default and enabled NativeAOT artifacts prove exact provenance",
            TestDefaultAndEnabledEfiArtifacts,
            ref failures);
        RunCase(
            "M02-013 fake firmware executes every pre-terminal contract stage",
            TestFakeFirmwareOrchestration,
            ref failures);
        RunCase(
            "M02-013 preparation materializes and reads back every authority",
            TestPreparationMaterializationAndReadBack,
            ref failures);
        RunCase(
            "M02-013 pre-entry validation is read-only and tail complete",
            TestPreEntryValidation,
            ref failures);
        RunCase(
            "M02-013 cleanup and marker transports fail closed",
            TestCleanupAndMarkers,
            ref failures);
        RunCase(
            "M02-013 terminal entry and boot-path ordering are exact",
            TestTerminalEntryAndBootOrdering,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" +
                failures);
            throw new InvalidOperationException(
                "M02-013 EFI integration host tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
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
        catch (Exception exception)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL " + name + " " +
                exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static bool TestAllocationAndCompositeTopology()
    {
        string source = ReadEfiEntry();
        string validRange = ExtractBlock(
            source,
            "private static bool IsValidM02Ud2VectorStateRange(",
            "private static bool M02Ud2VectorStateRangesOverlap(");
        string overlap = ExtractBlock(
            source,
            "private static bool M02Ud2VectorStateRangesOverlap(",
            "private static bool IsM02Ud2VectorStateRangeZero(");

        return source.Contains(
                "private const uint M02Ud2VectorStateEfiLoaderCode = 1;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const uint M02Ud2VectorStateEfiLoaderData = 2;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const ulong M02Ud2VectorStatePageSize = 4096;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const ulong M02Ud2VectorStateCodePageCount = 2;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const ulong M02Ud2VectorStateDataPageCount = 5;",
                StringComparison.Ordinal)
            && source.Contains(
                "M02Ud2VectorStateCodePageCount * " +
                "M02Ud2VectorStatePageSize;",
                StringComparison.Ordinal)
            && source.Contains(
                "M02Ud2VectorStateDataPageCount * " +
                "M02Ud2VectorStatePageSize;",
                StringComparison.Ordinal)
            && validRange.Contains(
                ".DisabledDefaultUd2VectorStatePreTerminalContract\n" +
                "            .IsValidRange(address, byteCount);",
                StringComparison.Ordinal)
            && overlap.Contains(
                ".DisabledDefaultUd2VectorStatePreTerminalContract\n" +
                "            .RangesOverlap(",
                StringComparison.Ordinal)
            && DisabledDefaultUd2VectorStatePreTerminalContract
                .PageByteCount == 4096
            && DisabledDefaultUd2VectorStatePreTerminalContract
                .CodePageCount == 2
            && DisabledDefaultUd2VectorStatePreTerminalContract
                .DataPageCount == 5
            && DisabledDefaultUd2VectorStatePreTerminalContract
                .CodeByteCount == 0x2000
            && DisabledDefaultUd2VectorStatePreTerminalContract
                .DataByteCount == 0x5000
            && DisabledDefaultUd2VectorStatePreTerminalContract.IsValidRange(
                ValidCodeAddress,
                DisabledDefaultUd2VectorStatePreTerminalContract.CodeByteCount)
            && !DisabledDefaultUd2VectorStatePreTerminalContract.IsValidRange(
                0,
                DisabledDefaultUd2VectorStatePreTerminalContract.CodeByteCount)
            && !DisabledDefaultUd2VectorStatePreTerminalContract.IsValidRange(
                ~0UL - 0x2000 + 1,
                DisabledDefaultUd2VectorStatePreTerminalContract.CodeByteCount)
            && DisabledDefaultUd2VectorStateTransition
                .CompositeCodeByteCount == 0x2000
            && DisabledDefaultUd2VectorStateTransition
                .CompositeDataByteCount == 0x5000
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedSlotsCompositeOffset == 0x0000
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedSlotsCompositeCapacity == 0x1000
            && DisabledDefaultUd2VectorStateTransition
                .CommonHandlerCompositeOffset == 0x1000
            && DisabledDefaultUd2VectorStateTransition
                .CommonHandlerCompositeCapacity == 0x0800
            && DisabledDefaultUd2VectorStateTransition
                .TransitionEntryCompositeOffset == 0x1800
            && DisabledDefaultUd2VectorStateTransition
                .TransitionCompositeCapacity == 0x0800
            && DisabledDefaultUd2VectorStateTransition.IdtDataOffset == 0x0000
            && DisabledDefaultUd2VectorStateTransition
                .IdtDataCapacity == 0x1000
            && DisabledDefaultUd2VectorStateTransition
                .ContextDataOffset == 0x1000
            && DisabledDefaultUd2VectorStateTransition
                .ContextDataCapacity == 0x1000
            && DisabledDefaultUd2VectorStateTransition
                .Ist1DataOffset == 0x2000
            && DisabledDefaultUd2VectorStateTransition
                .Ist2DataOffset == 0x3000
            && DisabledDefaultUd2VectorStateTransition
                .Ist3DataOffset == 0x4000
            && DisabledDefaultUd2VectorStateTransition
                .Ist1StackTopDataOffset == 0x3000
            && DisabledDefaultUd2VectorStateTransition
                .Ist2StackTopDataOffset == 0x4000
            && DisabledDefaultUd2VectorStateTransition
                .Ist3StackTopDataOffset == 0x5000
            && DisabledDefaultUd2VectorStateTransition
                .StreamByteCount == 0x1000
            && DisabledDefaultUd2VectorStateTransition
                .ContextByteCount < 0x1000
            && !DisabledDefaultUd2VectorStateTransition.HostTestsInvokeBytes;
    }

    private static bool TestDefaultAndEnabledEfiArtifacts()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string script = Path.Combine(root, "scripts", "build-efi-shell.ps1");
        string artifactDirectory = Path.Combine(root, "artifacts", "efi");
        string logDirectory = Path.Combine(root, "artifacts", "logs");
        Directory.CreateDirectory(artifactDirectory);
        Directory.CreateDirectory(logDirectory);

        string identity = Environment.ProcessId.ToString(
                CultureInfo.InvariantCulture) + "-" +
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string prefix = "BOOTX64-M02-013-HOSTTEST-" + identity;
        string defaultArtifact = Path.Combine(
            artifactDirectory,
            prefix + "-DEFAULT.EFI");
        string enabledArtifact = Path.Combine(
            artifactDirectory,
            prefix + "-ENABLED.EFI");
        string defaultLog = Path.Combine(
            logDirectory,
            "m02-013-hosttest-" + identity + "-default.log");
        string enabledLog = Path.Combine(
            logDirectory,
            "m02-013-hosttest-" + identity + "-enabled.log");
        string[] ownedOutputs =
        [
            .. ArtifactOutputPaths(defaultArtifact),
            .. ArtifactOutputPaths(enabledArtifact),
            defaultLog,
            enabledLog
        ];

        SourceAuthoritySnapshot before = CaptureSourceAuthoritySnapshot(root);
        try
        {
            ScriptResult defaultBuild = RunPowerShellScript(
                root,
                script,
                900_000,
                "-EfiOutputPath",
                defaultArtifact,
                "-LogPath",
                defaultLog);
            ScriptResult enabledBuild = RunPowerShellScript(
                root,
                script,
                900_000,
                "-EnableM02Ud2VectorStateProbe",
                "-EfiOutputPath",
                enabledArtifact,
                "-LogPath",
                enabledLog);

            if (defaultBuild.ExitCode != 0 || enabledBuild.ExitCode != 0)
            {
                Console.WriteLine(
                    "M02_013_ARTIFACT_BUILD_AUDIT defaultExit=" +
                    defaultBuild.ExitCode + " enabledExit=" +
                    enabledBuild.ExitCode + " defaultOutput=" +
                    TailForAudit(defaultBuild.Output) + " enabledOutput=" +
                    TailForAudit(enabledBuild.Output));
                return false;
            }

            SourceAuthoritySnapshot after = CaptureSourceAuthoritySnapshot(root);
            string defaultMap = File.ReadAllText(defaultArtifact + ".link.map");
            string enabledMap = File.ReadAllText(enabledArtifact + ".link.map");
            bool defaultExcluded =
                !defaultMap.Contains(
                    "M02Ud2VectorState",
                    StringComparison.Ordinal) &&
                !defaultMap.Contains(
                    "DisabledDefaultUd2VectorState",
                    StringComparison.Ordinal);
            string[] requiredEnabledSymbols =
            [
                "OS_OS_Boot_EfiEntry__PrepareM02Ud2VectorStateProbe",
                "OS_OS_Boot_EfiEntry__EnterM02Ud2VectorStateProbe",
                "OS_OS_Boot_EfiEntry__FailM02Ud2VectorStateBeforeEntry",
                "DisabledDefaultUd2VectorStateTransition__PrepareContext_0",
                "DisabledDefaultUd2VectorStateTransition__EmitStream_0",
                "DisabledDefaultUd2VectorStateTransition__ValidateStreamReadBack_0",
                "M01StageThreeAdapter__TryWriteM02Ud2VectorStateStartLine"
            ];
            bool enabledIncluded = requiredEnabledSymbols.All(
                symbol => enabledMap.Contains(symbol, StringComparison.Ordinal));
            bool verifierPassed =
                defaultBuild.Output.Contains(
                    "TEST_PASS efi.artifact.verify",
                    StringComparison.Ordinal) &&
                enabledBuild.Output.Contains(
                    "TEST_PASS efi.artifact.verify",
                    StringComparison.Ordinal);
            bool sourceHeadStable = before == after;
            bool frozenAuthorityExact =
                before.ProjectSha256 == ExpectedProjectSha256 &&
                before.EntrySha256 == ExpectedEntrySha256 &&
                before.SourceClosureSha256 == ExpectedSourceClosureSha256 &&
                before.CompileSha256 == ExpectedCompileSha256 &&
                before.SourceInputCount == ExpectedSourceInputCount &&
                before.CompileCount == ExpectedCompileCount &&
                Sha256File(Path.Combine(
                    root,
                    "src",
                    "OS.Kernel",
                    "Diagnostics",
                    "DisabledDefaultUd2VectorStateTransition.cs")) ==
                    ExpectedTransitionSha256;
            bool provenanceExact = sourceHeadStable &&
                frozenAuthorityExact &&
                ValidateLinkEvidence(root, defaultArtifact, enabled: false, before) &&
                ValidateLinkEvidence(root, enabledArtifact, enabled: true, before);

            Console.WriteLine(
                "M02_013_ARTIFACT_BUILD_AUDIT defaultExit=0 enabledExit=0 " +
                "defaultExcluded=" +
                defaultExcluded.ToString().ToLowerInvariant() +
                " enabledIncluded=" +
                enabledIncluded.ToString().ToLowerInvariant() +
                " verifierPassed=" +
                verifierPassed.ToString().ToLowerInvariant() +
                " sourceHeadStable=" +
                sourceHeadStable.ToString().ToLowerInvariant() +
                " frozenAuthorityExact=" +
                frozenAuthorityExact.ToString().ToLowerInvariant() +
                " provenanceExact=" +
                provenanceExact.ToString().ToLowerInvariant());
            return defaultExcluded &&
                enabledIncluded &&
                verifierPassed &&
                provenanceExact;
        }
        finally
        {
            DeleteOwnedFiles(ownedOutputs);
        }
    }

    private static bool TestFakeFirmwareOrchestration()
    {
        FakeTrace existingState = RunFakeProbe(new FakePlan
        {
            CurrentState = 1
        });
        FakeTrace missingBootServices = RunFakeProbe(new FakePlan
        {
            BootServicesPresent = false
        });
        FakeTrace missingAllocate = RunFakeProbe(new FakePlan
        {
            AllocatePresent = false
        });
        FakeTrace missingFree = RunFakeProbe(new FakePlan
        {
            FreePresent = false
        });
        FakeTrace codeAllocationFailure = RunFakeProbe(new FakePlan
        {
            CodeAllocationStatus = 1
        });
        FakeTrace dataAllocationFailure = RunFakeProbe(new FakePlan
        {
            DataAllocationStatus = 1
        });

        FakeTrace codeAddressZero = RunFakeProbe(new FakePlan
        {
            CodeAddress = 0
        });
        FakeTrace[] otherInvalidCodeRanges =
        [
            RunFakeProbe(new FakePlan { CodeAddress = ValidCodeAddress + 1 }),
            RunFakeProbe(new FakePlan
            {
                CodeAddress = 0x0000_8000_0000_0000UL
            }),
            RunFakeProbe(new FakePlan
            {
                CodeAddress = ~0UL -
                    DisabledDefaultUd2VectorStatePreTerminalContract
                        .CodeByteCount + 1
            })
        ];
        FakeTrace dataAddressZero = RunFakeProbe(new FakePlan
        {
            DataAddress = 0
        });
        FakeTrace[] otherInvalidDataRanges =
        [
            RunFakeProbe(new FakePlan { DataAddress = ValidDataAddress + 1 }),
            RunFakeProbe(new FakePlan
            {
                DataAddress = 0x0000_8000_0000_0000UL
            }),
            RunFakeProbe(new FakePlan
            {
                DataAddress = ~0UL -
                    DisabledDefaultUd2VectorStatePreTerminalContract
                        .DataByteCount + 1
            })
        ];
        FakeTrace equalAlias = RunFakeProbe(new FakePlan
        {
            DataAddress = ValidCodeAddress
        });
        FakeTrace partialOverlap = RunFakeProbe(new FakePlan
        {
            DataAddress = ValidCodeAddress + 0x1000
        });
        FakeTrace materializationFailure = RunFakeProbe(new FakePlan
        {
            MaterializationSucceeded = false
        });
        FakeTrace readBackFailure = RunFakeProbe(new FakePlan
        {
            ReadBackSucceeded = false
        });
        FakeTrace markerFailure = RunFakeProbe(new FakePlan
        {
            MarkerTransportSucceeded = false
        });
        FakeTrace publicationFailure = RunFakeProbe(new FakePlan
        {
            PublicationSucceeded = false
        });
        FakeTrace success = RunFakeProbe(new FakePlan());

        bool serviceFailuresExact =
            IsFakeFailure(
                existingState,
                DisabledDefaultUd2VectorStatePreTerminalStage.Services,
                DisabledDefaultUd2VectorStatePreTerminalStatus.ExistingState,
                0) &&
            IsFakeFailure(
                missingBootServices,
                DisabledDefaultUd2VectorStatePreTerminalStage.Services,
                DisabledDefaultUd2VectorStatePreTerminalStatus
                    .MissingBootServices,
                0) &&
            IsFakeFailure(
                missingAllocate,
                DisabledDefaultUd2VectorStatePreTerminalStage.Services,
                DisabledDefaultUd2VectorStatePreTerminalStatus
                    .MissingAllocatePages,
                0) &&
            IsFakeFailure(
                missingFree,
                DisabledDefaultUd2VectorStatePreTerminalStage.Services,
                DisabledDefaultUd2VectorStatePreTerminalStatus
                    .MissingFreePages,
                0);
        bool allocationFailuresExact =
            IsFakeFailure(
                codeAllocationFailure,
                DisabledDefaultUd2VectorStatePreTerminalStage.CodeAllocation,
                DisabledDefaultUd2VectorStatePreTerminalStatus
                    .CodeAllocationFailed,
                1) &&
            codeAllocationFailure.ReleaseCalls.Count == 0 &&
            IsFakeFailure(
                dataAllocationFailure,
                DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation,
                DisabledDefaultUd2VectorStatePreTerminalStatus
                    .DataAllocationFailed,
                2) &&
            dataAllocationFailure.ReleaseCalls.SequenceEqual(
                [new FakeRelease(ValidCodeAddress, 2)]);
        bool statusBasedOwnershipExact =
            IsFakeFailure(
                codeAddressZero,
                DisabledDefaultUd2VectorStatePreTerminalStage.CodeAllocation,
                DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidCodeRange,
                1) &&
            codeAddressZero.ReleaseCalls.SequenceEqual(
                [new FakeRelease(0, 2)]) &&
            otherInvalidCodeRanges.All(trace =>
                IsFakeFailure(
                    trace,
                    DisabledDefaultUd2VectorStatePreTerminalStage.CodeAllocation,
                    DisabledDefaultUd2VectorStatePreTerminalStatus
                        .InvalidCodeRange,
                    1) &&
                trace.ReleaseCalls.Count == 1 &&
                trace.ReleaseCalls[0].Pages == 2) &&
            IsFakeFailure(
                dataAddressZero,
                DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation,
                DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange,
                2) &&
            dataAddressZero.ReleaseCalls.SequenceEqual(
                [
                    new FakeRelease(0, 5),
                    new FakeRelease(ValidCodeAddress, 2)
                ]) &&
            IsFakeFailure(
                otherInvalidDataRanges[0],
                DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation,
                DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange,
                2) &&
            IsFakeFailure(
                otherInvalidDataRanges[1],
                DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation,
                DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange,
                2) &&
            otherInvalidDataRanges[0].ReleaseCalls.Count == 2 &&
            otherInvalidDataRanges[0].ReleaseCalls[0].Pages == 5 &&
            otherInvalidDataRanges[0].ReleaseCalls[1] ==
                new FakeRelease(ValidCodeAddress, 2) &&
            otherInvalidDataRanges[1].ReleaseCalls.Count == 2 &&
            otherInvalidDataRanges[1].ReleaseCalls[0].Pages == 5 &&
            otherInvalidDataRanges[1].ReleaseCalls[1] ==
                new FakeRelease(ValidCodeAddress, 2) &&
            IsFakeFailure(
                otherInvalidDataRanges[2],
                DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation,
                DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange,
                2) &&
            otherInvalidDataRanges[2].ReleaseCalls.SequenceEqual(
                [
                    new FakeRelease(
                        ~0UL -
                            DisabledDefaultUd2VectorStatePreTerminalContract
                                .DataByteCount + 1,
                        5)
                ]);
        bool overlapSuppressionExact =
            IsFakeFailure(
                equalAlias,
                DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation,
                DisabledDefaultUd2VectorStatePreTerminalStatus.OverlappingRanges,
                2) &&
            equalAlias.ReleaseCalls.SequenceEqual(
                [new FakeRelease(ValidCodeAddress, 5)]) &&
            IsFakeFailure(
                partialOverlap,
                DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation,
                DisabledDefaultUd2VectorStatePreTerminalStatus.OverlappingRanges,
                2) &&
            partialOverlap.ReleaseCalls.SequenceEqual(
                [new FakeRelease(ValidCodeAddress + 0x1000, 5)]);
        bool stageFailuresExact =
            IsFakeFailure(
                materializationFailure,
                DisabledDefaultUd2VectorStatePreTerminalStage.Materialization,
                DisabledDefaultUd2VectorStatePreTerminalStatus
                    .MaterializationFailed,
                2) &&
            IsFakeFailure(
                readBackFailure,
                DisabledDefaultUd2VectorStatePreTerminalStage.ReadBack,
                DisabledDefaultUd2VectorStatePreTerminalStatus.ReadBackFailed,
                2) &&
            IsFakeFailure(
                markerFailure,
                DisabledDefaultUd2VectorStatePreTerminalStage.MarkerTransport,
                DisabledDefaultUd2VectorStatePreTerminalStatus
                    .MarkerTransportFailed,
                2) &&
            IsFakeFailure(
                publicationFailure,
                DisabledDefaultUd2VectorStatePreTerminalStage.Publication,
                DisabledDefaultUd2VectorStatePreTerminalStatus
                    .PublicationFailed,
                2) &&
            ReverseReleaseExact(materializationFailure) &&
            ReverseReleaseExact(readBackFailure) &&
            ReverseReleaseExact(markerFailure) &&
            ReverseReleaseExact(publicationFailure);

        return serviceFailuresExact &&
            allocationFailuresExact &&
            statusBasedOwnershipExact &&
            overlapSuppressionExact &&
            stageFailuresExact &&
            success.Succeeded &&
            success.TerminalAllowed &&
            !success.FailureEmitted &&
            success.Stage ==
                DisabledDefaultUd2VectorStatePreTerminalStage.Publication &&
            success.Status ==
                DisabledDefaultUd2VectorStatePreTerminalStatus.Success &&
            success.AllocationCalls.SequenceEqual(
                [
                    new FakeAllocation("LoaderCode", ValidCodeAddress, 2, 0),
                    new FakeAllocation("LoaderData", ValidDataAddress, 5, 0)
                ]) &&
            success.ReleaseCalls.Count == 0 &&
            success.Events.SequenceEqual(
                [
                    "services",
                    "allocate-code",
                    "allocate-data",
                    "materialization",
                    "read-back",
                    "marker-transport",
                    "publication",
                    "terminal-allowed"
                ]);
    }

    private static FakeTrace RunFakeProbe(FakePlan plan)
    {
        FakeTrace trace = new();
        trace.Events.Add("services");
        DisabledDefaultUd2VectorStatePreTerminalResult result =
            DisabledDefaultUd2VectorStatePreTerminalContract.EvaluateServices(
                plan.CurrentState,
                plan.BootServicesPresent,
                plan.AllocatePresent,
                plan.FreePresent);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("allocate-code");
        trace.AllocationCalls.Add(new FakeAllocation(
            "LoaderCode",
            plan.CodeAddress,
            DisabledDefaultUd2VectorStatePreTerminalContract.CodePageCount,
            plan.CodeAllocationStatus));
        result = DisabledDefaultUd2VectorStatePreTerminalContract
            .EvaluateCodeAllocation(
                plan.CodeAllocationStatus,
                plan.CodeAddress);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("allocate-data");
        trace.AllocationCalls.Add(new FakeAllocation(
            "LoaderData",
            plan.DataAddress,
            DisabledDefaultUd2VectorStatePreTerminalContract.DataPageCount,
            plan.DataAllocationStatus));
        result = DisabledDefaultUd2VectorStatePreTerminalContract
            .EvaluateDataAllocation(
                plan.CodeAddress,
                plan.DataAllocationStatus,
                plan.DataAddress);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("materialization");
        result = DisabledDefaultUd2VectorStatePreTerminalContract
            .EvaluateMaterialization(
                plan.CodeAddress,
                plan.DataAddress,
                plan.MaterializationSucceeded);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("read-back");
        result = DisabledDefaultUd2VectorStatePreTerminalContract
            .EvaluateReadBack(
                plan.CodeAddress,
                plan.DataAddress,
                plan.ReadBackSucceeded);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("marker-transport");
        result = DisabledDefaultUd2VectorStatePreTerminalContract
            .EvaluateMarkerTransport(
                plan.CodeAddress,
                plan.DataAddress,
                plan.MarkerTransportSucceeded);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("publication");
        result = DisabledDefaultUd2VectorStatePreTerminalContract
            .EvaluatePublication(
                plan.CodeAddress,
                plan.DataAddress,
                plan.PublicationSucceeded);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Stage = result.Stage;
        trace.Status = result.Status;
        trace.Succeeded = true;
        trace.TerminalAllowed = true;
        trace.Events.Add("terminal-allowed");
        return trace;
    }

    private static FakeTrace FailFakeProbe(
        FakeTrace trace,
        DisabledDefaultUd2VectorStatePreTerminalResult result)
    {
        trace.Stage = result.Stage;
        trace.Status = result.Status;
        if (result.ReleaseData)
        {
            trace.ReleaseCalls.Add(new FakeRelease(
                result.DataAddress,
                DisabledDefaultUd2VectorStatePreTerminalContract.DataPageCount));
            trace.Events.Add("free-data");
        }
        if (result.ReleaseCode)
        {
            trace.ReleaseCalls.Add(new FakeRelease(
                result.CodeAddress,
                DisabledDefaultUd2VectorStatePreTerminalContract.CodePageCount));
            trace.Events.Add("free-code");
        }

        trace.FailureEmitted = true;
        trace.Events.Add("failure");
        return trace;
    }

    private static bool IsFakeFailure(
        FakeTrace trace,
        DisabledDefaultUd2VectorStatePreTerminalStage stage,
        DisabledDefaultUd2VectorStatePreTerminalStatus status,
        int allocationCount)
    {
        return !trace.Succeeded &&
            !trace.TerminalAllowed &&
            trace.FailureEmitted &&
            trace.Stage == stage &&
            trace.Status == status &&
            trace.AllocationCalls.Count == allocationCount;
    }

    private static bool ReverseReleaseExact(FakeTrace trace)
    {
        return trace.ReleaseCalls.SequenceEqual(
            [
                new FakeRelease(ValidDataAddress, 5),
                new FakeRelease(ValidCodeAddress, 2)
            ]);
    }

    private static bool TestPreparationMaterializationAndReadBack()
    {
        string source = ReadEfiEntry();
        string preparation = ExtractBlock(
            source,
            "private static bool PrepareM02Ud2VectorStateProbe(",
            "private static bool ValidateM02Ud2VectorStateResources()");
        string existingStateBranch = ExtractBlock(
            preparation,
            "if (serviceEvaluation.Status ==",
            "if (!serviceEvaluation.Succeeded)");

        int serviceEvaluation = preparation.IndexOf(
            ".EvaluateServices(",
            StringComparison.Ordinal);
        int codeAllocation = preparation.IndexOf(
            "M02Ud2VectorStateEfiLoaderCode",
            StringComparison.Ordinal);
        int codeOwnership = preparation.IndexOf(
            "codeAllocated = firmwareStatus == " +
            "M02Ud2VectorStateEfiSuccess;",
            StringComparison.Ordinal);
        int codeEvaluation = preparation.IndexOf(
            ".EvaluateCodeAllocation(firmwareStatus, codeAddress);",
            StringComparison.Ordinal);
        int dataAllocation = preparation.IndexOf(
            "M02Ud2VectorStateEfiLoaderData",
            StringComparison.Ordinal);
        int dataOwnership = preparation.IndexOf(
            "dataAllocated = firmwareStatus == " +
            "M02Ud2VectorStateEfiSuccess;",
            StringComparison.Ordinal);
        int dataEvaluation = preparation.IndexOf(
            ".EvaluateDataAllocation(",
            StringComparison.Ordinal);
        int codeZero = preparation.IndexOf(
            "code[offset] = 0;",
            StringComparison.Ordinal);
        int dataZero = preparation.IndexOf(
            "data[offset] = 0;",
            StringComparison.Ordinal);
        int normalizedMaterialize = preparation.IndexOf(
            ".NormalizeErrorCodeNonErrorCodeInterruptFrame\n" +
            "                    .Materialize(",
            StringComparison.Ordinal);
        int normalizedReadBack = preparation.IndexOf(
            ".NormalizeErrorCodeNonErrorCodeInterruptFrame\n" +
            "                    .ValidateReadBack(",
            StringComparison.Ordinal);
        int idtMaterialize = preparation.IndexOf(
            ".PopulateAllN256IDTVectorsOwnedGatesNever\n" +
            "                .Materialize(",
            StringComparison.Ordinal);
        int idtReadBack = preparation.IndexOf(
            ".PopulateAllN256IDTVectorsOwnedGatesNever\n" +
            "                .ValidateReadBack(",
            StringComparison.Ordinal);
        int contextPrepare = preparation.IndexOf(
            ".DisabledDefaultUd2VectorStateTransition\n" +
            "                    .PrepareContext(",
            StringComparison.Ordinal);
        int streamEmit = preparation.IndexOf(
            ".DisabledDefaultUd2VectorStateTransition\n" +
            "                    .EmitStream(",
            StringComparison.Ordinal);
        int streamReadBack = preparation.IndexOf(
            ".DisabledDefaultUd2VectorStateTransition\n" +
            "                    .ValidateStreamReadBack(",
            StringComparison.Ordinal);
        int gdtReadBack = preparation.IndexOf(
            ".OwnedX64GdtContainingNullN64BitCode\n" +
            "                .Verify(",
            StringComparison.Ordinal);
        int contextTail = preparation.IndexOf(
            ".ContextByteCount,\n" +
            "                M02Ud2VectorStatePageSize)",
            StringComparison.Ordinal);
        int zeroIstPages = preparation.IndexOf(
            "AreM02Ud2VectorStateIstPagesZero(data)",
            StringComparison.Ordinal);
        int materializationEvaluation = preparation.IndexOf(
            ".EvaluateMaterialization(",
            StringComparison.Ordinal);
        int readBackEvaluation = preparation.IndexOf(
            ".EvaluateReadBack(",
            StringComparison.Ordinal);
        int saveCode = preparation.IndexOf(
            "s_m02Ud2VectorStateCodeAddress = codeAddress;",
            StringComparison.Ordinal);
        int saveData = preparation.IndexOf(
            "s_m02Ud2VectorStateDataAddress = dataAddress;",
            StringComparison.Ordinal);
        int saveState = preparation.IndexOf(
            "s_m02Ud2VectorStateState = M02Ud2VectorStatePreparedState;",
            StringComparison.Ordinal);

        return preparation.Length != 0
            && source.Contains(
                "private const byte M02Ud2VectorStatePreparedState = 1;",
                StringComparison.Ordinal)
            && source.Contains(
                "private const byte M02Ud2VectorStateEnteredState = 2;",
                StringComparison.Ordinal)
            && existingStateBranch.Contains(
                "s_m02Ud2VectorStateState ==\n" +
                "                    M02Ud2VectorStatePreparedState &&\n" +
                "                bootServices != null &&\n" +
                "                bootServices->FreePages != null",
                StringComparison.Ordinal)
            && existingStateBranch.Contains(
                "s_m02Ud2VectorStateCodeAddress,\n" +
                "                    true,\n" +
                "                    s_m02Ud2VectorStateDataAddress,\n" +
                "                    true);",
                StringComparison.Ordinal)
            && CountOccurrences(
                existingStateBranch,
                "FailM02Ud2VectorStateBeforeEntry(") == 1
            && existingStateBranch.LastIndexOf(
                "return false;",
                StringComparison.Ordinal) >
                existingStateBranch.IndexOf(
                    "FailM02Ud2VectorStateBeforeEntry(",
                    StringComparison.Ordinal)
            && !existingStateBranch.Contains(
                "AllocatePages(",
                StringComparison.Ordinal)
            && !existingStateBranch.Contains(
                "s_m02Ud2VectorStateCodeAddress = 0",
                StringComparison.Ordinal)
            && !existingStateBranch.Contains(
                "s_m02Ud2VectorStateDataAddress = 0",
                StringComparison.Ordinal)
            && CountOccurrences(
                preparation,
                "bootServices->AllocatePages(") == 2
            && CountOccurrences(
                preparation,
                ".DisabledDefaultUd2VectorStatePreTerminalContract") == 5
            && serviceEvaluation >= 0
            && codeAllocation > serviceEvaluation
            && codeOwnership > codeAllocation
            && codeEvaluation > codeOwnership
            && dataAllocation > codeEvaluation
            && dataOwnership > dataAllocation
            && dataEvaluation > dataOwnership
            && preparation.Contains(
                "M02Ud2VectorStateCodePageCount,\n" +
                "            &codeAddress);",
                StringComparison.Ordinal)
            && preparation.Contains(
                "M02Ud2VectorStateDataPageCount,\n" +
                "            &dataAddress);",
                StringComparison.Ordinal)
            && codeZero > dataEvaluation
            && dataZero > codeZero
            && normalizedMaterialize > dataZero
            && normalizedReadBack > normalizedMaterialize
            && idtMaterialize > normalizedReadBack
            && idtReadBack > idtMaterialize
            && contextPrepare > idtReadBack
            && streamEmit > contextPrepare
            && streamReadBack > streamEmit
            && gdtReadBack > streamReadBack
            && contextTail > gdtReadBack
            && zeroIstPages > contextTail
            && materializationEvaluation > contextPrepare
            && readBackEvaluation > materializationEvaluation
            && saveCode > readBackEvaluation
            && saveData > saveCode
            && saveState > saveData
            && HasExactCompositeAddresses(preparation)
            && preparation.Contains(
                "contextBytes + 24,\n" +
                "                    40,\n" +
                "                    contextAddress + 64,\n" +
                "                    103",
                StringComparison.Ordinal)
            && preparation.Contains(
                "contextPrepared.ContextFingerprint ==\n" +
                "                streamEmitted.ContextFingerprint",
                StringComparison.Ordinal)
            && preparation.Contains(
                "streamEmitted.StreamFingerprint ==\n" +
                "                streamReadBack.StreamFingerprint",
                StringComparison.Ordinal)
            && preparation.Contains(
                "M02Ud2VectorStateGdtResultMatches(gdtReadBack)",
                StringComparison.Ordinal);
    }

    private static bool TestPreEntryValidation()
    {
        string validation = ExtractBlock(
            ReadEfiEntry(),
            "private static bool ValidateM02Ud2VectorStateResources()",
            "private static bool M02Ud2VectorStateNormalizedResultMatches(");

        return validation.Length != 0
            && HasExactCompositeAddresses(validation)
            && validation.Contains(
                ".NormalizeErrorCodeNonErrorCodeInterruptFrame\n" +
                "                    .ValidateReadBack(",
                StringComparison.Ordinal)
            && validation.Contains(
                ".PopulateAllN256IDTVectorsOwnedGatesNever\n" +
                "                .ValidateReadBack(",
                StringComparison.Ordinal)
            && validation.Contains(
                ".DisabledDefaultUd2VectorStateTransition\n" +
                "                    .ValidateStreamReadBack(",
                StringComparison.Ordinal)
            && validation.Contains(
                ".OwnedX64GdtContainingNullN64BitCode\n" +
                "                .Verify(",
                StringComparison.Ordinal)
            && validation.Contains(
                "M02Ud2VectorStateGdtResultMatches(gdtReadBack)",
                StringComparison.Ordinal)
            && validation.Contains(
                ".ContextByteCount,\n" +
                "                M02Ud2VectorStatePageSize)",
                StringComparison.Ordinal)
            && validation.Contains(
                "AreM02Ud2VectorStateIstPagesZero(data)",
                StringComparison.Ordinal)
            && !validation.Contains(".Materialize(", StringComparison.Ordinal)
            && !validation.Contains(".PrepareContext(", StringComparison.Ordinal)
            && !validation.Contains(".EmitStream(", StringComparison.Ordinal)
            && !validation.Contains("code[offset] =", StringComparison.Ordinal)
            && !validation.Contains("data[offset] =", StringComparison.Ordinal);
    }

    private static bool TestCleanupAndMarkers()
    {
        string source = ReadEfiEntry();
        string transitionSource = ReadTransitionSource();
        string cleanupPlan = ExtractBlock(
            source,
            "private static byte GetM02Ud2VectorStateCleanupPlan(",
            "private static bool FailM02Ud2VectorStateBeforeEntry(");
        string cleanup = ExtractBlock(
            source,
            "private static bool FailM02Ud2VectorStateBeforeEntry(",
            "private static void WriteM02Ud2VectorStateFirmwareFailureLines(");
        string directStartWriter = ExtractBlock(
            source,
            "internal static bool TryWriteM02Ud2VectorStateStartLine()",
            "#endif\n\n        // NativeAOT emits GS-cookie");

        int dataFree = cleanup.IndexOf(
            "dataAddress,\n                    " +
            "M02Ud2VectorStateDataPageCount",
            StringComparison.Ordinal);
        int codeFree = cleanup.IndexOf(
            "codeAddress,\n                    " +
            "M02Ud2VectorStateCodePageCount",
            StringComparison.Ordinal);
        int clearCode = cleanup.IndexOf(
            "s_m02Ud2VectorStateCodeAddress = 0;",
            StringComparison.Ordinal);
        int clearData = cleanup.IndexOf(
            "s_m02Ud2VectorStateDataAddress = 0;",
            StringComparison.Ordinal);
        int failState = cleanup.IndexOf(
            "s_m02Ud2VectorStateState = M02Ud2VectorStateFailedState;",
            StringComparison.Ordinal);
        int failureLines = cleanup.IndexOf(
            "WriteM02Ud2VectorStateFirmwareFailureLines(console, serial);",
            StringComparison.Ordinal);

        return cleanupPlan.Contains(
                ".DisabledDefaultUd2VectorStatePreTerminalContract\n" +
                "            .PlanCleanup(",
                StringComparison.Ordinal)
            && cleanupPlan.Contains(
                ".DisabledDefaultUd2VectorStatePreTerminalContract\n" +
                "            .AllocationRangesDefinitelyDisjoint(",
                StringComparison.Ordinal)
            && DisabledDefaultUd2VectorStatePreTerminalContract.PlanCleanup(
                true,
                ValidCodeAddress,
                true,
                ValidDataAddress) ==
                (DisabledDefaultUd2VectorStatePreTerminalContract
                    .ReleaseDataMask |
                 DisabledDefaultUd2VectorStatePreTerminalContract
                    .ReleaseCodeMask)
            && DisabledDefaultUd2VectorStatePreTerminalContract.PlanCleanup(
                true,
                ValidCodeAddress,
                true,
                ValidCodeAddress) ==
                DisabledDefaultUd2VectorStatePreTerminalContract
                    .ReleaseDataMask
            && DisabledDefaultUd2VectorStatePreTerminalContract.PlanCleanup(
                true,
                0,
                false,
                0) ==
                DisabledDefaultUd2VectorStatePreTerminalContract
                    .ReleaseCodeMask
            && CountOccurrences(cleanup, "bootServices->FreePages(") == 2
            && cleanup.Contains(
                "if ((cleanupPlan & 1) != 0)",
                StringComparison.Ordinal)
            && cleanup.Contains(
                "if ((cleanupPlan & 2) != 0)",
                StringComparison.Ordinal)
            && dataFree >= 0
            && codeFree > dataFree
            && clearCode > codeFree
            && clearData > clearCode
            && failState > clearData
            && failureLines > failState
            && cleanup.Contains("return false;", StringComparison.Ordinal)
            && !cleanup.Contains("return true;", StringComparison.Ordinal)
            && directStartWriter.Contains(
                "s_m02CheckedPortIoBinding.Binding",
                StringComparison.Ordinal)
            && directStartWriter.Contains(
                "Com1OwnedPortIoBackend backend",
                StringComparison.Ordinal)
            && directStartWriter.Contains("poll < 128", StringComparison.Ordinal)
            && directStartWriter.Contains(
                ".LineStatusRegisterPort",
                StringComparison.Ordinal)
            && directStartWriter.Contains(
                ".DataRegisterPort",
                StringComparison.Ordinal)
            && directStartWriter.Contains("backend.ReadByte(", StringComparison.Ordinal)
            && directStartWriter.Contains("backend.WriteByte(", StringComparison.Ordinal)
            && !directStartWriter.Contains(
                "Write(console",
                StringComparison.Ordinal)
            && ExtractStartMarker(source) == GuestStartMarker
            && ExtractPackedFailureMarker(
                source,
                "M02Ud2VectorStateFailureHeadingLine",
                GuestFailureHeading.Length) == GuestFailureHeading
            && ExtractPackedFailureMarker(
                source,
                "M02Ud2VectorStateFailureDetailLine",
                GuestFailureDetail.Length) == GuestFailureDetail
            && ExtractPackedTransitionTranscript(
                transitionSource,
                "private static void EmitSuccessTranscript(",
                "private static void EmitFailureTranscript(")
                .EndsWith(GuestPassMarker + "\r\n", StringComparison.Ordinal)
            && ExtractPackedTransitionTranscript(
                transitionSource,
                "private static void EmitFailureTranscript(",
                "private static ulong ComputePreparedContextFingerprint(") ==
                GuestFailureHeading + "\r\n" + GuestFailureDetail + "\r\n";
    }

    private static bool TestTerminalEntryAndBootOrdering()
    {
        string source = ReadEfiEntry();
        string entry = ExtractBlock(
            source,
            "private static void EnterM02Ud2VectorStateProbe(",
            "private static byte GetM02Ud2VectorStateCleanupPlan(");
        string continuation = ExtractBlock(
            source,
            "private static void M01StageOneContinuation(",
            "private static bool FailBeforeTransfer(");
        string efiMain = ExtractBlock(
            source,
            "private static long EfiMain(",
            "private static void Main()");

        int validate = entry.IndexOf(
            "ValidateM02Ud2VectorStateResources()",
            StringComparison.Ordinal);
        int start = entry.IndexOf(
            "TryWriteM02Ud2VectorStateStartLine()",
            StringComparison.Ordinal);
        int functionPointer = entry.IndexOf(
            "delegate* unmanaged<",
            StringComparison.Ordinal);
        int transitionOffset = entry.IndexOf(
            ".TransitionEntryCompositeOffset",
            StringComparison.Ordinal);
        int contextAddress = entry.IndexOf(
            "s_m02Ud2VectorStateDataAddress +\n" +
            "                        M02Ud2VectorStatePageSize",
            StringComparison.Ordinal);
        int publicationEvaluation = entry.IndexOf(
            ".EvaluatePublication(",
            StringComparison.Ordinal);
        int serialize = entry.IndexOf(
            "X64InstructionPublication.Serialize()",
            StringComparison.Ordinal);
        int publicationGuard = entry.IndexOf(
            "if (!publicationEvaluation.Succeeded)",
            StringComparison.Ordinal);
        int enteredState = entry.IndexOf(
            "s_m02Ud2VectorStateState = M02Ud2VectorStateEnteredState;",
            StringComparison.Ordinal);
        int invoke = entry.IndexOf(
            "terminalEntry(context);",
            StringComparison.Ordinal);
        int spin = entry.LastIndexOf(
            "SpinM02Ud2VectorStateProbe();",
            StringComparison.Ordinal);
        string postInvocation = invoke >= 0
            ? entry[invoke..]
            : string.Empty;

        int bootPlan = continuation.IndexOf(
            "EfiEntry.WriteBootSequencePlan(",
            StringComparison.Ordinal);
        int prepare = continuation.IndexOf(
            "EfiEntry.PrepareM02Ud2VectorStateProbe(",
            StringComparison.Ordinal);
        int m02013 = continuation.IndexOf(
            "EfiEntry.EnterM02Ud2VectorStateProbe(",
            StringComparison.Ordinal);
        int m02009 = continuation.IndexOf(
            "EfiEntry.EnterM02OwnedGdtTssTerminalProbe(",
            StringComparison.Ordinal);

        int resolveConsole = efiMain.IndexOf(
            "GetFirmwareConsole(systemTable)",
            StringComparison.Ordinal);
        int resolveSerial = efiMain.IndexOf(
            "GetFirmwareSerial(systemTable)",
            StringComparison.Ordinal);
        int bootMarkers = efiMain.IndexOf(
            "WriteBootMarkers(",
            StringComparison.Ordinal);

        return validate >= 0
            && start > validate
            && functionPointer > start
            && transitionOffset > functionPointer
            && contextAddress > transitionOffset
            && publicationEvaluation > contextAddress
            && serialize > publicationEvaluation
            && publicationGuard > serialize
            && enteredState > publicationGuard
            && invoke > enteredState
            && spin > invoke
            && CountOccurrences(entry, "X64InstructionPublication.Serialize()") == 1
            && CountOccurrences(entry, ".EvaluatePublication(") == 1
            && CountOccurrences(entry, "terminalEntry(context);") == 1
            && entry.Contains(
                "X64InstructionPublication.Serialize() ==\n" +
                "                        X64InstructionPublication." +
                "ExpectedSuccessCookie",
                StringComparison.Ordinal)
            && entry.Contains(
                "s_m02Ud2VectorStateCodeAddress +\n" +
                "                        (ulong)OS.Kernel.Diagnostics\n" +
                "                            .DisabledDefaultUd2VectorStateTransition\n" +
                "                            .TransitionEntryCompositeOffset",
                StringComparison.Ordinal)
            && !postInvocation.Contains("return", StringComparison.Ordinal)
            && !postInvocation.Contains("bootServices->", StringComparison.Ordinal)
            && !postInvocation.Contains("Write(", StringComparison.Ordinal)
            && !postInvocation.Contains("WriteCrLf(", StringComparison.Ordinal)
            && DisabledDefaultUd2VectorStateTransition
                .TransitionEntryCompositeOffset == 0x1800
            && DisabledDefaultUd2VectorStateTransition
                .ContextDataOffset == 0x1000
            && !DisabledDefaultUd2VectorStateTransition.HasReturns
            && !DisabledDefaultUd2VectorStateTransition.UsesFirmwareCallbacks
            && !DisabledDefaultUd2VectorStateTransition.ContinuesExecution
            && DisabledDefaultUd2VectorStateTransition.TerminalHalt
            && bootPlan >= 0
            && prepare > bootPlan
            && m02013 > prepare
            && m02009 > m02013
            && CountOccurrences(
                continuation,
                "EfiEntry.PrepareM02Ud2VectorStateProbe(") == 1
            && CountOccurrences(
                continuation,
                "EfiEntry.EnterM02Ud2VectorStateProbe(") == 1
            && resolveConsole >= 0
            && resolveSerial > resolveConsole
            && bootMarkers > resolveSerial
            && CountOccurrences(
                efiMain,
                "PrepareM02Ud2VectorStateProbe(") == 0
            && CountOccurrences(
                efiMain,
                "FailM02Ud2VectorStateBeforeEntry(") == 0
            && CountOccurrences(efiMain, "AllocatePages(") == 0
            && !efiMain.Contains(
                "s_m02Ud2VectorState",
                StringComparison.Ordinal);
    }

    private static bool HasExactCompositeAddresses(string source)
    {
        return source.Contains(
                "ulong commonTargetAddress =\n" +
                "            codeAddress + M02Ud2VectorStatePageSize;",
                StringComparison.Ordinal)
            && source.Contains(
                "ulong idtAddress = dataAddress;",
                StringComparison.Ordinal)
            && source.Contains(
                "ulong contextAddress =\n" +
                "            dataAddress + M02Ud2VectorStatePageSize;",
                StringComparison.Ordinal)
            && source.Contains(
                "ulong ist1StackTop =\n" +
                "            dataAddress + (3 * M02Ud2VectorStatePageSize);",
                StringComparison.Ordinal)
            && source.Contains(
                "ulong ist2StackTop =\n" +
                "            dataAddress + (4 * M02Ud2VectorStatePageSize);",
                StringComparison.Ordinal)
            && source.Contains(
                "ulong ist3StackTop =\n" +
                "            dataAddress + (5 * M02Ud2VectorStatePageSize);",
                StringComparison.Ordinal)
            && source.Contains(
                "byte* contextBytes = data + M02Ud2VectorStatePageSize;",
                StringComparison.Ordinal)
            && source.Contains(
                "byte* stream = code + M02Ud2VectorStatePageSize;",
                StringComparison.Ordinal);
    }

    private static string ExtractStartMarker(string source)
    {
        string method = ExtractBlock(
            source,
            "internal static bool TryGetM02Ud2VectorStateStartLineByte(",
            "private static void SpinM02Ud2VectorStateProbe()");
        MatchCollection characters = Regex.Matches(
            method,
            @"\(byte\)'(?<value>.)'",
            RegexOptions.CultureInvariant);
        return string.Concat(
            characters
                .Cast<Match>()
                .Select(match => match.Groups["value"].Value));
    }

    private static string ExtractPackedFailureMarker(
        string source,
        string lineKind,
        int byteCount)
    {
        string method = ExtractBlock(
            source,
            "private static bool TryGetM02Ud2VectorStateFailureLineByte(",
            "internal static bool TryGetM02Ud2VectorStateStartLineByte(");
        Match switchMatch = Regex.Match(
            method,
            @"if \(lineKind == " + Regex.Escape(lineKind) +
            @"\)\s*\{\s*word = wordIndex switch\s*\{" +
            @"(?<cases>.*?)\};\s*\}",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        if (!switchMatch.Success)
        {
            if (lineKind != "M02Ud2VectorStateFailureDetailLine")
            {
                return string.Empty;
            }

            switchMatch = Regex.Match(
                method,
                @"else\s*\{\s*word = wordIndex switch\s*\{" +
                @"(?<cases>.*?)\};\s*\}",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);
        }

        MatchCollection words = Regex.Matches(
            switchMatch.Groups["cases"].Value,
            @"(?<index>\d+)\s*=>\s*0x(?<hex>[0-9a-fA-F_]+)UL",
            RegexOptions.CultureInvariant);
        byte[] bytes = new byte[words.Count * sizeof(ulong)];
        foreach (Match wordMatch in words)
        {
            int wordIndex = int.Parse(
                wordMatch.Groups["index"].Value,
                CultureInfo.InvariantCulture);
            ulong word = ulong.Parse(
                wordMatch.Groups["hex"].Value.Replace(
                    "_",
                    string.Empty,
                    StringComparison.Ordinal),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            if (wordIndex < 0 || wordIndex >= words.Count)
            {
                return string.Empty;
            }

            for (int byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
            {
                bytes[(wordIndex * sizeof(ulong)) + byteIndex] =
                    (byte)(word >> (byteIndex * 8));
            }
        }

        return byteCount <= bytes.Length
            ? Encoding.ASCII.GetString(bytes, 0, byteCount)
            : string.Empty;
    }

    private static string ExtractPackedTransitionTranscript(
        string source,
        string startToken,
        string endToken)
    {
        string method = ExtractBlock(source, startToken, endToken);
        MatchCollection emissions = Regex.Matches(
            method,
            @"builder\.Emit(?<width>U64|U32|U16)?\(0x" +
            @"(?<hex>[0-9a-fA-F_]+)(?:UL|U)?\);",
            RegexOptions.CultureInvariant);
        List<byte> bytes = [];
        foreach (Match emission in emissions)
        {
            int width = emission.Groups["width"].Value switch
            {
                "U64" => sizeof(ulong),
                "U32" => sizeof(uint),
                "U16" => sizeof(ushort),
                _ => sizeof(byte),
            };
            ulong value = ulong.Parse(
                emission.Groups["hex"].Value.Replace(
                    "_",
                    string.Empty,
                    StringComparison.Ordinal),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            for (int byteIndex = 0; byteIndex < width; byteIndex++)
            {
                bytes.Add((byte)(value >> (byteIndex * 8)));
            }
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static string[] ArtifactOutputPaths(string artifactPath)
    {
        return
        [
            artifactPath,
            artifactPath + ".link.map",
            artifactPath + ".link.rsp",
            artifactPath + ".link.json",
            artifactPath + ".serializer.obj"
        ];
    }

    private static void DeleteOwnedFiles(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static ScriptResult RunPowerShellScript(
        string root,
        string script,
        int timeoutMilliseconds,
        params string[] arguments)
    {
        string systemPowerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        ProcessStartInfo start = new(
            File.Exists(systemPowerShell) ? systemPowerShell : "powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = root
        };
        string windowsPowerShellModules = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "Modules");
        string inheritedModulePath =
            Environment.GetEnvironmentVariable("PSModulePath") ?? string.Empty;
        start.Environment["PSModulePath"] = inheritedModulePath.Length == 0
            ? windowsPowerShellModules
            : windowsPowerShellModules +
                Path.PathSeparator + inheritedModulePath;
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-NonInteractive");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ??
            throw new InvalidOperationException("Could not start PowerShell.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(
                "M02-013 PowerShell command timed out: " + script);
        }

        return new ScriptResult(
            process.ExitCode,
            stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }

    private static bool ValidateLinkEvidence(
        string root,
        string artifactPath,
        bool enabled,
        SourceAuthoritySnapshot expected)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(artifactPath + ".link.json"));
        JsonElement evidence = document.RootElement;
        JsonElement evaluation = evidence.GetProperty("postRestoreEvaluation");
        JsonElement properties = evaluation.GetProperty("properties");
        JsonElement[] inputElements = evidence
            .GetProperty("sourceClosureInputs")
            .EnumerateArray()
            .ToArray();
        string[] expectedPaths = ExpectedSourceAuthorityPaths(root);
        string[] actualPaths = inputElements
            .Select(input =>
                input.GetProperty("path").GetString() ?? string.Empty)
            .ToArray();
        bool inputHashesExact = inputElements.All(input =>
        {
            string relativePath =
                input.GetProperty("path").GetString() ?? string.Empty;
            string evidenceSha =
                input.GetProperty("sha256").GetString() ?? string.Empty;
            return relativePath.Length != 0 &&
                evidenceSha == Sha256File(Path.Combine(
                    root,
                    relativePath.Replace('/', '\\')));
        });
        FileInfo artifact = new(artifactPath);

        return evidence.GetProperty("schemaVersion").GetInt32() == 3
            && evidence.GetProperty("configuration").GetString() == "Release"
            && evidence.GetProperty("runtimeIdentifier").GetString() == "win-x64"
            && evidence.GetProperty("artifactBytes").GetInt64() == artifact.Length
            && evidence.GetProperty("artifactSha256").GetString() ==
                Sha256File(artifactPath)
            && evidence.GetProperty("linkerMapSha256").GetString() ==
                Sha256File(artifactPath + ".link.map")
            && evidence.GetProperty("linkResponseSha256").GetString() ==
                Sha256File(artifactPath + ".link.rsp")
            && evidence.GetProperty("serializerObjectSha256").GetString() ==
                Sha256File(artifactPath + ".serializer.obj")
            && evidence.GetProperty("sourceClosureSha256").GetString() ==
                expected.SourceClosureSha256
            && evidence.GetProperty("sourceProjectSha256").GetString() ==
                expected.ProjectSha256
            && evidence.GetProperty("entrySourceSha256").GetString() ==
                expected.EntrySha256
            && evaluation.GetProperty("compileCount").GetInt32() ==
                expected.CompileCount
            && evaluation.GetProperty("compileSha256").GetString() ==
                expected.CompileSha256
            && evaluation.GetProperty("projectReferenceCount").GetInt32() == 0
            && evaluation.GetProperty("referenceCount").GetInt32() == 0
            && evaluation.GetProperty("analyzerCount").GetInt32() == 2
            && properties.GetProperty("TargetFramework").GetString() == "net10.0"
            && properties.GetProperty("RuntimeIdentifier").GetString() == "win-x64"
            && properties.GetProperty("PublishAot").GetString() == "true"
            && properties.GetProperty("SelfContained").GetString() == "true"
            && properties.GetProperty("PublishTrimmed").GetString() == "true"
            && properties.GetProperty("DisableFrameworkLibGeneration")
                .GetString() == "true"
            && properties.GetProperty("EnableM02OwnedCom1ReadyProbe")
                .GetString() == (enabled ? "true" : "false")
            && properties.GetProperty("EnableM02PanicSafeOwnedCom1Probe")
                .GetString() == (enabled ? "true" : "false")
            && properties.GetProperty("EnableM02Ud2VectorStateProbe")
                .GetString() == (enabled ? "true" : "false")
            && properties.GetProperty("EnableM02OwnedX64GdtEncodingProbe")
                .GetString() == "false"
            && properties.GetProperty("EnableM02GuardedIstStackAllocationProbe")
                .GetString() == "false"
            && properties.GetProperty("EnableM02OwnedGdtTssTerminalProbe")
                .GetString() == "false"
            && properties.GetProperty(
                    "EnableM02NormalizedInterruptFramePageProbe")
                .GetString() == "false"
            && properties.GetProperty("EnableM02OwnedIdtGatePopulationProbe")
                .GetString() == "false"
            && properties.GetProperty("EnableM02IdtrContinuedExecutionProbe")
                .GetString() == "false"
            && properties.GetProperty(
                    "EnableM04IndependentEndianConformanceProbe")
                .GetString() == "false"
            && properties.GetProperty(
                    "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe")
                .GetString() == "false"
            && inputElements.Length == expected.SourceInputCount
            && actualPaths.SequenceEqual(expectedPaths, StringComparer.Ordinal)
            && inputHashesExact;
    }

    private static SourceAuthoritySnapshot CaptureSourceAuthoritySnapshot(
        string root)
    {
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        string entryPath = Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs");
        XDocument project = XDocument.Load(projectPath);
        string[] compileKeys = project
            .Descendants("Compile")
            .Select(item =>
                (item.Attribute("Include")?.Value ?? string.Empty) + "|" +
                (item.Attribute("Link")?.Value ?? string.Empty))
            .ToArray();
        string compileClosure = Sha256Text(
            string.Join("\n", compileKeys) + "\n");
        string[] paths = ExpectedSourceAuthorityPaths(root);
        StringBuilder sourceClosure = new();
        foreach (string relativePath in paths)
        {
            sourceClosure.Append(relativePath);
            sourceClosure.Append('|');
            sourceClosure.Append(Sha256File(Path.Combine(
                root,
                relativePath.Replace('/', '\\'))));
            sourceClosure.Append('\n');
        }

        return new SourceAuthoritySnapshot(
            Sha256File(projectPath),
            Sha256File(entryPath),
            Sha256Text(sourceClosure.ToString()),
            compileClosure,
            paths.Length,
            compileKeys.Length);
    }

    private static string[] ExpectedSourceAuthorityPaths(string root)
    {
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        string projectDirectory = Path.GetDirectoryName(projectPath) ??
            throw new InvalidOperationException("OS project directory is missing.");
        XDocument project = XDocument.Load(projectPath);
        List<string> paths =
        [
            "CLROS.slnx",
            "Directory.Build.props",
            "Directory.Build.targets",
            "global.json",
            "scripts/build-efi-shell.ps1",
            "scripts/test-efi-artifact.ps1",
            "src/OS/OS.csproj",
            "tools/Directory.Build.props",
            "tools/OS.X64TextSerializerGenerator/Amd64CoffWriter.cs",
            "tools/OS.X64TextSerializerGenerator/InstructionPublicationSerializerGenerator.cs",
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.csproj",
            "tools/OS.X64TextSerializerGenerator/OS.X64TextSerializerGenerator.targets",
            "tools/OS.X64TextSerializerGenerator/Program.cs"
        ];
        paths.AddRange(project
            .Descendants("Compile")
            .Select(item => item.Attribute("Include")?.Value ?? string.Empty)
            .Select(include => Path.GetFullPath(
                Path.Combine(projectDirectory, include)))
            .Select(fullPath => Path.GetRelativePath(root, fullPath)
                .Replace('\\', '/')));
        return paths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Sha256Text(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private static string TailForAudit(string value)
    {
        const int MaximumCharacters = 2_000;
        string tail = value.Length <= MaximumCharacters
            ? value
            : value[^MaximumCharacters..];
        return tail
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " | ", StringComparison.Ordinal);
    }

    private static string ReadEfiEntry()
    {
        return File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS",
            "Boot",
            "EfiEntry.cs"));
    }

    private static string ReadTransitionSource()
    {
        return File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Diagnostics",
            "DisabledDefaultUd2VectorStateTransition.cs"));
    }

    private static string ExtractBlock(
        string source,
        string startToken,
        string endToken)
    {
        int start = source.IndexOf(startToken, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : source.IndexOf(
                endToken,
                start + startToken.Length,
                StringComparison.Ordinal);
        return start >= 0 && end > start
            ? source[start..end]
            : string.Empty;
    }

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(
                   value,
                   index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed class FakePlan
    {
        public byte CurrentState { get; init; }

        public bool BootServicesPresent { get; init; } = true;

        public bool AllocatePresent { get; init; } = true;

        public bool FreePresent { get; init; } = true;

        public ulong CodeAllocationStatus { get; init; }

        public ulong CodeAddress { get; init; } = ValidCodeAddress;

        public ulong DataAllocationStatus { get; init; }

        public ulong DataAddress { get; init; } = ValidDataAddress;

        public bool MaterializationSucceeded { get; init; } = true;

        public bool ReadBackSucceeded { get; init; } = true;

        public bool MarkerTransportSucceeded { get; init; } = true;

        public bool PublicationSucceeded { get; init; } = true;
    }

    private sealed class FakeTrace
    {
        public List<string> Events { get; } = [];

        public List<FakeAllocation> AllocationCalls { get; } = [];

        public List<FakeRelease> ReleaseCalls { get; } = [];

        public DisabledDefaultUd2VectorStatePreTerminalStage Stage { get; set; }

        public DisabledDefaultUd2VectorStatePreTerminalStatus Status { get; set; }

        public bool Succeeded { get; set; }

        public bool TerminalAllowed { get; set; }

        public bool FailureEmitted { get; set; }
    }

    private readonly record struct FakeAllocation(
        string MemoryType,
        ulong Address,
        ulong Pages,
        ulong Status);

    private readonly record struct FakeRelease(ulong Address, ulong Pages);

    private readonly record struct SourceAuthoritySnapshot(
        string ProjectSha256,
        string EntrySha256,
        string SourceClosureSha256,
        string CompileSha256,
        int SourceInputCount,
        int CompileCount);

    private readonly record struct ScriptResult(int ExitCode, string Output);
}
