using OS.Tooling;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;

internal static partial class M02Ud2VectorStateEfiIntegrationTests
{
    private const string TestName = "m02.013.efi";
    private const string GuestStartMarker = "TEST_START m02.013";
    private const string GuestPassMarker = "TEST_PASS m02.013";
    private const string GuestFailureHeading = "M02_UD2_VECTOR_STATE_VERIFY_FAILED";
    private const string GuestFailureDetail = "TEST_FAIL m02.013 vector_or_register_mismatch";
    private const ulong ValidCodeAddress = 0x0020_0000UL;
    private const ulong ValidDataAddress = 0x0030_0000UL;
    private static int ExpectedSourceInputCount => EfiEvidence.SourceClosurePaths(HostToolProcess.FindRepositoryRoot()).Count;
    private static int ExpectedCompileCount => OS.Tooling.EfiContracts.CompileItems.Count;

    private const string ExpectedProjectSourceSha256 = OS.Tooling.EfiContracts.ProjectSourceSha256;
    private const string ExpectedEntrySha256 = EfiContracts.EfiEntrySourceSha256;
    private static readonly string ExpectedCompileSha256 = OS.Tooling.EfiContracts.CompileInventorySha256;
    private const string ExpectedTransitionSha256 = EfiContracts.Ud2TransitionSourceSha256;
    private const string HostReadyMarker = "CLROS_M02_013_EFI_HOST_READY codePages=2 dataPages=5 " + "normalizedPage=true ownedIdt=true transitionReadBack=true " + "ownedGdtVerified=true zeroTails=true cleanup=true " + "terminalEntry=C+0x1800 context=D+0x1000 " + "guestExecution=false faultTriggered=false " + "postExitBootServices=false";
    [HostTest("M02-013 C and D allocation topology is exact", HostLane.Fast, 0)]
    private static bool TestAllocationAndCompositeTopology()
    {
        var catalog = Ud2Sources();
        string source = Ud2Constants(catalog);
        string validRange = Ud2Method(catalog, "IsValidM02Ud2VectorStateRange").ToString();
        string overlap = Ud2Method(catalog, "M02Ud2VectorStateRangesOverlap").ToString();
        return HostSourceText.Contains(source, "private const uint M02Ud2VectorStateEfiLoaderCode = 1;")
            && HostSourceText.Contains(source, "private const uint M02Ud2VectorStateEfiLoaderData = 2;")
            && HostSourceText.Contains(source, "private const ulong M02Ud2VectorStatePageSize = 4096;")
            && HostSourceText.Contains(source, "private const ulong M02Ud2VectorStateCodePageCount = 2;")
            && HostSourceText.Contains(source, "private const ulong M02Ud2VectorStateDataPageCount = 5;")
            && HostSourceText.Contains(source, "M02Ud2VectorStateCodePageCount * " + "M02Ud2VectorStatePageSize;")
            && HostSourceText.Contains(source, "M02Ud2VectorStateDataPageCount * " + "M02Ud2VectorStatePageSize;")
            && HostSourceText.Contains(validRange, ".DisabledDefaultUd2VectorStatePreTerminalContract\n" + "            .IsValidRange(address, byteCount);")
            && HostSourceText.Contains(overlap, ".DisabledDefaultUd2VectorStatePreTerminalContract\n" + "            .RangesOverlap(")
            && DisabledDefaultUd2VectorStatePreTerminalContract.PageByteCount == 4096
            && DisabledDefaultUd2VectorStatePreTerminalContract.CodePageCount == 2
            && DisabledDefaultUd2VectorStatePreTerminalContract.DataPageCount == 5
            && DisabledDefaultUd2VectorStatePreTerminalContract.CodeByteCount == 0x2000
            && DisabledDefaultUd2VectorStatePreTerminalContract.DataByteCount == 0x5000
            && DisabledDefaultUd2VectorStatePreTerminalContract.IsValidRange(ValidCodeAddress, DisabledDefaultUd2VectorStatePreTerminalContract.CodeByteCount)
            && !DisabledDefaultUd2VectorStatePreTerminalContract.IsValidRange(0, DisabledDefaultUd2VectorStatePreTerminalContract.CodeByteCount)
            && !DisabledDefaultUd2VectorStatePreTerminalContract.IsValidRange(~0UL - 0x2000 + 1, DisabledDefaultUd2VectorStatePreTerminalContract.CodeByteCount)
            && DisabledDefaultUd2VectorStateTransition.CompositeCodeByteCount == 0x2000
            && DisabledDefaultUd2VectorStateTransition.CompositeDataByteCount == 0x5000
            && DisabledDefaultUd2VectorStateTransition.NormalizedSlotsCompositeOffset == 0x0000
            && DisabledDefaultUd2VectorStateTransition.NormalizedSlotsCompositeCapacity == 0x1000
            && DisabledDefaultUd2VectorStateTransition.CommonHandlerCompositeOffset == 0x1000
            && DisabledDefaultUd2VectorStateTransition.CommonHandlerCompositeCapacity == 0x0800
            && DisabledDefaultUd2VectorStateTransition.TransitionEntryCompositeOffset == 0x1800
            && DisabledDefaultUd2VectorStateTransition.TransitionCompositeCapacity == 0x0800
            && DisabledDefaultUd2VectorStateTransition.IdtDataOffset == 0x0000
            && DisabledDefaultUd2VectorStateTransition.IdtDataCapacity == 0x1000
            && DisabledDefaultUd2VectorStateTransition.ContextDataOffset == 0x1000
            && DisabledDefaultUd2VectorStateTransition.ContextDataCapacity == 0x1000
            && DisabledDefaultUd2VectorStateTransition.Ist1DataOffset == 0x2000
            && DisabledDefaultUd2VectorStateTransition.Ist2DataOffset == 0x3000
            && DisabledDefaultUd2VectorStateTransition.Ist3DataOffset == 0x4000
            && DisabledDefaultUd2VectorStateTransition.Ist1StackTopDataOffset == 0x3000
            && DisabledDefaultUd2VectorStateTransition.Ist2StackTopDataOffset == 0x4000
            && DisabledDefaultUd2VectorStateTransition.Ist3StackTopDataOffset == 0x5000
            && DisabledDefaultUd2VectorStateTransition.StreamByteCount == 0x1000
            && DisabledDefaultUd2VectorStateTransition.ContextByteCount < 0x1000
            && !DisabledDefaultUd2VectorStateTransition.HostTestsInvokeBytes;
    }

    [HostTest("M02-013 default and enabled NativeAOT artifacts prove exact provenance", HostLane.Native, 1)]
    private static bool TestDefaultAndEnabledEfiArtifacts()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string artifactDirectory = Path.Combine(root, "artifacts", "efi");
        string logDirectory = Path.Combine(root, "artifacts", "logs");
        Directory.CreateDirectory(artifactDirectory);
        Directory.CreateDirectory(logDirectory);
        string identity = Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string prefix = "BOOTX64-M02-013-HOSTTEST-" + identity;
        string defaultArtifact = Path.Combine(artifactDirectory, prefix + "-DEFAULT.EFI");
        string enabledArtifact = Path.Combine(artifactDirectory, prefix + "-ENABLED.EFI");
        string defaultLog = Path.Combine(logDirectory, "m02-013-hosttest-" + identity + "-default.log");
        string enabledLog = Path.Combine(logDirectory, "m02-013-hosttest-" + identity + "-enabled.log");
        string[] ownedOutputs = [.. ArtifactOutputPaths(defaultArtifact), .. ArtifactOutputPaths(enabledArtifact), defaultLog, enabledLog];
        SourceAuthoritySnapshot before = CaptureSourceAuthoritySnapshot(root);
        try
        {
            ScriptResult defaultBuild = RunEfiBuild("-EfiOutputPath", defaultArtifact, "-LogPath", defaultLog);
            ScriptResult enabledBuild = RunEfiBuild("-EnableM02Ud2VectorStateProbe", "-EfiOutputPath", enabledArtifact, "-LogPath", enabledLog);
            if (defaultBuild.ExitCode != 0 || enabledBuild.ExitCode != 0)
            {
                Console.WriteLine("M02_013_ARTIFACT_BUILD_AUDIT defaultExit=" + defaultBuild.ExitCode + " enabledExit=" + enabledBuild.ExitCode + " defaultOutput=" + TailForAudit(defaultBuild.Output) + " enabledOutput=" + TailForAudit(enabledBuild.Output));
                return false;
            }

            SourceAuthoritySnapshot after = CaptureSourceAuthoritySnapshot(root);
            string defaultMap = File.ReadAllText(defaultArtifact + ".link.map");
            string enabledMap = File.ReadAllText(enabledArtifact + ".link.map");
            bool defaultExcluded = !defaultMap.Contains("M02Ud2VectorState", StringComparison.Ordinal) && !defaultMap.Contains("DisabledDefaultUd2VectorState", StringComparison.Ordinal);
            string[] requiredEnabledSymbols = ["OS_OS_Boot_EfiEntry__PrepareM02Ud2VectorStateProbe", "OS_OS_Boot_EfiEntry__EnterM02Ud2VectorStateProbe", "OS_OS_Boot_EfiEntry__FailM02Ud2VectorStateBeforeEntry", "DisabledDefaultUd2VectorStateTransition__PrepareContext_0", "DisabledDefaultUd2VectorStateTransition__EmitStream_0", "DisabledDefaultUd2VectorStateTransition__ValidateStreamReadBack_0", "M01StageThreeAdapter__TryWriteM02Ud2VectorStateStartLine"];
            bool enabledIncluded = requiredEnabledSymbols.All(symbol => enabledMap.Contains(symbol, StringComparison.Ordinal));
            bool verifierPassed = defaultBuild.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal) && enabledBuild.Output.Contains("TEST_PASS efi.artifact.verify", StringComparison.Ordinal);
            bool sourceHeadStable = before == after;
            // Reviewed content tolerates checkout line endings; snapshots and link
            // evidence below still bind the exact raw bytes before and after publication.
            (string Name, string Expected, string Actual)[] frozenAuthority = [("projectSourceSha256", ExpectedProjectSourceSha256, ContractAudit.HashSource(File.ReadAllText(Path.Combine(root, "src", "OS", "OS.csproj")))), ("entrySha256", ExpectedEntrySha256, ContractAudit.HashSource(File.ReadAllText(Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs")))), ("compileSha256", ExpectedCompileSha256, before.CompileSha256), ("sourceInputCount", ExpectedSourceInputCount.ToString(CultureInfo.InvariantCulture), before.SourceInputCount.ToString(CultureInfo.InvariantCulture)), ("compileCount", ExpectedCompileCount.ToString(CultureInfo.InvariantCulture), before.CompileCount.ToString(CultureInfo.InvariantCulture)), ("transitionSha256", ExpectedTransitionSha256, ContractAudit.HashSource(File.ReadAllText(Path.Combine(root, "src", "OS.Kernel", "Diagnostics", "DisabledDefaultUd2VectorStateTransition.cs"))))];
            bool frozenAuthorityExact = true;
            foreach (var authority in frozenAuthority)
            {
                if (authority.Expected == authority.Actual)
                    continue;
                frozenAuthorityExact = false;
                Console.WriteLine("M02_013_AUTHORITY_MISMATCH property=" + authority.Name + " expected=" + authority.Expected + " actual=" + authority.Actual);
            }

            bool provenanceExact = sourceHeadStable && frozenAuthorityExact && ValidateLinkEvidence(root, defaultArtifact, enabled: false, before) && ValidateLinkEvidence(root, enabledArtifact, enabled: true, before);
            Console.WriteLine("M02_013_ARTIFACT_BUILD_AUDIT defaultExit=0 enabledExit=0 " + "defaultExcluded=" + defaultExcluded.ToString().ToLowerInvariant() + " enabledIncluded=" + enabledIncluded.ToString().ToLowerInvariant() + " verifierPassed=" + verifierPassed.ToString().ToLowerInvariant() + " sourceHeadStable=" + sourceHeadStable.ToString().ToLowerInvariant() + " frozenAuthorityExact=" + frozenAuthorityExact.ToString().ToLowerInvariant() + " provenanceExact=" + provenanceExact.ToString().ToLowerInvariant());
            return defaultExcluded && enabledIncluded && verifierPassed && provenanceExact;
        }
        finally
        {
            DeleteOwnedFiles(ownedOutputs);
        }
    }

    [HostTest("M02-013 fake firmware executes every pre-terminal contract stage", HostLane.Fast, 2)]
    private static bool TestFakeFirmwareOrchestration()
    {
        FakeTrace existingState = RunFakeProbe(new FakePlan { CurrentState = 1 });
        FakeTrace missingBootServices = RunFakeProbe(new FakePlan { BootServicesPresent = false });
        FakeTrace missingAllocate = RunFakeProbe(new FakePlan { AllocatePresent = false });
        FakeTrace missingFree = RunFakeProbe(new FakePlan { FreePresent = false });
        FakeTrace codeAllocationFailure = RunFakeProbe(new FakePlan { CodeAllocationStatus = 1 });
        FakeTrace dataAllocationFailure = RunFakeProbe(new FakePlan { DataAllocationStatus = 1 });
        FakeTrace codeAddressZero = RunFakeProbe(new FakePlan { CodeAddress = 0 });
        FakeTrace[] otherInvalidCodeRanges = [RunFakeProbe(new FakePlan { CodeAddress = ValidCodeAddress + 1 }), RunFakeProbe(new FakePlan { CodeAddress = 0x0000_8000_0000_0000UL }), RunFakeProbe(new FakePlan { CodeAddress = ~0UL - DisabledDefaultUd2VectorStatePreTerminalContract.CodeByteCount + 1 })];
        FakeTrace dataAddressZero = RunFakeProbe(new FakePlan { DataAddress = 0 });
        FakeTrace[] otherInvalidDataRanges = [RunFakeProbe(new FakePlan { DataAddress = ValidDataAddress + 1 }), RunFakeProbe(new FakePlan { DataAddress = 0x0000_8000_0000_0000UL }), RunFakeProbe(new FakePlan { DataAddress = ~0UL - DisabledDefaultUd2VectorStatePreTerminalContract.DataByteCount + 1 })];
        FakeTrace equalAlias = RunFakeProbe(new FakePlan { DataAddress = ValidCodeAddress });
        FakeTrace partialOverlap = RunFakeProbe(new FakePlan { DataAddress = ValidCodeAddress + 0x1000 });
        FakeTrace materializationFailure = RunFakeProbe(new FakePlan { MaterializationSucceeded = false });
        FakeTrace readBackFailure = RunFakeProbe(new FakePlan { ReadBackSucceeded = false });
        FakeTrace markerFailure = RunFakeProbe(new FakePlan { MarkerTransportSucceeded = false });
        FakeTrace publicationFailure = RunFakeProbe(new FakePlan { PublicationSucceeded = false });
        FakeTrace success = RunFakeProbe(new FakePlan());
        bool serviceFailuresExact = IsFakeFailure(existingState, DisabledDefaultUd2VectorStatePreTerminalStage.Services, DisabledDefaultUd2VectorStatePreTerminalStatus.ExistingState, 0) && IsFakeFailure(missingBootServices, DisabledDefaultUd2VectorStatePreTerminalStage.Services, DisabledDefaultUd2VectorStatePreTerminalStatus.MissingBootServices, 0) && IsFakeFailure(missingAllocate, DisabledDefaultUd2VectorStatePreTerminalStage.Services, DisabledDefaultUd2VectorStatePreTerminalStatus.MissingAllocatePages, 0) && IsFakeFailure(missingFree, DisabledDefaultUd2VectorStatePreTerminalStage.Services, DisabledDefaultUd2VectorStatePreTerminalStatus.MissingFreePages, 0);
        bool allocationFailuresExact = IsFakeFailure(codeAllocationFailure, DisabledDefaultUd2VectorStatePreTerminalStage.CodeAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.CodeAllocationFailed, 1) && codeAllocationFailure.ReleaseCalls.Count == 0 && IsFakeFailure(dataAllocationFailure, DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.DataAllocationFailed, 2) && dataAllocationFailure.ReleaseCalls.SequenceEqual([new FakeRelease(ValidCodeAddress, 2)]);
        bool statusBasedOwnershipExact = IsFakeFailure(codeAddressZero, DisabledDefaultUd2VectorStatePreTerminalStage.CodeAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidCodeRange, 1) && codeAddressZero.ReleaseCalls.SequenceEqual([new FakeRelease(0, 2)]) && otherInvalidCodeRanges.All(trace => IsFakeFailure(trace, DisabledDefaultUd2VectorStatePreTerminalStage.CodeAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidCodeRange, 1) && trace.ReleaseCalls.Count == 1 && trace.ReleaseCalls[0].Pages == 2) && IsFakeFailure(dataAddressZero, DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange, 2) && dataAddressZero.ReleaseCalls.SequenceEqual([new FakeRelease(0, 5), new FakeRelease(ValidCodeAddress, 2)]) && IsFakeFailure(otherInvalidDataRanges[0], DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange, 2) && IsFakeFailure(otherInvalidDataRanges[1], DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange, 2) && otherInvalidDataRanges[0].ReleaseCalls.Count == 2 && otherInvalidDataRanges[0].ReleaseCalls[0].Pages == 5 && otherInvalidDataRanges[0].ReleaseCalls[1] == new FakeRelease(ValidCodeAddress, 2) && otherInvalidDataRanges[1].ReleaseCalls.Count == 2 && otherInvalidDataRanges[1].ReleaseCalls[0].Pages == 5 && otherInvalidDataRanges[1].ReleaseCalls[1] == new FakeRelease(ValidCodeAddress, 2) && IsFakeFailure(otherInvalidDataRanges[2], DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.InvalidDataRange, 2) && otherInvalidDataRanges[2].ReleaseCalls.SequenceEqual([new FakeRelease(~0UL - DisabledDefaultUd2VectorStatePreTerminalContract.DataByteCount + 1, 5)]);
        bool overlapSuppressionExact = IsFakeFailure(equalAlias, DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.OverlappingRanges, 2) && equalAlias.ReleaseCalls.SequenceEqual([new FakeRelease(ValidCodeAddress, 5)]) && IsFakeFailure(partialOverlap, DisabledDefaultUd2VectorStatePreTerminalStage.DataAllocation, DisabledDefaultUd2VectorStatePreTerminalStatus.OverlappingRanges, 2) && partialOverlap.ReleaseCalls.SequenceEqual([new FakeRelease(ValidCodeAddress + 0x1000, 5)]);
        bool stageFailuresExact = IsFakeFailure(materializationFailure, DisabledDefaultUd2VectorStatePreTerminalStage.Materialization, DisabledDefaultUd2VectorStatePreTerminalStatus.MaterializationFailed, 2) && IsFakeFailure(readBackFailure, DisabledDefaultUd2VectorStatePreTerminalStage.ReadBack, DisabledDefaultUd2VectorStatePreTerminalStatus.ReadBackFailed, 2) && IsFakeFailure(markerFailure, DisabledDefaultUd2VectorStatePreTerminalStage.MarkerTransport, DisabledDefaultUd2VectorStatePreTerminalStatus.MarkerTransportFailed, 2) && IsFakeFailure(publicationFailure, DisabledDefaultUd2VectorStatePreTerminalStage.Publication, DisabledDefaultUd2VectorStatePreTerminalStatus.PublicationFailed, 2) && ReverseReleaseExact(materializationFailure) && ReverseReleaseExact(readBackFailure) && ReverseReleaseExact(markerFailure) && ReverseReleaseExact(publicationFailure);
        return serviceFailuresExact && allocationFailuresExact && statusBasedOwnershipExact && overlapSuppressionExact && stageFailuresExact && success.Succeeded && success.TerminalAllowed && !success.FailureEmitted && success.Stage == DisabledDefaultUd2VectorStatePreTerminalStage.Publication && success.Status == DisabledDefaultUd2VectorStatePreTerminalStatus.Success && success.AllocationCalls.SequenceEqual([new FakeAllocation("LoaderCode", ValidCodeAddress, 2, 0), new FakeAllocation("LoaderData", ValidDataAddress, 5, 0)]) && success.ReleaseCalls.Count == 0 && success.Events.SequenceEqual(["services", "allocate-code", "allocate-data", "materialization", "read-back", "marker-transport", "publication", "terminal-allowed"]);
    }

    private static FakeTrace RunFakeProbe(FakePlan plan)
    {
        FakeTrace trace = new();
        trace.Events.Add("services");
        DisabledDefaultUd2VectorStatePreTerminalResult result = DisabledDefaultUd2VectorStatePreTerminalContract.EvaluateServices(plan.CurrentState, plan.BootServicesPresent, plan.AllocatePresent, plan.FreePresent);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("allocate-code");
        trace.AllocationCalls.Add(new FakeAllocation("LoaderCode", plan.CodeAddress, DisabledDefaultUd2VectorStatePreTerminalContract.CodePageCount, plan.CodeAllocationStatus));
        result = DisabledDefaultUd2VectorStatePreTerminalContract.EvaluateCodeAllocation(plan.CodeAllocationStatus, plan.CodeAddress);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("allocate-data");
        trace.AllocationCalls.Add(new FakeAllocation("LoaderData", plan.DataAddress, DisabledDefaultUd2VectorStatePreTerminalContract.DataPageCount, plan.DataAllocationStatus));
        result = DisabledDefaultUd2VectorStatePreTerminalContract.EvaluateDataAllocation(plan.CodeAddress, plan.DataAllocationStatus, plan.DataAddress);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("materialization");
        result = DisabledDefaultUd2VectorStatePreTerminalContract.EvaluateMaterialization(plan.CodeAddress, plan.DataAddress, plan.MaterializationSucceeded);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("read-back");
        result = DisabledDefaultUd2VectorStatePreTerminalContract.EvaluateReadBack(plan.CodeAddress, plan.DataAddress, plan.ReadBackSucceeded);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("marker-transport");
        result = DisabledDefaultUd2VectorStatePreTerminalContract.EvaluateMarkerTransport(plan.CodeAddress, plan.DataAddress, plan.MarkerTransportSucceeded);
        if (!result.Succeeded)
        {
            return FailFakeProbe(trace, result);
        }

        trace.Events.Add("publication");
        result = DisabledDefaultUd2VectorStatePreTerminalContract.EvaluatePublication(plan.CodeAddress, plan.DataAddress, plan.PublicationSucceeded);
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

    private static FakeTrace FailFakeProbe(FakeTrace trace, DisabledDefaultUd2VectorStatePreTerminalResult result)
    {
        trace.Stage = result.Stage;
        trace.Status = result.Status;
        if (result.ReleaseData)
        {
            trace.ReleaseCalls.Add(new FakeRelease(result.DataAddress, DisabledDefaultUd2VectorStatePreTerminalContract.DataPageCount));
            trace.Events.Add("free-data");
        }

        if (result.ReleaseCode)
        {
            trace.ReleaseCalls.Add(new FakeRelease(result.CodeAddress, DisabledDefaultUd2VectorStatePreTerminalContract.CodePageCount));
            trace.Events.Add("free-code");
        }

        trace.FailureEmitted = true;
        trace.Events.Add("failure");
        return trace;
    }

    private static bool IsFakeFailure(FakeTrace trace, DisabledDefaultUd2VectorStatePreTerminalStage stage, DisabledDefaultUd2VectorStatePreTerminalStatus status, int allocationCount)
    {
        return !trace.Succeeded && !trace.TerminalAllowed && trace.FailureEmitted && trace.Stage == stage && trace.Status == status && trace.AllocationCalls.Count == allocationCount;
    }

    private static bool ReverseReleaseExact(FakeTrace trace)
    {
        return trace.ReleaseCalls.SequenceEqual([new FakeRelease(ValidDataAddress, 5), new FakeRelease(ValidCodeAddress, 2)]);
    }

    [HostTest("M02-013 preparation materializes and reads back every authority", HostLane.Fast, 3)]
    private static bool TestPreparationMaterializationAndReadBack()
    {
        var catalog = Ud2Sources();
        string source = Ud2Constants(catalog);
        string preparation = Ud2Method(catalog, "PrepareM02Ud2VectorStateProbe").ToString();
        string existingStateBranch = Ud2ExistingState(Ud2Method(catalog, "PrepareM02Ud2VectorStateProbe")).ToString();
        int serviceEvaluation = HostSourceText.IndexOf(preparation, ".EvaluateServices(");
        int codeAllocation = HostSourceText.IndexOf(preparation, "M02Ud2VectorStateEfiLoaderCode");
        int codeOwnership = HostSourceText.IndexOf(preparation, "codeAllocated = firmwareStatus == " + "M02Ud2VectorStateEfiSuccess;");
        int codeEvaluation = HostSourceText.IndexOf(preparation, ".EvaluateCodeAllocation(firmwareStatus, codeAddress);");
        int dataAllocation = HostSourceText.IndexOf(preparation, "M02Ud2VectorStateEfiLoaderData");
        int dataOwnership = HostSourceText.IndexOf(preparation, "dataAllocated = firmwareStatus == " + "M02Ud2VectorStateEfiSuccess;");
        int dataEvaluation = HostSourceText.IndexOf(preparation, ".EvaluateDataAllocation(");
        int codeZero = HostSourceText.IndexOf(preparation, "code[offset] = 0;");
        int dataZero = HostSourceText.IndexOf(preparation, "data[offset] = 0;");
        int normalizedMaterialize = HostSourceText.IndexOf(preparation, ".NormalizeErrorCodeNonErrorCodeInterruptFrame\n" + "                    .Materialize(");
        int normalizedReadBack = HostSourceText.IndexOf(preparation, ".NormalizeErrorCodeNonErrorCodeInterruptFrame\n" + "                    .ValidateReadBack(");
        int idtMaterialize = HostSourceText.IndexOf(preparation, ".PopulateAllN256IDTVectorsOwnedGatesNever\n" + "                .Materialize(");
        int idtReadBack = HostSourceText.IndexOf(preparation, ".PopulateAllN256IDTVectorsOwnedGatesNever\n" + "                .ValidateReadBack(");
        int contextPrepare = HostSourceText.IndexOf(preparation, ".DisabledDefaultUd2VectorStateTransition\n" + "                    .PrepareContext(");
        int streamEmit = HostSourceText.IndexOf(preparation, ".DisabledDefaultUd2VectorStateTransition\n" + "                    .EmitStream(");
        int streamReadBack = HostSourceText.IndexOf(preparation, ".DisabledDefaultUd2VectorStateTransition\n" + "                    .ValidateStreamReadBack(");
        int gdtReadBack = HostSourceText.IndexOf(preparation, ".OwnedX64GdtContainingNullN64BitCode\n" + "                .Verify(");
        int contextTail = HostSourceText.IndexOf(preparation, ".ContextByteCount,\n" + "                M02Ud2VectorStatePageSize)");
        int zeroIstPages = HostSourceText.IndexOf(preparation, "AreM02Ud2VectorStateIstPagesZero(data)");
        int materializationEvaluation = HostSourceText.IndexOf(preparation, ".EvaluateMaterialization(");
        int readBackEvaluation = HostSourceText.IndexOf(preparation, ".EvaluateReadBack(");
        int saveCode = HostSourceText.IndexOf(preparation, "s_m02Ud2VectorStateCodeAddress = codeAddress;");
        int saveData = HostSourceText.IndexOf(preparation, "s_m02Ud2VectorStateDataAddress = dataAddress;");
        int saveState = HostSourceText.IndexOf(preparation, "s_m02Ud2VectorStateState = M02Ud2VectorStatePreparedState;");
        return preparation.Length != 0
            && HostSourceText.Contains(source, "private const byte M02Ud2VectorStatePreparedState = 1;")
            && HostSourceText.Contains(source, "private const byte M02Ud2VectorStateEnteredState = 2;")
            && HostSourceText.Contains(existingStateBranch, "s_m02Ud2VectorStateState ==\n" + "                    M02Ud2VectorStatePreparedState &&\n" + "                bootServices != null &&\n" + "                bootServices->FreePages != null")
            && HostSourceText.Contains(existingStateBranch, "s_m02Ud2VectorStateCodeAddress,\n" + "                    true,\n" + "                    s_m02Ud2VectorStateDataAddress,\n" + "                    true);")
            && CountOccurrences(existingStateBranch, "FailM02Ud2VectorStateBeforeEntry(") == 1
            && LastUd2Token(existingStateBranch, "return false;") > HostSourceText.IndexOf(existingStateBranch, "FailM02Ud2VectorStateBeforeEntry(")
            && !existingStateBranch.Contains("AllocatePages(", StringComparison.Ordinal)
            && !existingStateBranch.Contains("s_m02Ud2VectorStateCodeAddress = 0", StringComparison.Ordinal)
            && !existingStateBranch.Contains("s_m02Ud2VectorStateDataAddress = 0", StringComparison.Ordinal)
            && CountOccurrences(preparation, "bootServices->AllocatePages(") == 2
            && CountOccurrences(preparation, ".DisabledDefaultUd2VectorStatePreTerminalContract") == 5
            && serviceEvaluation >= 0
            && codeAllocation > serviceEvaluation
            && codeOwnership > codeAllocation
            && codeEvaluation > codeOwnership
            && dataAllocation > codeEvaluation
            && dataOwnership > dataAllocation
            && dataEvaluation > dataOwnership
            && HostSourceText.Contains(preparation, "M02Ud2VectorStateCodePageCount,\n" + "            &codeAddress);")
            && HostSourceText.Contains(preparation, "M02Ud2VectorStateDataPageCount,\n" + "            &dataAddress);")
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
            && HostSourceText.Contains(preparation, "contextBytes + 24,\n" + "                    40,\n" + "                    contextAddress + 64,\n" + "                    103")
            && HostSourceText.Contains(preparation, "contextPrepared.ContextFingerprint ==\n" + "                streamEmitted.ContextFingerprint")
            && HostSourceText.Contains(preparation, "streamEmitted.StreamFingerprint ==\n" + "                streamReadBack.StreamFingerprint")
            && HostSourceText.Contains(preparation, "M02Ud2VectorStateGdtResultMatches(gdtReadBack)");
    }

    [HostTest("M02-013 pre-entry validation is read-only and tail complete", HostLane.Fast, 4)]
    private static bool TestPreEntryValidation()
    {
        string validation = Ud2Method(Ud2Sources(), "ValidateM02Ud2VectorStateResources").ToString();
        return validation.Length != 0
            && HasExactCompositeAddresses(validation)
            && HostSourceText.Contains(validation, ".NormalizeErrorCodeNonErrorCodeInterruptFrame\n" + "                    .ValidateReadBack(")
            && HostSourceText.Contains(validation, ".PopulateAllN256IDTVectorsOwnedGatesNever\n" + "                .ValidateReadBack(")
            && HostSourceText.Contains(validation, ".DisabledDefaultUd2VectorStateTransition\n" + "                    .ValidateStreamReadBack(")
            && HostSourceText.Contains(validation, ".OwnedX64GdtContainingNullN64BitCode\n" + "                .Verify(")
            && HostSourceText.Contains(validation, "M02Ud2VectorStateGdtResultMatches(gdtReadBack)")
            && HostSourceText.Contains(validation, ".ContextByteCount,\n" + "                M02Ud2VectorStatePageSize)")
            && HostSourceText.Contains(validation, "AreM02Ud2VectorStateIstPagesZero(data)")
            && !validation.Contains(".Materialize(", StringComparison.Ordinal)
            && !validation.Contains(".PrepareContext(", StringComparison.Ordinal)
            && !validation.Contains(".EmitStream(", StringComparison.Ordinal)
            && !validation.Contains("code[offset] =", StringComparison.Ordinal)
            && !validation.Contains("data[offset] =", StringComparison.Ordinal);
    }

    [HostTest("M02-013 cleanup and marker transports fail closed", HostLane.Fast, 5)]
    private static bool TestCleanupAndMarkers()
    {
        var catalog = Ud2Sources();
        string cleanupPlan = Ud2Method(catalog, "GetM02Ud2VectorStateCleanupPlan").ToString() + "\n" + Ud2Method(catalog, "M02Ud2VectorStateAllocationRangesDefinitelyDisjoint").ToString();
        string cleanup = Ud2Method(catalog, "FailM02Ud2VectorStateBeforeEntry").ToString();
        string directStartWriter = Ud2Method(catalog, "TryWriteM02Ud2VectorStateStartLine", "OS.Boot.EfiEntry+M01StageThreeAdapter", Microsoft.CodeAnalysis.Accessibility.Internal).ToString();
        int dataFree = HostSourceText.IndexOf(cleanup, "dataAddress,\n                    " + "M02Ud2VectorStateDataPageCount");
        int codeFree = HostSourceText.IndexOf(cleanup, "codeAddress,\n                    " + "M02Ud2VectorStateCodePageCount");
        int clearCode = HostSourceText.IndexOf(cleanup, "s_m02Ud2VectorStateCodeAddress = 0;");
        int clearData = HostSourceText.IndexOf(cleanup, "s_m02Ud2VectorStateDataAddress = 0;");
        int failState = HostSourceText.IndexOf(cleanup, "s_m02Ud2VectorStateState = M02Ud2VectorStateFailedState;");
        int failureLines = HostSourceText.IndexOf(cleanup, "WriteM02Ud2VectorStateFirmwareFailureLines(console, serial);");
        return HostSourceText.Contains(cleanupPlan, ".DisabledDefaultUd2VectorStatePreTerminalContract\n" + "            .PlanCleanup(")
            && HostSourceText.Contains(cleanupPlan, ".DisabledDefaultUd2VectorStatePreTerminalContract\n" + "            .AllocationRangesDefinitelyDisjoint(")
            && DisabledDefaultUd2VectorStatePreTerminalContract.PlanCleanup(true, ValidCodeAddress, true, ValidDataAddress) == (DisabledDefaultUd2VectorStatePreTerminalContract.ReleaseDataMask | DisabledDefaultUd2VectorStatePreTerminalContract.ReleaseCodeMask)
            && DisabledDefaultUd2VectorStatePreTerminalContract.PlanCleanup(true, ValidCodeAddress, true, ValidCodeAddress) == DisabledDefaultUd2VectorStatePreTerminalContract.ReleaseDataMask
            && DisabledDefaultUd2VectorStatePreTerminalContract.PlanCleanup(true, 0, false, 0) == DisabledDefaultUd2VectorStatePreTerminalContract.ReleaseCodeMask
            && CountOccurrences(cleanup, "bootServices->FreePages(") == 2
            && HostSourceText.Contains(cleanup, "if ((cleanupPlan & 1) != 0)")
            && HostSourceText.Contains(cleanup, "if ((cleanupPlan & 2) != 0)")
            && dataFree >= 0
            && codeFree > dataFree
            && clearCode > codeFree
            && clearData > clearCode
            && failState > clearData
            && failureLines > failState
            && HostSourceText.Contains(cleanup, "return false;")
            && !cleanup.Contains("return true;", StringComparison.Ordinal)
            && HostSourceText.Contains(directStartWriter, "s_m02CheckedPortIoBinding.Binding")
            && HostSourceText.Contains(directStartWriter, "Com1OwnedPortIoBackend backend")
            && HostSourceText.Contains(directStartWriter, "poll < 128")
            && HostSourceText.Contains(directStartWriter, ".LineStatusRegisterPort")
            && HostSourceText.Contains(directStartWriter, ".DataRegisterPort")
            && HostSourceText.Contains(directStartWriter, "backend.ReadByte(")
            && HostSourceText.Contains(directStartWriter, "backend.WriteByte(")
            && !directStartWriter.Contains("Write(console", StringComparison.Ordinal)
            && ExtractStartMarker(Ud2Method(catalog, "TryGetM02Ud2VectorStateStartLineByte", access: Microsoft.CodeAnalysis.Accessibility.Internal).ToString()) == GuestStartMarker
            && ExtractPackedFailureMarker(Ud2Method(catalog, "TryGetM02Ud2VectorStateFailureLineByte").ToString(), "M02Ud2VectorStateFailureHeadingLine", GuestFailureHeading.Length) == GuestFailureHeading
            && ExtractPackedFailureMarker(Ud2Method(catalog, "TryGetM02Ud2VectorStateFailureLineByte").ToString(), "M02Ud2VectorStateFailureDetailLine", GuestFailureDetail.Length) == GuestFailureDetail
            && ExtractPackedTransitionTranscript(Ud2Method(catalog, "EmitSuccessTranscript", typeof(DisabledDefaultUd2VectorStateTransition).FullName!).ToString()).EndsWith(GuestPassMarker + "\r\n", StringComparison.Ordinal)
            && ExtractPackedTransitionTranscript(Ud2Method(catalog, "EmitFailureTranscript", typeof(DisabledDefaultUd2VectorStateTransition).FullName!).ToString()) == GuestFailureHeading + "\r\n" + GuestFailureDetail + "\r\n";
    }

    [HostTest("M02-013 terminal entry and boot-path ordering are exact", HostLane.Fast, 6)]
    private static bool TestTerminalEntryAndBootOrdering()
    {
        var catalog = Ud2Sources();
        var entrySyntax = Ud2Method(catalog, "EnterM02Ud2VectorStateProbe");
        string entry = entrySyntax.ToString();
        string continuation = Ud2AllBranchCode(Ud2Caller(catalog));
        string efiMain = Ud2Method(catalog, "EfiMain").ToString();
        int validate = HostSourceText.IndexOf(entry, "ValidateM02Ud2VectorStateResources()");
        int start = HostSourceText.IndexOf(entry, "TryWriteM02Ud2VectorStateStartLine()");
        int functionPointer = HostSourceText.IndexOf(entry, "delegate* unmanaged<");
        int transitionOffset = HostSourceText.IndexOf(entry, ".TransitionEntryCompositeOffset");
        int contextAddress = HostSourceText.IndexOf(entry, "s_m02Ud2VectorStateDataAddress +\n" + "                        M02Ud2VectorStatePageSize");
        int publicationEvaluation = HostSourceText.IndexOf(entry, ".EvaluatePublication(");
        int serialize = HostSourceText.IndexOf(entry, "X64InstructionPublication.Serialize()");
        int publicationGuard = HostSourceText.IndexOf(entry, "if (!publicationEvaluation.Succeeded)");
        int enteredState = HostSourceText.IndexOf(entry, "s_m02Ud2VectorStateState = M02Ud2VectorStateEnteredState;");
        int invoke = HostSourceText.IndexOf(entry, "terminalEntry(context);");
        int spin = LastUd2Token(entry, "SpinM02Ud2VectorStateProbe();");
        string postInvocation = Ud2PostInvocation(entrySyntax);
        int bootPlan = HostSourceText.IndexOf(continuation, "EfiEntry.WriteBootSequencePlan(");
        int prepare = HostSourceText.IndexOf(continuation, "EfiEntry.PrepareM02Ud2VectorStateProbe(");
        int m02013 = HostSourceText.IndexOf(continuation, "EfiEntry.EnterM02Ud2VectorStateProbe(");
        int m02009 = HostSourceText.IndexOf(continuation, "EfiEntry.EnterM02OwnedGdtTssTerminalProbe(");
        int resolveConsole = HostSourceText.IndexOf(efiMain, "GetFirmwareConsole(systemTable)");
        int resolveSerial = HostSourceText.IndexOf(efiMain, "GetFirmwareSerial(systemTable)");
        int bootMarkers = HostSourceText.IndexOf(efiMain, "WriteBootMarkers(");
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
            && HostSourceText.Contains(entry, "X64InstructionPublication.Serialize() ==\n" + "                        X64InstructionPublication." + "ExpectedSuccessCookie")
            && HostSourceText.Contains(entry, "s_m02Ud2VectorStateCodeAddress +\n" + "                        (ulong)OS.Kernel.Diagnostics\n" + "                            .DisabledDefaultUd2VectorStateTransition\n" + "                            .TransitionEntryCompositeOffset")
            && !postInvocation.Contains("return", StringComparison.Ordinal)
            && !postInvocation.Contains("bootServices->", StringComparison.Ordinal)
            && !postInvocation.Contains("Write(", StringComparison.Ordinal)
            && !postInvocation.Contains("WriteCrLf(", StringComparison.Ordinal)
            && DisabledDefaultUd2VectorStateTransition.TransitionEntryCompositeOffset == 0x1800
            && DisabledDefaultUd2VectorStateTransition.ContextDataOffset == 0x1000
            && !DisabledDefaultUd2VectorStateTransition.HasReturns
            && !DisabledDefaultUd2VectorStateTransition.UsesFirmwareCallbacks
            && !DisabledDefaultUd2VectorStateTransition.ContinuesExecution
            && DisabledDefaultUd2VectorStateTransition.TerminalHalt
            && bootPlan >= 0
            && prepare > bootPlan
            && m02013 > prepare
            && m02009 > m02013
            && CountOccurrences(continuation, "EfiEntry.PrepareM02Ud2VectorStateProbe(") == 1
            && CountOccurrences(continuation, "EfiEntry.EnterM02Ud2VectorStateProbe(") == 1
            && resolveConsole >= 0
            && resolveSerial > resolveConsole
            && bootMarkers > resolveSerial
            && CountOccurrences(efiMain, "PrepareM02Ud2VectorStateProbe(") == 0
            && CountOccurrences(efiMain, "FailM02Ud2VectorStateBeforeEntry(") == 0
            && CountOccurrences(efiMain, "AllocatePages(") == 0
            && !efiMain.Contains("s_m02Ud2VectorState", StringComparison.Ordinal);
    }

    private static bool HasExactCompositeAddresses(string source)
    {
        return HostSourceText.Contains(source, "ulong commonTargetAddress =\n" + "            codeAddress + M02Ud2VectorStatePageSize;")
            && HostSourceText.Contains(source, "ulong idtAddress = dataAddress;")
            && HostSourceText.Contains(source, "ulong contextAddress =\n" + "            dataAddress + M02Ud2VectorStatePageSize;")
            && HostSourceText.Contains(source, "ulong ist1StackTop =\n" + "            dataAddress + (3 * M02Ud2VectorStatePageSize);")
            && HostSourceText.Contains(source, "ulong ist2StackTop =\n" + "            dataAddress + (4 * M02Ud2VectorStatePageSize);")
            && HostSourceText.Contains(source, "ulong ist3StackTop =\n" + "            dataAddress + (5 * M02Ud2VectorStatePageSize);")
            && HostSourceText.Contains(source, "byte* contextBytes = data + M02Ud2VectorStatePageSize;")
            && HostSourceText.Contains(source, "byte* stream = code + M02Ud2VectorStatePageSize;");
    }

    private static string ExtractStartMarker(string method)
    {
        MatchCollection characters = Regex.Matches(method, @"\(byte\)'(?<value>.)'", RegexOptions.CultureInvariant);
        return string.Concat(characters.Cast<Match>().Select(match => match.Groups["value"].Value));
    }

    private static string ExtractPackedFailureMarker(string method, string lineKind, int byteCount)
    {
        Match switchMatch = Regex.Match(method, @"if \(lineKind == " + Regex.Escape(lineKind) + @"\)\s*\{\s*word = wordIndex switch\s*\{" + @"(?<cases>.*?)\};\s*\}", RegexOptions.CultureInvariant | RegexOptions.Singleline);
        if (!switchMatch.Success)
        {
            if (lineKind != "M02Ud2VectorStateFailureDetailLine")
            {
                return string.Empty;
            }

            switchMatch = Regex.Match(method, @"else\s*\{\s*word = wordIndex switch\s*\{" + @"(?<cases>.*?)\};\s*\}", RegexOptions.CultureInvariant | RegexOptions.Singleline);
        }

        MatchCollection words = Regex.Matches(switchMatch.Groups["cases"].Value, @"(?<index>\d+)\s*=>\s*0x(?<hex>[0-9a-fA-F_]+)UL", RegexOptions.CultureInvariant);
        byte[] bytes = new byte[words.Count * sizeof(ulong)];
        foreach (Match wordMatch in words)
        {
            int wordIndex = int.Parse(wordMatch.Groups["index"].Value, CultureInfo.InvariantCulture);
            ulong word = ulong.Parse(wordMatch.Groups["hex"].Value.Replace("_", string.Empty, StringComparison.Ordinal), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (wordIndex < 0 || wordIndex >= words.Count)
            {
                return string.Empty;
            }

            for (int byteIndex = 0; byteIndex < sizeof(ulong); byteIndex++)
            {
                bytes[(wordIndex * sizeof(ulong)) + byteIndex] = (byte)(word >> (byteIndex * 8));
            }
        }

        return byteCount <= bytes.Length ? Encoding.ASCII.GetString(bytes, 0, byteCount) : string.Empty;
    }

    private static string ExtractPackedTransitionTranscript(string method)
    {
        MatchCollection emissions = Regex.Matches(method, @"builder\.Emit(?<width>U64|U32|U16)?\(0x" + @"(?<hex>[0-9a-fA-F_]+)(?:UL|U)?\);", RegexOptions.CultureInvariant);
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
            ulong value = ulong.Parse(emission.Groups["hex"].Value.Replace("_", string.Empty, StringComparison.Ordinal), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            for (int byteIndex = 0; byteIndex < width; byteIndex++)
            {
                bytes.Add((byte)(value >> (byteIndex * 8)));
            }
        }

        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    private static string[] ArtifactOutputPaths(string artifactPath)
    {
        return [artifactPath, artifactPath + ".link.map", artifactPath + ".link.rsp", artifactPath + ".link.json", artifactPath + ".serializer.obj"];
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

    private static ScriptResult RunEfiBuild(params string[] arguments)
    {
        var result = HostToolProcess.RunTool(["efi", "build", "--boot-policy", "Diagnostic", .. arguments]);
        return new ScriptResult(result.ExitCode, result.Output + result.Error);
    }

    private static bool ValidateLinkEvidence(string root, string artifactPath, bool enabled, SourceAuthoritySnapshot expected)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(artifactPath + ".link.json"));
        JsonElement evidence = document.RootElement;
        JsonElement evaluation = evidence.GetProperty("postRestoreEvaluation");
        string[] expectedCompileItems = OS.Tooling.EfiProjectAdmission.ExpectedCompileItems(OS.Tooling.EfiProfile.Create("Diagnostic", new Dictionary<string, bool> { ["EnableM02Ud2VectorStateProbe"] = enabled }));
        JsonElement properties = evaluation.GetProperty("properties");
        JsonElement[] inputElements = evidence.GetProperty("sourceClosureInputs").EnumerateArray().ToArray();
        string[] expectedPaths = ExpectedSourceAuthorityPaths(root);
        string[] actualPaths = inputElements.Select(input => input.GetProperty("path").GetString() ?? string.Empty).ToArray();
        bool inputHashesExact = inputElements.All(input =>
        {
            string relativePath = input.GetProperty("path").GetString() ?? string.Empty;
            string evidenceSha = input.GetProperty("sha256").GetString() ?? string.Empty;
            return relativePath.Length != 0 && evidenceSha == Sha256File(Path.Combine(root, relativePath.Replace('/', '\\')));
        });
        FileInfo artifact = new(artifactPath);
        return evidence.GetProperty("schemaVersion").GetInt32() == 3 && evidence.GetProperty("configuration").GetString() == "Release" && evidence.GetProperty("runtimeIdentifier").GetString() == "win-x64" && evidence.GetProperty("artifactBytes").GetInt64() == artifact.Length && evidence.GetProperty("artifactSha256").GetString() == Sha256File(artifactPath) && evidence.GetProperty("linkerMapSha256").GetString() == Sha256File(artifactPath + ".link.map") && evidence.GetProperty("linkResponseSha256").GetString() == Sha256File(artifactPath + ".link.rsp") && evidence.GetProperty("serializerObjectSha256").GetString() == Sha256File(artifactPath + ".serializer.obj") && evidence.GetProperty("sourceClosureSha256").GetString() == expected.SourceClosureSha256 && evidence.GetProperty("sourceProjectSha256").GetString() == expected.ProjectSha256 && evidence.GetProperty("entrySourceSha256").GetString() == expected.EntrySha256 && evaluation.GetProperty("compileCount").GetInt32() == expectedCompileItems.Length && evaluation.GetProperty("compileSha256").GetString() == OS.Tooling.EfiEvidence.HashLines(expectedCompileItems) && evaluation.GetProperty("projectReferenceCount").GetInt32() == 0 && evaluation.GetProperty("referenceCount").GetInt32() == 0 && evaluation.GetProperty("analyzerCount").GetInt32() == 2 && properties.GetProperty("TargetFramework").GetString() == "net11.0" && properties.GetProperty("RuntimeIdentifier").GetString() == "win-x64" && properties.GetProperty("PublishAot").GetString() == "true" && properties.GetProperty("SelfContained").GetString() == "true" && properties.GetProperty("PublishTrimmed").GetString() == "true" && properties.GetProperty("DisableFrameworkLibGeneration").GetString() == "true" && properties.GetProperty("EnableM02OwnedCom1ReadyProbe").GetString() == (enabled ? "true" : "false") && properties.GetProperty("EnableM02PanicSafeOwnedCom1Probe").GetString() == (enabled ? "true" : "false") && properties.GetProperty("EnableM02Ud2VectorStateProbe").GetString() == (enabled ? "true" : "false") && properties.GetProperty("EnableM02OwnedX64GdtEncodingProbe").GetString() == "false" && properties.GetProperty("EnableM02GuardedIstStackAllocationProbe").GetString() == "false" && properties.GetProperty("EnableM02OwnedGdtTssTerminalProbe").GetString() == "false" && properties.GetProperty("EnableM02NormalizedInterruptFramePageProbe").GetString() == "false" && properties.GetProperty("EnableM02OwnedIdtGatePopulationProbe").GetString() == "false" && properties.GetProperty("EnableM02IdtrContinuedExecutionProbe").GetString() == "false" && properties.GetProperty("EnableM04IndependentEndianConformanceProbe").GetString() == "false" && properties.GetProperty("EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe").GetString() == "false" && inputElements.Length == expected.SourceInputCount && actualPaths.SequenceEqual(expectedPaths, StringComparer.Ordinal) && inputHashesExact;
    }

    private static SourceAuthoritySnapshot CaptureSourceAuthoritySnapshot(string root)
    {
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        string entryPath = Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs");
        XDocument project = XDocument.Load(projectPath);
        string[] compileKeys = project.Descendants("Compile").Select(item => (item.Attribute("Include")?.Value ?? string.Empty) + "|" + (item.Attribute("Link")?.Value ?? string.Empty)).ToArray();
        string compileClosure = Sha256Text(string.Join("\n", compileKeys) + "\n");
        string[] paths = ExpectedSourceAuthorityPaths(root);
        StringBuilder sourceClosure = new();
        foreach (string relativePath in paths)
        {
            sourceClosure.Append(relativePath);
            sourceClosure.Append('|');
            sourceClosure.Append(Sha256File(Path.Combine(root, relativePath.Replace('/', '\\'))));
            sourceClosure.Append('\n');
        }

        return new SourceAuthoritySnapshot(Sha256File(projectPath), Sha256File(entryPath), Sha256Text(sourceClosure.ToString()), compileClosure, paths.Length, compileKeys.Length);
    }

    private static string[] ExpectedSourceAuthorityPaths(string root) => EfiEvidence.SourceClosurePaths(root).ToArray();
    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Sha256Text(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string TailForAudit(string value)
    {
        const int MaximumCharacters = 2_000;
        string tail = value.Length <= MaximumCharacters ? value : value[^MaximumCharacters..];
        return tail.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " | ", StringComparison.Ordinal);
    }
    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
