using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

internal static class M01LoadedImageStackEfiEntryTests
{
    private const string MemoryMapSymbol = "M03_MEMORY_MAP_CAPTURE_PROBE";
    private const string TypeManagerSymbol = "M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE";
    private const string EntropySymbol = "M03_FIRMWARE_ENTROPY_SEED_PROBE";
    private const string IssueTestName = "m01.021";
    private const string ProbeSymbol = "M01_LOADED_IMAGE_STACK_PROBE";
    private const string DoubleFaultSymbol = "M02_DOUBLE_FAULT_IST_PROBE";
    private const string UnexpectedPanicSymbol = "M02_UNEXPECTED_VECTOR_PANIC_PROBE";
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run(
            "m01.021 default-off EFI source is the audited baseline",
            TestDefaultOffProfile,
            ref failures);
        Run(
            "m01.021 loaded-image capture uses the exact EFI boundary",
            TestLoadedImageCaptureBoundary,
            ref failures);
        Run(
            "m01.021 page allocations and cleanup remain deterministic",
            TestAllocationAndCleanupContract,
            ref failures);
        Run(
            "m01.021 stack-probe page and runtime bridge are exact",
            TestStackProbePageAndRuntimeContract,
            ref failures);
        Run(
            "m01.021 refreshed map precedes the canonical reservation ledger",
            TestMapAndReservationLedgerContract,
            ref failures);
        Run(
            "m01.021 direct unmanaged continuation uses static transitions",
            TestContinuationAndCoordinatorContract,
            ref failures);
        Run(
            "m01.021 success and failure marker topology is complete",
            TestMarkerTopology,
            ref failures);
        Run(
            "m01.021 live-boundary and source firewalls hold",
            TestSourceFirewalls,
            ref failures);
        Run(
            "m01.021 probe-scoped mutation selector skips prefix collisions",
            TestProbeScopedMutationSelector,
            ref failures);
        Run(
            "m01.021 probe-scoped method selector skips same-name collisions",
            TestProbeScopedMethodMutationSelector,
            ref failures);
        Run(
            "m01.021 contract validator rejects representative bypasses",
            TestRepresentativeMutations,
            ref failures);
        Run(
            "m01.021 issue marker is unique",
            TestIssueMarkerUniqueness,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-021 EFI entry source-contract tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_021_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(
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
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "TEST_FAIL "
                + name
                + " "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static bool TestDefaultOffProfile()
    {
        foreach (string disabledSymbol in new[] { DoubleFaultSymbol, UnexpectedPanicSymbol, MemoryMapSymbol, EntropySymbol, TypeManagerSymbol })
        {
            string selectorFixture = "#if " + disabledSymbol + "\nDfOnly();\n#else\nDfOff();\n#endif\n"
                + "#if UNRELATED_PROBE\nUnknown();\n#else\nUnknownElse();\n#endif\n";
            foreach (bool on in new[] { false, true })
            {
                if (!TrySelectProbeProfile(selectorFixture, on, out string selected, out _)
                    || selected.Contains("DfOnly();", StringComparison.Ordinal)
                    || !selected.Contains("DfOff();", StringComparison.Ordinal)
                    || !selected.Contains("#if UNRELATED_PROBE", StringComparison.Ordinal)
                    || !selected.Contains("UnknownElse();", StringComparison.Ordinal)) return Failure("known diagnostic default-off finite selector");
            }
            if (!TrySelectProbeProfile(selectorFixture.Replace(disabledSymbol, disabledSymbol.ToLowerInvariant(), StringComparison.Ordinal), false, out string alias, out _)
                || !alias.Contains("#if " + disabledSymbol.ToLowerInvariant(), StringComparison.Ordinal)) return Failure("known diagnostic selector ordinal");
        }

        string source = LoadEfiEntrySource();
        bool defaultSelected = TrySelectProbeProfile(
            source,
            enabled: false,
            out string defaultSource,
            out string defaultFailure);
        bool enabledSelected = TrySelectProbeProfile(
            source,
            enabled: true,
            out string enabledSource,
            out string enabledFailure);
        if (!defaultSelected || !enabledSelected)
        {
            return Failure(
                "profile-selection="
                + defaultFailure
                + enabledFailure);
        }

        int exactProbeBlocks = Regex.Matches(
            source,
            @"(?m)^[ \t]*#if[ \t]+"
                + ProbeSymbol
                + @"[ \t]*$",
            RegexOptions.CultureInvariant).Count;
        if (exactProbeBlocks != 1
            || Regex.IsMatch(
                source,
                @"(?m)^[ \t]*#(?:if|elif)[^\r\n]*\b"
                    + ProbeSymbol
                    + @"\b[^\r\n]*$",
                RegexOptions.CultureInvariant)
                && exactProbeBlocks != Regex.Matches(
                    source,
                    @"(?m)^[ \t]*#(?:if|elif)[^\r\n]*\b"
                        + ProbeSymbol
                        + @"\b[^\r\n]*$",
                    RegexOptions.CultureInvariant).Count)
        {
            return Failure("probe-guard-is-not-exact");
        }

        string? defaultAdapterRun = FindStageOneAdapterRunBody(
            defaultSource);
        if (defaultAdapterRun is null
            || !string.Equals(
                RemoveAllWhitespace(defaultAdapterRun),
                "returnfalse;",
                StringComparison.Ordinal)
            || CountFlexibleQualifiedCalls(
                defaultSource,
                "M01StageOneAdapter.Run") != 1
            || !Regex.IsMatch(
                defaultSource,
                @"if\s*\(\s*M01StageOneAdapter\s*\.\s*Run\s*\("
                    + @"[\s\S]*?\)\s*\)\s*\{\s*return\s*;\s*\}",
                RegexOptions.CultureInvariant))
        {
            return Failure("default-adapter-is-not-a-false-fallthrough");
        }

        string[] stageOnlyTokens =
        [
            "M01LoadedImageStackIntegration",
            "M01LoadedImageStackStaticState",
            "EfiLoadedImageProtocol",
            "UnmanagedCallersOnly",
            "GetM01StageOneSuccessLine"
        ];
        foreach (string token in stageOnlyTokens)
        {
            if (defaultSource.Contains(token, StringComparison.Ordinal))
            {
                return Failure("default-profile-token=" + token);
            }
        }

        return !string.Equals(
                RemoveAllWhitespace(defaultSource),
                RemoveAllWhitespace(enabledSource),
                StringComparison.Ordinal)
            || Failure("probe-does-not-change-enabled-profile");
    }

    private static bool TestLoadedImageCaptureBoundary()
    {
        string source = LoadEfiEntrySource();
        string enabled = RemoveComments(
            SelectProbeProfileOrThrow(source, enabled: true));
        string stage = LoadProbeOnlySource();
        string compact = RemoveComments(stage);
        string noWhitespace = RemoveAllWhitespace(compact);

        if (!Regex.IsMatch(
                enabled,
                @"delegate\s*\*\s*unmanaged\s*<\s*"
                    + @"void\s*\*\s*,\s*EfiGuid\s*\*\s*,\s*"
                    + @"void\s*\*\s*\*\s*,\s*ulong\s*>\s*"
                    + @"HandleProtocol",
                RegexOptions.CultureInvariant)
            || !noWhitespace.Contains(
                "EfiLoadedImageProtocolObservation"
                    + ".CaptureFromUefiBoundary",
                StringComparison.Ordinal)
            || !noWhitespace.Contains(
                "EfiLoadedImageRangeCapturePlan.Create",
                StringComparison.Ordinal)
            || !compact.Contains(
                "M01LoadedImageVirtualMappingEvidence",
                StringComparison.Ordinal)
            || !compact.Contains(
                "CaptureCallerAttestedIdentity",
                StringComparison.Ordinal))
        {
            return Failure("loaded-image-capture-surface");
        }

        string? captureBody = FindMethodBody(
            compact,
            "TryCaptureLoadedImage");
        if (captureBody is null)
        {
            return Failure("loaded-image-capture-method-not-found");
        }

        string captureCompact =
            RemoveAllWhitespace(captureBody);
        string[] exactGuidAssignments =
        [
            "loadedImageGuid.Data1=0x5b1b31a1;",
            "loadedImageGuid.Data2=0x9562;",
            "loadedImageGuid.Data3=0x11d2;",
            "loadedImageGuid.Data4_0=0x8e;",
            "loadedImageGuid.Data4_1=0x3f;",
            "loadedImageGuid.Data4_2=0x00;",
            "loadedImageGuid.Data4_3=0xa0;",
            "loadedImageGuid.Data4_4=0xc9;",
            "loadedImageGuid.Data4_5=0x69;",
            "loadedImageGuid.Data4_6=0x72;",
            "loadedImageGuid.Data4_7=0x3b;"
        ];
        foreach (string assignment in exactGuidAssignments)
        {
            if (!captureCompact.Contains(
                    assignment,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    "loaded-image-guid-assignment=" + assignment);
            }
        }

        List<string[]> handleProtocolCalls =
            FindInvocationArguments(
                captureBody,
                "HandleProtocol");
        if (!captureCompact.Contains(
                "sizeof(EfiLoadedImageProtocol)!=96",
                StringComparison.Ordinal)
            || !captureCompact.Contains(
                "firmwareStatus=NotAttemptedStatus;",
                StringComparison.Ordinal)
            || handleProtocolCalls.Count != 1
            || handleProtocolCalls[0].Length != 3
            || !string.Equals(
                RemoveAllWhitespace(handleProtocolCalls[0][0]),
                "imageHandle",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(handleProtocolCalls[0][1]),
                "&loadedImageGuid",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(handleProtocolCalls[0][2]),
                "&loadedImageInterface",
                StringComparison.Ordinal)
            || !captureCompact.Contains(
                "firmwareStatus=bootServices->HandleProtocol(",
                StringComparison.Ordinal)
            || !Regex.IsMatch(
                compact,
                @"TryCaptureLoadedImage\s*\([^;{}]*"
                    + @"out\s+ulong\s+firmwareStatus\s*\)\s*\{",
                RegexOptions.CultureInvariant)
            || !noWhitespace.Contains(
                "outulongloadedImageFirmwareStatus",
                StringComparison.Ordinal)
            || !noWhitespace.Contains(
                "captureStatus==M01StageOneAdapterStatus"
                    + ".LoadedImageProtocolFailed"
                    + "?loadedImageFirmwareStatus:",
                StringComparison.Ordinal))
        {
            return Failure(
                "loaded-image-firmware-status-plumbing");
        }

        if (!Regex.IsMatch(
                enabled,
                @"delegate\s*\*\s*unmanaged\s*<\s*"
                    + @"(?:uint|EfiAllocateType)\s*,\s*"
                    + @"(?:uint|UefiMemoryType)\s*,\s*"
                    + @"ulong\s*,\s*ulong\s*\*\s*,\s*ulong\s*>\s*"
                    + @"AllocatePages",
                RegexOptions.CultureInvariant)
            || !Regex.IsMatch(
                enabled,
                @"delegate\s*\*\s*unmanaged\s*<\s*"
                    + @"ulong\s*,\s*ulong\s*,\s*ulong\s*>\s*"
                    + @"FreePages",
                RegexOptions.CultureInvariant))
        {
            return Failure("firmware-page-function-pointer-shape");
        }

        if (Regex.IsMatch(
                compact,
                @"(?:->|\.)\s*Unload\s*\(",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                compact,
                @"FreePages\s*\([^;\r\n]*(?:ImageBase|loadedImage)",
                RegexOptions.CultureInvariant
                    | RegexOptions.IgnoreCase))
        {
            return Failure("loader-owned-image-released");
        }

        if (!HasRhpOriginalImageAndAuthority(source)
            || HasRhpOriginalImageAndAuthority(source.Replace("!mapping.IsValidFor(capture)", "false", StringComparison.Ordinal))
            || HasRhpOriginalImageAndAuthority(source.Replace("s_evidenceScratch.LoadedImageCapture;", "default;", StringComparison.Ordinal))
            || HasRhpOriginalImageAndAuthority(source.Replace("Allocator = &state->AllocatorStateValue", "Allocator = null", StringComparison.Ordinal))
            || HasRhpOriginalImageAndAuthority(source.Replace("M01StageTwoAdapter.RunRhpNewFastAfterZeroCheckpoint();", "SkippedRhpHandoff();", StringComparison.Ordinal)))
            return Failure("rhp-original-image-and-authority");

        return true;
    }

    private static bool HasRhpOriginalImageAndAuthority(string source)
    {
        string? image = FindMethodBody(source, "TryGetOriginalLoadedImage");
        string? handoff = FindMethodBody(source, "RunRhpNewFastAfterZeroCheckpoint");
        if (image is null || handoff is null) return false;
        string imageCode = RemoveAllWhitespace(RemoveComments(image));
        foreach (string required in new[] { "!IsFinalStageOneSuccess(state,", "s_evidenceScratch.LoadedImageCapture;",
            "s_evidenceScratch.LoadedImageVirtualMapping;", "!capture.Succeeded", "!mapping.IsValidFor(capture)",
            "mapping.Provenance!=M01LoadedImageVirtualMappingProvenance.CallerAttestedIdentityMapping",
            "address!=physical", "mapping.ImageByteLength!=capture.Range.ImageSize", "address>~0UL-capture.Range.ImageSize" })
            if (!imageCode.Contains(required, StringComparison.Ordinal)) return false;
        if (FindInvocationArguments(image, "HandleProtocol").Count != 0) return false;
        string authority = RemoveAllWhitespace(RemoveComments(handoff));
        foreach (string required in new[] { "M01StageOneAdapter.TryGetOriginalLoadedImage(",
            "Allocator=&state->AllocatorStateValue,AllocatorCapacity=1", "Nodes=&state->FreeNodeValue,NodeCount=1,NodeCapacity=1",
            "Reservations=&ledger->Range0,ReservationCount=5,ReservationCapacity=5", "ParentIndex=heap->ParentIndex",
            "Backing=(byte*)record->VirtualStart", "BackingCapacity=8192", "Heap=heap", "OS.Runtime.RhpNewFastAllocation.Run(original,image,imageBytes);" })
            if (!authority.Contains(required, StringComparison.Ordinal)) return false;
        int zero = source.IndexOf("if (!M01StageTwoAdapter.ValidateManagedHeapBeforeBootPlan", StringComparison.Ordinal);
        int run = source.IndexOf("M01StageTwoAdapter.RunRhpNewFastAfterZeroCheckpoint();", StringComparison.Ordinal);
        return zero >= 0 && run > zero && source.IndexOf("EfiEntry.WriteBootSequencePlan(", zero, StringComparison.Ordinal) > run;
    }

    private static bool TestAllocationAndCleanupContract()
    {
        string stage = RemoveComments(LoadProbeOnlySource());
        string noWhitespace = RemoveAllWhitespace(stage);
        if (!ValidateAllocationSafetyHelperContract(
                stage,
                out string allocationHelperFailure))
        {
            return Failure(
                "allocation-helper=" + allocationHelperFailure);
        }

        if (!ValidateMapContainsAllocationContract(
                stage,
                out string mapAllocationFailure))
        {
            return Failure(
                "map-allocation-helper=" + mapAllocationFailure);
        }

        string[] allocationConstants =
        [
            "privateconstuintAllocateAnyPages=0;",
            "privateconstuintEfiLoaderCode=1;",
            "privateconstuintEfiLoaderData=2;",
            "privateconstulongCodePageCount=1;",
            "privateconstulongGuardPageCount=1;",
            "privateconstulongDataArenaPageCount="
                + "GuardPageCount+UsableStackPageCount;"
        ];
        foreach (string constant in allocationConstants)
        {
            if (!noWhitespace.Contains(
                    constant,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "allocation-constant=" + constant);
            }
        }

        Match usableStackPages = Regex.Match(
            noWhitespace,
            @"privateconstulongUsableStackPageCount=(?<pages>\d+);",
            RegexOptions.CultureInvariant);
        if (!usableStackPages.Success
            || !ulong.TryParse(
                usableStackPages.Groups["pages"].Value,
                out ulong usablePageCount)
            || usablePageCount != 33)
        {
            return Failure("usable-stack-page-count");
        }

        List<string[]> allocationCalls =
            FindInvocationArguments(stage, "AllocatePages");
        if (allocationCalls.Count != 2)
        {
            return Failure(
                "allocate-pages-call-count=" + allocationCalls.Count);
        }

        string[] codeArguments = allocationCalls[0];
        string[] dataArguments = allocationCalls[1];
        if (codeArguments.Length != 4
            || dataArguments.Length != 4
            || !string.Equals(
                RemoveAllWhitespace(codeArguments[0]),
                "AllocateAnyPages",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(codeArguments[1]),
                "EfiLoaderCode",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(codeArguments[2]),
                "CodePageCount",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(codeArguments[3]),
                "&codeAddress",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(dataArguments[0]),
                "AllocateAnyPages",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(dataArguments[1]),
                "EfiLoaderData",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(dataArguments[2]),
                "DataArenaPageCount",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(dataArguments[3]),
                "&dataAddress",
                StringComparison.Ordinal))
        {
            return Failure("allocate-pages-exact-arguments");
        }

        if (!Regex.IsMatch(
                stage,
                @"private\s+static\s+M01StageOneMemoryMapBuffer\b",
                RegexOptions.CultureInvariant)
            || !stage.Contains(
                "fixed byte Bytes[OwnedMemoryMapBufferSize]",
                StringComparison.Ordinal)
            || !Regex.IsMatch(
                stage,
                @"private\s+static\s+"
                    + @"M01LoadedImageStackStaticState\b",
                RegexOptions.CultureInvariant)
            || !stage.Contains(
                "BootMemoryRegionRecord",
                StringComparison.Ordinal)
            || !stage.Contains(
                "BootStackGuard",
                StringComparison.Ordinal)
            || !stage.Contains(
                "BootStackSwitchStub",
                StringComparison.Ordinal))
        {
            return Failure("static-allocation-state");
        }

        string? runBody = FindMethodBody(stage, "Run");
        if (runBody is null)
        {
            return Failure("adapter-run-not-found");
        }

        string runCompact = RemoveAllWhitespace(runBody);
        if (!runCompact.Contains(
                "boolcodeAllocated="
                    + "codeFirmwareStatus==EfiSuccess;",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "booldataAllocated="
                    + "dataFirmwareStatus==EfiSuccess;",
                StringComparison.Ordinal)
            || runCompact.Contains(
                "codeFirmwareStatus==EfiSuccess&&",
                StringComparison.Ordinal)
            || runCompact.Contains(
                "dataFirmwareStatus==EfiSuccess&&",
                StringComparison.Ordinal))
        {
            return Failure("efi-success-allocation-ownership");
        }

        List<string[]> freeCalls =
            FindInvocationArguments(stage, "FreePages");
        if (freeCalls.Count != 2)
        {
            return Failure("free-pages-stage-call-count");
        }

        string? cleanupBody = FindMethodBody(
            stage,
            "FailBeforeTransfer");
        if (cleanupBody is null)
        {
            return Failure("cleanup-method-not-found");
        }

        List<string[]> cleanupFreeCalls =
            FindInvocationArguments(cleanupBody, "FreePages");
        if (cleanupFreeCalls.Count != 2
            || cleanupFreeCalls[0].Length != 2
            || cleanupFreeCalls[1].Length != 2
            || !cleanupFreeCalls[0][0].Contains(
                "dataAddress",
                StringComparison.Ordinal)
            || !cleanupFreeCalls[0][1].Contains(
                "DataArenaPageCount",
                StringComparison.Ordinal)
            || !cleanupFreeCalls[1][0].Contains(
                "codeAddress",
                StringComparison.Ordinal)
            || !cleanupFreeCalls[1][1].Contains(
                "CodePageCount",
                StringComparison.Ordinal))
        {
            return Failure("cleanup-is-not-data-then-code");
        }

        string cleanupCompact = RemoveAllWhitespace(cleanupBody);
        int dataFree = cleanupCompact.IndexOf(
            "bootServices->FreePages(dataAddress,DataArenaPageCount)",
            StringComparison.Ordinal);
        int clearProbe = cleanupCompact.IndexOf(
            "TryClearStackProbeBytes(codeAddress)",
            StringComparison.Ordinal);
        int codeFree = cleanupCompact.IndexOf(
            "bootServices->FreePages(codeAddress,CodePageCount)",
            StringComparison.Ordinal);
        if (dataFree < 0
            || clearProbe <= dataFree
            || codeFree <= clearProbe
            || !cleanupCompact.Contains(
                "if(dataAllocated){",
                StringComparison.Ordinal)
            || !cleanupCompact.Contains(
                "if(codeAllocated){",
                StringComparison.Ordinal)
            || !cleanupCompact.Contains(
                "if(stackProbeCleared){",
                StringComparison.Ordinal))
        {
            return Failure("cleanup-data-clear-code-order");
        }

        string? clearBody = FindMethodBody(
            stage,
            "TryClearStackProbeBytes");
        if (clearBody is null)
        {
            return Failure("stack-probe-clear-method-not-found");
        }

        string clearCompact = RemoveAllWhitespace(clearBody);
        int clearTarget = clearCompact.IndexOf(
            "X64StackProbeRuntime.TryClear(stackProbeAddress)",
            StringComparison.Ordinal);
        int clearBytes = clearCompact.IndexOf(
            "probe[index]=0;",
            StringComparison.Ordinal);
        int verifyBytes = clearCompact.IndexOf(
            "probe[index]!=0",
            StringComparison.Ordinal);
        if (clearTarget < 0
            || clearBytes <= clearTarget
            || verifyBytes <= clearBytes)
        {
            return Failure("stack-probe-clear-readback-order");
        }

        return true;
    }

    private static bool TestStackProbePageAndRuntimeContract()
    {
        string stage = RemoveComments(LoadProbeOnlySource());
        if (!ValidateEfiStackProbeContract(
                stage,
                out string efiFailure))
        {
            return Failure("efi-stack-probe=" + efiFailure);
        }

        string runtime = RemoveComments(
            LoadMinimalRuntimeSource());
        return ValidateRuntimeStackProbeContract(
                runtime,
                out string runtimeFailure)
            || Failure("runtime-stack-probe=" + runtimeFailure);
    }

    private static bool ValidateEfiStackProbeContract(
        string stage,
        out string failure)
    {
        string compact = RemoveAllWhitespace(stage);
        string[] requiredConstants =
        [
            "privateconstulongStackProbeEntryOffset=64;",
            "privateconstulongStackProbeByteLength=27;"
        ];
        foreach (string required in requiredConstants)
        {
            if (!compact.Contains(required, StringComparison.Ordinal))
            {
                failure = "constant=" + required;
                return false;
            }
        }

        string? materializeBody = FindMethodBody(
            stage,
            "MaterializeAndInstallStackProbe");
        string? bytePlanBody = FindMethodBody(
            stage,
            "GetStackProbeByte");
        string? runBody = FindMethodBody(
            stage,
            "Run");
        if (materializeBody is null
            || bytePlanBody is null
            || runBody is null)
        {
            failure = "method-not-found";
            return false;
        }

        string runCompact = RemoveAllWhitespace(runBody);
        int installCall = runCompact.IndexOf(
            "MaterializeAndInstallStackProbe(",
            StringComparison.Ordinal);
        int prepareCall = runCompact.IndexOf(
            "PrepareAndTransfer(",
            StringComparison.Ordinal);
        if (installCall < 0
            || prepareCall <= installCall
            || Regex.Matches(
                runBody,
                @"\bMaterializeAndInstallStackProbe\s*\(",
                RegexOptions.CultureInvariant).Count != 1
            || Regex.Matches(
                runBody,
                @"\bPrepareAndTransfer\s*\(",
                RegexOptions.CultureInvariant).Count != 1)
        {
            failure = "install-before-large-prepare";
            return false;
        }

        byte[] expectedBytes =
        [
            0xf3, 0x0f, 0x1e, 0xfa,
            0x48, 0x89, 0xe0,
            0x48, 0x25, 0x00, 0xf0, 0xff, 0xff,
            0x48, 0x2d, 0x00, 0x10, 0x00, 0x00,
            0x85, 0x00,
            0x4c, 0x39, 0xd8,
            0x7f, 0xf3,
            0xc3
        ];
        MatchCollection mappings = Regex.Matches(
            bytePlanBody,
            @"(?m)\b(?<index>\d+)\s*=>\s*"
                + @"0x(?<value>[0-9a-fA-F]{2})\b",
            RegexOptions.CultureInvariant);
        if (mappings.Count != expectedBytes.Length)
        {
            failure = "byte-mapping-count=" + mappings.Count;
            return false;
        }

        bool[] observed = new bool[expectedBytes.Length];
        foreach (Match mapping in mappings)
        {
            if (!int.TryParse(
                    mapping.Groups["index"].Value,
                    out int index)
                || index < 0
                || index >= expectedBytes.Length
                || observed[index]
                || !byte.TryParse(
                    mapping.Groups["value"].Value,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out byte value)
                || value != expectedBytes[index])
            {
                failure = "byte-mapping="
                    + mapping.Value;
                return false;
            }

            observed[index] = true;
        }

        if (observed.Any(value => !value)
            || !Regex.IsMatch(
                bytePlanBody,
                @"_\s*=>\s*0\b",
                RegexOptions.CultureInvariant))
        {
            failure = "byte-plan-fallback-or-gap";
            return false;
        }

        StringBuilder expectedPlanBody =
            new("returnindexswitch{");
        for (int index = 0;
            index < expectedBytes.Length;
            index++)
        {
            expectedPlanBody
                .Append(index)
                .Append("=>0x")
                .Append(
                    expectedBytes[index].ToString(
                        "x2",
                        System.Globalization
                            .CultureInfo.InvariantCulture))
                .Append(',');
        }

        expectedPlanBody.Append("_=>0};");
        if (!string.Equals(
                RemoveAllWhitespace(bytePlanBody),
                expectedPlanBody.ToString(),
                StringComparison.Ordinal))
        {
            failure = "byte-plan-body";
            return false;
        }

        string materializeCompact =
            RemoveAllWhitespace(materializeBody);
        string[] requiredLayoutTokens =
        [
            "codeByteLength!=PageSize",
            "StackProbeEntryOffset<StubEntryOffset"
                + "+OwnedBootStackSwitchPlan.RequiredStubByteLength",
            "StackProbeEntryOffset>"
                + "codeByteLength-StackProbeByteLength",
            "((codeAddress+StackProbeEntryOffset)"
                + "&(OwnedBootStackSwitchPlan"
                + ".StubEntryAlignmentBytes-1))!=0"
        ];
        foreach (string token in requiredLayoutTokens)
        {
            if (!materializeCompact.Contains(
                    token,
                    StringComparison.Ordinal))
            {
                failure = "layout=" + token;
                return false;
            }
        }

        int fullPageFill = materializeCompact.IndexOf(
            "for(ulongindex=0;index<codeByteLength;index++)"
                + "{codePage[index]=0xcc;}",
            StringComparison.Ordinal);
        int fullPageReadback = materializeCompact.IndexOf(
            "for(ulongindex=0;index<codeByteLength;index++)"
                + "{if(codePage[index]!=0xcc)",
            StringComparison.Ordinal);
        int probePointer = materializeCompact.IndexOf(
            "byte*probe=codePage+StackProbeEntryOffset;",
            StringComparison.Ordinal);
        int probeFill = materializeCompact.IndexOf(
            "for(ulongindex=0;index<StackProbeByteLength;index++)"
                + "{probe[index]=GetStackProbeByte(index);}",
            StringComparison.Ordinal);
        int probeReadback = materializeCompact.IndexOf(
            "for(ulongindex=0;index<StackProbeByteLength;index++)"
                + "{if(probe[index]!=GetStackProbeByte(index))",
            StringComparison.Ordinal);
        int install = materializeCompact.IndexOf(
            "X64StackProbeRuntime.TryInstall((ulong)probe)",
            StringComparison.Ordinal);
        if (fullPageFill < 0
            || fullPageReadback <= fullPageFill
            || probePointer <= fullPageReadback
            || probeFill <= probePointer
            || probeReadback <= probeFill
            || install <= probeReadback
            || FindInvocationArguments(
                materializeBody,
                "TryInstall").Count != 1)
        {
            failure = "fill-readback-install-order";
            return false;
        }

        const string exactReadbackInstallSuffix =
            "for(ulongindex=0;"
            + "index<StackProbeByteLength;index++)"
            + "{if(probe[index]"
            + "!=GetStackProbeByte(index))"
            + "{returnM01StageOneAdapterStatus"
            + ".StackProbeReadbackRejected;}}"
            + "returnSystem.Runtime.X64StackProbeRuntime"
            + ".TryInstall((ulong)probe)"
            + "?M01StageOneAdapterStatus.Success"
            + ":M01StageOneAdapterStatus"
            + ".StackProbeInstallRejected;";
        if (!materializeCompact.EndsWith(
                exactReadbackInstallSuffix,
                StringComparison.Ordinal))
        {
            failure = "readback-to-install-window";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool ValidateRuntimeStackProbeContract(
        string runtime,
        out string failure)
    {
        if (Regex.Matches(
                runtime,
                @"\[\s*RuntimeExport\s*\(\s*"""
                    + @"RhpStackProbe""\s*\)\s*\]\s*"
                    + @"private\s+static\s+void\s+"
                    + @"RhpStackProbe\s*\(\s*\)\s*\{",
                RegexOptions.CultureInvariant).Count != 1)
        {
            failure = "runtime-export";
            return false;
        }

        string? installBody = FindMethodBody(
            runtime,
            "TryInstall");
        string? clearBody = FindMethodBody(
            runtime,
            "TryClear");
        string? bridgeBody = FindMethodBody(
            runtime,
            "RhpStackProbe");
        if (installBody is null
            || clearBody is null
            || bridgeBody is null)
        {
            failure = "runtime-method-not-found";
            return false;
        }

        string installCompact =
            RemoveAllWhitespace(installBody);
        string bridgeCompact =
            RemoveAllWhitespace(bridgeBody);
        const string expectedInstall =
            "if(probeEntry==0||(probeEntry&15)!=0"
            + "||s_probeEntry!=0){returnfalse;}"
            + "s_probeEntry=probeEntry;returntrue;";
        const string expectedBridge =
            "ulongprobeEntry=s_probeEntry;"
            + "if(probeEntry==0){while(true){}}"
            + "((delegate*managed<void>)(void*)probeEntry)();";
        if (!string.Equals(
                installCompact,
                expectedInstall,
                StringComparison.Ordinal))
        {
            failure = "aligned-one-shot-install";
            return false;
        }

        if (!string.Equals(
                bridgeCompact,
                expectedBridge,
                StringComparison.Ordinal)
            || bridgeBody.Contains(
                "delegate* unmanaged",
                StringComparison.Ordinal)
            || bridgeBody.Contains(
                "UnmanagedCallersOnly",
                StringComparison.Ordinal))
        {
            failure = "managed-tail-bridge-or-null-loop";
            return false;
        }

        string clearCompact =
            RemoveAllWhitespace(clearBody);
        if (!clearCompact.Contains(
                "expectedProbeEntry==0",
                StringComparison.Ordinal)
            || !clearCompact.Contains(
                "s_probeEntry!=expectedProbeEntry",
                StringComparison.Ordinal)
            || !clearCompact.EndsWith(
                "s_probeEntry=0;returntrue;",
                StringComparison.Ordinal))
        {
            failure = "expected-target-clear";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool ValidateAllocationSafetyHelperContract(
        string stage,
        out string failure)
    {
        string? allocationBody = FindMethodBody(
            stage,
            "TryValidateAllocation");
        string? overlapBody = FindMethodBody(
            stage,
            "RangesOverlap");
        if (allocationBody is null || overlapBody is null)
        {
            failure = "method-not-found";
            return false;
        }

        const string expectedAllocation =
            "byteLength=0;endExclusive=0;"
            + "if(address==0||(address&(PageSize-1))!=0"
            + "||pageCount==0||pageCount>~0UL/PageSize)"
            + "{returnfalse;}"
            + "byteLength=pageCount*PageSize;"
            + "if(address>~0UL-byteLength)"
            + "{byteLength=0;returnfalse;}"
            + "endExclusive=address+byteLength;"
            + "returnendExclusive>address"
            + "&&(endExclusive&(PageSize-1))==0;";
        const string expectedOverlap =
            "returnfirstStart<secondEndExclusive"
            + "&&secondStart<firstEndExclusive;";
        if (!string.Equals(
                RemoveAllWhitespace(allocationBody),
                expectedAllocation,
                StringComparison.Ordinal))
        {
            failure = "zero-alignment-count-or-overflow";
            return false;
        }

        if (!string.Equals(
                RemoveAllWhitespace(overlapBody),
                expectedOverlap,
                StringComparison.Ordinal))
        {
            failure = "half-open-overlap";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool ValidateMapContainsAllocationContract(
        string stage,
        out string failure)
    {
        string? body = FindMethodBody(
            stage,
            "MapContainsAllocation");
        if (body is null)
        {
            failure = "method-not-found";
            return false;
        }

        const string expected =
            "if(!map.HasOwnedCopy()||targetStart==0"
            + "||targetStart>=targetEndExclusive"
            + "||map.DescriptorSize"
            + "<UefiMemoryMapCopyPlan.MinimumDescriptorSize"
            + "||map.DescriptorCount==0"
            + "||map.DescriptorCount"
            + ">UefiMemoryMapCopyPlan.DefaultMaximumDescriptorCount)"
            + "{returnfalse;}"
            + "byte*mapBytes=(byte*)map.BufferAddress;"
            + "ulongcursor=targetStart;"
            + "for(ulongsegment=0;"
            + "segment<map.DescriptorCount"
            + "&&cursor<targetEndExclusive;segment++)"
            + "{ulongbestEnd=cursor;"
            + "for(ulongindex=0;"
            + "index<map.DescriptorCount;index++)"
            + "{byte*descriptor="
            + "mapBytes+(index*map.DescriptorSize);"
            + "uinttype=ReadUInt32(descriptor);"
            + "ulongphysicalStart=ReadUInt64(descriptor+8);"
            + "ulongpageCount=ReadUInt64(descriptor+24);"
            + "if(type!=expectedType||pageCount==0"
            + "||pageCount>~0UL/PageSize){continue;}"
            + "ulongbyteLength=pageCount*PageSize;"
            + "if(physicalStart>~0UL-byteLength)"
            + "{returnfalse;}"
            + "ulongdescriptorEnd=physicalStart+byteLength;"
            + "if(physicalStart<=cursor"
            + "&&cursor<descriptorEnd"
            + "&&descriptorEnd>bestEnd)"
            + "{bestEnd=descriptorEnd;}}"
            + "if(bestEnd==cursor){returnfalse;}"
            + "cursor=bestEnd<targetEndExclusive"
            + "?bestEnd:targetEndExclusive;}"
            + "returncursor==targetEndExclusive;";
        if (!string.Equals(
                RemoveAllWhitespace(body),
                expected,
                StringComparison.Ordinal))
        {
            failure =
                "type-descriptor-page-overflow-or-cursor";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool ValidateRunAndLedgerOrderContract(
        string stage,
        out string failure)
    {
        string? runBody = FindMethodBody(stage, "Run");
        string? prepareBody = FindMethodBody(
            stage,
            "PrepareAndTransfer");
        if (runBody is null || prepareBody is null)
        {
            failure = "method-not-found";
            return false;
        }

        int firstAllocation = NthInvocationIndex(
            runBody,
            "AllocatePages",
            1);
        int secondAllocation = NthInvocationIndex(
            runBody,
            "AllocatePages",
            2);
        int recapture = NthInvocationIndex(
            runBody,
            "TryRecaptureMemoryMap",
            1);
        int install = NthInvocationIndex(
            runBody,
            "MaterializeAndInstallStackProbe",
            1);
        int prepare = NthInvocationIndex(
            runBody,
            "PrepareAndTransfer",
            1);
        if (firstAllocation < 0
            || secondAllocation <= firstAllocation
            || recapture <= secondAllocation
            || install <= recapture
            || prepare <= install
            || NthInvocationIndex(
                runBody,
                "AllocatePages",
                3) >= 0
            || NthInvocationIndex(
                runBody,
                "TryRecaptureMemoryMap",
                2) >= 0
            || NthInvocationIndex(
                runBody,
                "MaterializeAndInstallStackProbe",
                2) >= 0
            || NthInvocationIndex(
                runBody,
                "PrepareAndTransfer",
                2) >= 0)
        {
            failure = "run-allocation-map-probe-prepare";
            return false;
        }

        string prepareCompact =
            RemoveAllWhitespace(prepareBody);
        int ledgerBuild = prepareCompact.IndexOf(
            "BuildReservationLedger(",
            StringComparison.Ordinal);
        int ledgerValidation = prepareCompact.IndexOf(
            "BootReservationTableValidator.Validate(",
            StringComparison.Ordinal);
        int mapEvidence = prepareCompact.IndexOf(
            "M01StageOneMapLedgerEvidence"
                + ".CaptureCallerAttested(",
            StringComparison.Ordinal);
        int staticPrepare = prepareCompact.IndexOf(
            "M01LoadedImageStackIntegration"
                + ".PrepareFromStaticScratch(",
            StringComparison.Ordinal);
        if (ledgerBuild < 0
            || ledgerValidation <= ledgerBuild
            || mapEvidence <= ledgerValidation
            || staticPrepare <= mapEvidence)
        {
            failure = "ledger-build-validate-evidence-prepare";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool ValidateSuccessLineWriterContract(
        string stage,
        out string failure)
    {
        string? body = FindMethodBody(
            stage,
            "WriteSuccessLine");
        if (body is null)
        {
            failure = "method-not-found";
            return false;
        }

        const string expected =
            "intlength=BootSequencePlan"
            + ".GetM01StageOneSuccessLineLength(lineIndex);"
            + "for(intoffset=0;offset<length;offset++)"
            + "{EfiEntry.Write(console,serial,"
            + "BootSequencePlan"
            + ".GetM01StageOneSuccessLineByte("
            + "lineIndex,offset));}"
            + "EfiEntry.WriteCrLf(console,serial);";
        if (!string.Equals(
                RemoveAllWhitespace(body),
                expected,
                StringComparison.Ordinal))
        {
            failure = "length-byte-loop-or-crlf";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool TestMapAndReservationLedgerContract()
    {
        string stage = RemoveComments(LoadProbeOnlySource());
        string noWhitespace = RemoveAllWhitespace(stage);

        if (!ValidateRunAndLedgerOrderContract(
                stage,
                out string orderFailure))
        {
            return Failure(
                "allocate-map-ledger-order=" + orderFailure);
        }

        string[] requiredNoWhitespace =
        [
            "UefiMemoryMapCopyPlanResult.Create",
            "FirmwareMemoryMapRecord.FlagOwnedCopy",
            "M01StageOneMapLedgerEvidence.CaptureCallerAttested",
            "M01StageOneAddressMappingEvidence",
            "PhysicalStart",
            "BootReservationTableValidator.Validate",
            "BootReservationKind.BootStackSwitchStub",
            "BootReservationKind.BootStackGuard",
            "BootReservationKind.BootStack",
            "reservationLedger[0]=loadedImage.Range.Reservation"
        ];
        foreach (string token in requiredNoWhitespace)
        {
            if (!noWhitespace.Contains(token, StringComparison.Ordinal))
            {
                return Failure("map-ledger-token=" + token);
            }
        }

        if (noWhitespace.Contains(
                "BootReservationKind.OwnedMemoryMap",
                StringComparison.Ordinal))
        {
            return Failure("duplicate-static-or-loaded-image-reservation");
        }

        string? recaptureBody = FindMethodBody(
            stage,
            "TryRecaptureMemoryMap");
        if (recaptureBody is null)
        {
            return Failure("map-recapture-method-not-found");
        }

        string recaptureCompact =
            RemoveAllWhitespace(recaptureBody);
        List<string[]> getMemoryMapCalls =
            FindInvocationArguments(
                recaptureBody,
                "GetMemoryMap");
        if (!Regex.IsMatch(
                stage,
                @"TryRecaptureMemoryMap\s*\([^;{}]*"
                    + @"out\s+ulong\s+firmwareStatus\s*\)\s*\{",
                RegexOptions.CultureInvariant)
            || !recaptureCompact.Contains(
                "firmwareStatus=NotAttemptedStatus;",
                StringComparison.Ordinal)
            || !recaptureCompact.Contains(
                "firmwareStatus=bootServices->GetMemoryMap(",
                StringComparison.Ordinal)
            || getMemoryMapCalls.Count != 1
            || getMemoryMapCalls[0].Length != 5
            || !string.Equals(
                RemoveAllWhitespace(
                    getMemoryMapCalls[0][0]),
                "&mapSize",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(
                    getMemoryMapCalls[0][1]),
                "source",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(
                    getMemoryMapCalls[0][2]),
                "&mapKey",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(
                    getMemoryMapCalls[0][3]),
                "&descriptorSize",
                StringComparison.Ordinal)
            || !string.Equals(
                RemoveAllWhitespace(
                    getMemoryMapCalls[0][4]),
                "&descriptorVersion",
                StringComparison.Ordinal)
            || !noWhitespace.Contains(
                "outulongmemoryMapFirmwareStatus",
                StringComparison.Ordinal)
            || !noWhitespace.Contains(
                "mapStatus==M01StageOneAdapterStatus"
                    + ".MemoryMapRecaptureFailed"
                    + "?memoryMapFirmwareStatus:",
                StringComparison.Ordinal))
        {
            return Failure("memory-map-firmware-status-plumbing");
        }

        return true;
    }

    private static bool TestContinuationAndCoordinatorContract()
    {
        string stage = RemoveComments(LoadProbeOnlySource());

        string[] exactCalls =
        [
            "M01LoadedImageStackIntegration.PrepareFromStaticScratch",
            "M01LoadedImageStackIntegration"
                + ".RecordTransferFromStaticState",
            "M01LoadedImageStackIntegration"
                + ".ObserveContinuationRspFromStaticState",
            "M01LoadedImageStackIntegration"
                + ".ReportUnexpectedReturnFromStaticState"
        ];
        foreach (string call in exactCalls)
        {
            if (CountFlexibleQualifiedCalls(stage, call) != 1)
            {
                return Failure("coordinator-call-count=" + call);
            }
        }

        if (CountFlexibleQualifiedCalls(
                stage,
                "M01LoadedImageStackIntegration.Prepare") != 0
            || CountFlexibleQualifiedCalls(
                stage,
                "M01LoadedImageStackIntegration"
                    + ".PrepareWithStubByteMode") != 0)
        {
            return Failure("rich-evidence-prepare-on-firmware-stack");
        }

        Match continuation = Regex.Match(
            stage,
            @"\[\s*(?:System\.Runtime\.InteropServices\.)?"
                + @"UnmanagedCallersOnly(?:Attribute)?(?:\s*\(\s*\))?\s*\]"
                + @"\s*private\s+static\s+void\s+(?<name>[A-Za-z_]\w*)"
                + @"\s*\(\s*ulong\s+[A-Za-z_]\w*\s*,\s*"
                + @"OwnedBootStackSwitchHandoff\s*\*\s*"
                + @"[A-Za-z_]\w*\s*\)",
            RegexOptions.CultureInvariant);
        if (!continuation.Success)
        {
            return Failure("direct-unmanaged-continuation");
        }

        string continuationName = continuation.Groups["name"].Value;
        string directContinuationAssignment =
            "delegate*unmanaged<ulong,"
            + "OwnedBootStackSwitchHandoff*,void>"
            + "continuation=&"
            + continuationName
            + ";";
        if (!Regex.IsMatch(
                stage,
                @"&\s*" + Regex.Escape(continuationName) + @"\b",
                RegexOptions.CultureInvariant)
            || !RemoveAllWhitespace(stage).Contains(
                directContinuationAssignment,
                StringComparison.Ordinal)
            || Regex.IsMatch(
                stage,
                @"delegate\s*\*\s*managed\b",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                stage,
                @"RuntimeExport[^\]]*\]\s*"
                    + @"(?:private\s+)?static\s+void\s+"
                    + Regex.Escape(continuationName)
                    + @"\b",
                RegexOptions.CultureInvariant))
        {
            return Failure("continuation-pointer-or-export-bypass");
        }

        string noWhitespace = RemoveAllWhitespace(stage);
        string exactSwitchType =
            "delegate*unmanaged<ulong,"
            + "OwnedBootStackSwitchHandoff*,"
            + "delegate*unmanaged<ulong,"
            + "OwnedBootStackSwitchHandoff*,void>,void>";
        if (!noWhitespace.Contains(
                exactSwitchType,
                StringComparison.Ordinal))
        {
            return Failure("stack-switch-function-pointer-type");
        }

        string? continuationBody = FindMethodBody(
            stage,
            continuationName);
        if (continuationBody is null
            || !continuationBody.Contains(
                "ObserveContinuationRspFromStaticState",
                StringComparison.Ordinal)
            || !continuationBody.Contains(
                "WriteBootSequencePlan",
                StringComparison.Ordinal)
            || !continuationBody.Contains(
                "Spin(",
                StringComparison.Ordinal))
        {
            return Failure("continuation-validation-or-nonreturn");
        }

        int recordTransfer = stage.IndexOf(
            "RecordTransferFromStaticState",
            StringComparison.Ordinal);
        int unexpectedReturn = stage.IndexOf(
            "ReportUnexpectedReturnFromStaticState",
            StringComparison.Ordinal);
        return recordTransfer >= 0
            && unexpectedReturn > recordTransfer
            || Failure("transfer-return-classification-order");
    }

    private static bool TestMarkerTopology()
    {
        string stage = RemoveComments(LoadProbeOnlySource());
        if (!ValidateSuccessLineWriterContract(
                stage,
                out string writerFailure))
        {
            return Failure(
                "success-line-writer=" + writerFailure);
        }

        string? runBody = FindMethodBody(stage, "Run");
        string? continuationBody = FindMethodBody(
            stage,
            "M01StageOneContinuation");
        string? failureBody = FindMethodBody(
            stage,
            "WriteFailure");
        string? failurePrefixBody = FindMethodBody(
            stage,
            "WriteFailurePrefix");
        if (runBody is null
            || continuationBody is null
            || failureBody is null
            || failurePrefixBody is null)
        {
            return Failure("marker-method-not-found");
        }

        List<string[]> runSuccessCalls =
            FindInvocationArguments(runBody, "WriteSuccessLine");
        if (runSuccessCalls.Count != 1
            || runSuccessCalls[0].Length != 3
            || !string.Equals(
                runSuccessCalls[0][2].Trim(),
                "0",
                StringComparison.Ordinal))
        {
            return Failure("pre-switch-is-not-test-start-only");
        }

        string continuationCompact =
            RemoveAllWhitespace(continuationBody);
        int observe = continuationCompact.IndexOf(
            "ObserveContinuationRspFromStaticState",
            StringComparison.Ordinal);
        int retrospectiveLoop = continuationCompact.IndexOf(
            "for(intlineIndex=1;"
                + "lineIndex<BootSequencePlan"
                + ".M01StageOneSuccessLineCount;lineIndex++)",
            StringComparison.Ordinal);
        int retrospectiveWrite = continuationCompact.IndexOf(
            "WriteSuccessLine(s_console,s_serial,lineIndex)",
            StringComparison.Ordinal);
        int terminalPlan = continuationCompact.IndexOf(
            "WriteBootSequencePlan",
            StringComparison.Ordinal);
        if (observe < 0
            || retrospectiveLoop <= observe
            || retrospectiveWrite <= retrospectiveLoop
            || terminalPlan <= retrospectiveWrite)
        {
            return Failure("retrospective-success-marker-order");
        }

        List<string[]> failurePrefixCalls =
            FindInvocationArguments(
                failureBody,
                "WriteFailurePrefix");
        if (failurePrefixCalls.Count != 2
            || failurePrefixCalls[0].Length != 3
            || failurePrefixCalls[1].Length != 3
            || !string.Equals(
                failurePrefixCalls[0][2].Trim(),
                "false",
                StringComparison.Ordinal)
            || !string.Equals(
                failurePrefixCalls[1][2].Trim(),
                "true",
                StringComparison.Ordinal))
        {
            return Failure("dual-failure-marker-order");
        }

        string decodedPrefixes =
            DecodeByteCharacterLiterals(failurePrefixBody);
        if (!decodedPrefixes.Contains(
                "M01_STAGE1_FAIL ",
                StringComparison.Ordinal)
            || !decodedPrefixes.Contains(
                "TEST_FAIL m01.stage1 ",
                StringComparison.Ordinal)
            || failureBody.Contains(
                "WriteSuccessLine",
                StringComparison.Ordinal))
        {
            return Failure("failure-marker-bytes-or-success-leak");
        }

        return true;
    }

    private static bool TestSourceFirewalls()
    {
        string stage = RemoveComments(LoadProbeOnlySource());

        string[] forbiddenPatterns =
        [
            @"\bBootSequence\s*\.\s*Run\b",
            @"\bRunM01LoadedImageStackIntegration\b",
            @"\.\s*RecordTransfer\s*\(",
            @"\.\s*ObserveContinuationRsp\s*\(",
            @"\.\s*ReportUnexpectedReturn\s*\(",
            @"\bdelegate\s*\*\s*managed\b",
            @"\bthrow\b",
            @"\btry\b",
            @"\bcatch\b",
            @"\bfinally\b",
            @"\bstackalloc\b",
            @"\bdynamic\b",
            @"\bMarshal\b",
            @"\bReflection\b",
            @"\bActivator\b",
            @"\bNativeMemory\b",
            @"\bExitBootServices\b",
            @"\bM01LiveAllocator\b",
            @"\bBootPageAllocator\b",
            @"\bPageTable\b",
            @"\bCR3\b",
            @"\binvlpg\b",
            @"\bThread\b",
            @"\bTask\b"
        ];
        foreach (string pattern in forbiddenPatterns)
        {
            if (Regex.IsMatch(
                    stage,
                    pattern,
                    RegexOptions.CultureInvariant))
            {
                return Failure("forbidden-stage-pattern=" + pattern);
            }
        }

        return !stage.Contains(
                "RuntimeExport",
                StringComparison.Ordinal)
            || Failure("stage-runtime-export");
    }

    private static bool TestProbeScopedMutationSelector()
    {
        const string prefix =
            "#if M01_OWNED_PAGING_PROBE\n"
            + "AllocatePages(\n"
            + "EfiLoaderCode\n"
            + "BootReservationTableValidator\n"
            + "AllocatePages(\n"
            + "EfiLoaderCode\n"
            + "BootReservationTableValidator\n"
            + "#endif\n";
        string baselineBranch = string.Join(
            Environment.NewLine,
            "AllocatePages(",
            "EfiLoaderCode",
            "BootReservationTableValidator",
            "AllocatePages(",
            "EfiLoaderCode",
            "BootReservationTableValidator",
            string.Empty);
        string source =
            prefix
            + "#if "
            + ProbeSymbol
            + "\n"
            + baselineBranch
            + "#endif\n";

        (string Search, string Replacement, string Name)[] cases =
            [
                (
                    "AllocatePages(",
                    "AllocatePagesDisabled(",
                    "allocation-removed"),
                (
                    "EfiLoaderCode",
                    "EfiLoaderData",
                    "allocation-type"),
                (
                    "BootReservationTableValidator",
                    "BootReservationTableValidatorDisabled",
                    "ledger-validation")
            ];

        string extractedBaseline =
            ExtractProbeEnabledBranches(source);
        if (!string.Equals(
                extractedBaseline,
                baselineBranch,
                StringComparison.Ordinal))
        {
            return Failure("selector-baseline-branch");
        }

        foreach ((
            string search,
            string replacement,
            string name) in cases)
        {
            if (!TryReplaceFirst(
                    baselineBranch,
                    search,
                    replacement,
                    out string expectedBranch))
            {
                return Failure(
                    "selector-expected-anchor=" + name);
            }

            if (!TryReplaceFirstAffectingProbeBranch(
                    source,
                    search,
                    replacement,
                    out string mutated))
            {
                return Failure("selector-anchor=" + name);
            }

            if (!mutated.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                return Failure("selector-prefix-modified=" + name);
            }

            string extractedMutated =
                ExtractProbeEnabledBranches(mutated);
            if (string.Equals(
                    extractedMutated,
                    extractedBaseline,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "selector-branch-unchanged=" + name);
            }

            if (!string.Equals(
                    extractedMutated,
                    expectedBranch,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "selector-wrong-branch-mutation=" + name);
            }

            string expectedSource =
                prefix
                + "#if "
                + ProbeSymbol
                + "\n"
                + expectedBranch
                + "#endif\n";
            if (!string.Equals(
                    mutated,
                    expectedSource,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "selector-wrong-source-mutation=" + name);
            }

            string prefixOnlySource =
                prefix
                + "#if "
                + ProbeSymbol
                + "\n"
                + "ProbeSentinel\n"
                + "#endif\n";
            if (TryReplaceFirstAffectingProbeBranch(
                    prefixOnlySource,
                    search,
                    replacement,
                    out string prefixOnly)
                || !string.Equals(
                    prefixOnly,
                    prefixOnlySource,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "selector-prefix-only=" + name);
            }
        }

        return !TryReplaceFirstAffectingProbeBranch(
                source,
                string.Empty,
                "invalid",
                out string emptySearch)
            && string.Equals(
                emptySearch,
                source,
                StringComparison.Ordinal)
            || Failure("selector-empty-search");
    }

    private static bool TestProbeScopedMethodMutationSelector()
    {
        const string adapterCollisionSource =
            "#if UNKNOWN_PROBE\n"
            + "private static class EarlierAdapter\n"
            + "{ public static bool Run() { return false; } }\n"
            + "#else\n"
            + "private static class OtherAdapter\n"
            + "{ public static bool Run() { return true; } }\n"
            + "#endif\n"
            + "#if M01_LOADED_IMAGE_STACK_PROBE\n"
            + "private static class M01StageOneAdapter\n"
            + "{ public static bool Run() { return EnabledSentinel(); } }\n"
            + "#else\n"
            + "private static class M01StageOneAdapter\n"
            + "{ public static bool Run() { return false; } }\n"
            + "#endif\n"
            + "private static class LaterAdapter\n"
            + "{ public static bool Run() { return false; } }\n";
        foreach (bool enabled in new[] { false, true })
        {
            if (!TrySelectProbeProfile(
                    adapterCollisionSource,
                    enabled,
                    out string selected,
                    out _)
                || !selected.Contains(
                    "#if UNKNOWN_PROBE", StringComparison.Ordinal)
                || !selected.Contains(
                    "class EarlierAdapter", StringComparison.Ordinal)
                || !selected.Contains(
                    "class OtherAdapter", StringComparison.Ordinal)
                || FindStageOneAdapterRunBody(selected) is not string body
                || !string.Equals(
                    RemoveAllWhitespace(body),
                    enabled ? "returnEnabledSentinel();" : "returnfalse;",
                    StringComparison.Ordinal))
            {
                return Failure("adapter-selector-profile-collision");
            }

            string missingAdapter = selected.Replace(
                "class M01StageOneAdapter",
                "class M01StageOneAdapterOther",
                StringComparison.Ordinal);
            // Profile selection normalizes line endings; keep the mutation exact.
            string missingRun = NormalizeLineEndings(selected).Replace(
                "class M01StageOneAdapter\n{ public static bool Run()",
                "class M01StageOneAdapter\n{ public static bool Other()",
                StringComparison.Ordinal);
            if (FindStageOneAdapterRunBody(missingAdapter) is not null
                || FindStageOneAdapterRunBody(missingRun) is not null)
            {
                return Failure("adapter-selector-outside-class-fallback");
            }
        }

        if (FindStageOneAdapterRunBody(adapterCollisionSource)
                is not string originalRun
            || !string.Equals(
                RemoveAllWhitespace(originalRun),
                "returnEnabledSentinel();",
                StringComparison.Ordinal))
        {
            return Failure("adapter-selector-original-enabled-branch");
        }

        const string prefix =
            "#if M01_OWNED_PAGING_PROBE\n"
            + "private static bool TryValidateAllocation()\n"
            + "{\n"
            + "    return (address & (PageSize - 1)) != 0;\n"
            + "}\n"
            + "private static bool RangesOverlap()\n"
            + "{\n"
            + "    return firstStart < secondEndExclusive;\n"
            + "}\n"
            + "private static void WriteSuccessLine()\n"
            + "{\n"
            + "    WriteEarlierSuccessLine();\n"
            + "}\n"
            + "private static void WriteSuccessLine()\n"
            + "{\n"
            + "    GetM01StageOneSuccessLineByte();\n"
            + "}\n"
            + "#endif\n";
        string baselineBranch = string.Join(
            Environment.NewLine,
            "private static bool TryValidateAllocation()",
            "{",
            "    return (address & (PageSize - 1)) != 0;",
            "}",
            "private static bool RangesOverlap()",
            "{",
            "    return firstStart < secondEndExclusive;",
            "}",
            "private static void WriteSuccessLine()",
            "{",
            "    GetM01StageOneSuccessLineByte();",
            "}",
            string.Empty);
        string source =
            prefix
            + "#if "
            + ProbeSymbol
            + "\n"
            + baselineBranch
            + "#endif\n";

        (
            string Method,
            string Search,
            string Replacement,
            string Name)[] cases =
        [
            (
                "TryValidateAllocation",
                "(address & (PageSize - 1)) != 0",
                "false",
                "allocation-alignment"),
            (
                "RangesOverlap",
                "firstStart < secondEndExclusive",
                "false",
                "allocation-overlap-always-false"),
            (
                "WriteSuccessLine",
                "GetM01StageOneSuccessLineByte(",
                "GetM01StageOneSuccessLineByteDisabled(",
                "success-marker-byte-source")
        ];

        string extractedBaseline =
            ExtractProbeEnabledBranches(source);
        if (!string.Equals(
                extractedBaseline,
                baselineBranch,
                StringComparison.Ordinal))
        {
            return Failure("method-selector-baseline-branch");
        }

        foreach ((
            string method,
            string search,
            string replacement,
            string name) in cases)
        {
            if (!TryMutateMethodBody(
                    baselineBranch,
                    method,
                    search,
                    replacement,
                    out string expectedBranch))
            {
                return Failure(
                    "method-selector-expected-anchor=" + name);
            }

            if (!TryMutateFirstMethodBodyAffectingProbeBranch(
                    source,
                    method,
                    search,
                    replacement,
                    out string mutated))
            {
                return Failure(
                    "method-selector-anchor=" + name);
            }

            if (!mutated.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "method-selector-prefix-modified=" + name);
            }

            string extractedMutated =
                ExtractProbeEnabledBranches(mutated);
            if (string.Equals(
                    extractedMutated,
                    extractedBaseline,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "method-selector-branch-unchanged=" + name);
            }

            if (!string.Equals(
                    extractedMutated,
                    expectedBranch,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "method-selector-wrong-branch-mutation="
                    + name);
            }

            string expectedSource =
                prefix
                + "#if "
                + ProbeSymbol
                + "\n"
                + expectedBranch
                + "#endif\n";
            if (!string.Equals(
                    mutated,
                    expectedSource,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "method-selector-wrong-source-mutation="
                    + name);
            }

            if (TryMutateFirstMethodBodyAffectingProbeBranch(
                    prefix,
                    method,
                    search,
                    replacement,
                    out string prefixOnly)
                || !string.Equals(
                    prefixOnly,
                    prefix,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "method-selector-prefix-only=" + name);
            }

            string missingTargetSource =
                prefix
                + "#if "
                + ProbeSymbol
                + "\n"
                + "ProbeSentinel\n"
                + "#endif\n";
            if (TryMutateFirstMethodBodyAffectingProbeBranch(
                    missingTargetSource,
                    method,
                    search,
                    replacement,
                    out string missingTarget)
                || !string.Equals(
                    missingTarget,
                    missingTargetSource,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "method-selector-missing-target=" + name);
            }
        }

        if (TryMutateFirstMethodBodyAffectingProbeBranch(
                source,
                string.Empty,
                "(address & (PageSize - 1)) != 0",
                "false",
                out string emptyMethodName)
            || !string.Equals(
                emptyMethodName,
                source,
                StringComparison.Ordinal))
        {
            return Failure("method-selector-empty-method-name");
        }

        return !TryMutateFirstMethodBodyAffectingProbeBranch(
                source,
                "TryValidateAllocation",
                string.Empty,
                "false",
                out string emptySearch)
            && string.Equals(
                emptySearch,
                source,
                StringComparison.Ordinal)
            || Failure("method-selector-empty-search");
    }

    private static bool TestRepresentativeMutations()
    {
        string source = LoadEfiEntrySource();
        if (!ValidateContract(source, out string baselineFailure))
        {
            return Failure(
                "unmutated-contract=" + baselineFailure);
        }

        (string Search, string Replacement, string Name)[] mutations =
        [
            (
                "#if " + ProbeSymbol,
                "#if true",
                "unguarded-probe"),
            (
                "PrepareFromStaticScratch",
                "Prepare",
                "rich-prepare-on-firmware-stack"),
            (
                "AllocatePages(",
                "AllocatePagesDisabled(",
                "allocation-removed"),
            (
                "EfiLoaderCode",
                "EfiLoaderData",
                "allocation-type"),
            (
                "RecordTransferFromStaticState",
                "RecordTransfer",
                "host-transition-overload"),
            (
                "ObserveContinuationRspFromStaticState",
                "ObserveContinuationRsp",
                "host-observe-overload"),
            (
                "ReportUnexpectedReturnFromStaticState",
                "ReportUnexpectedReturn",
                "host-return-overload"),
            (
                "UnmanagedCallersOnly",
                "ManagedContinuationOnly",
                "managed-continuation"),
            (
                "FirmwareMemoryMapRecord.FlagOwnedCopy",
                "0",
                "unowned-map"),
            (
                "BootReservationTableValidator",
                "BootReservationTableValidatorDisabled",
                "ledger-validation"),
            (
                "StackProbeEntryOffset = 64",
                "StackProbeEntryOffset = 63",
                "stack-probe-offset"),
            (
                "25 => 0xf3",
                "25 => 0xf2",
                "stack-probe-loop-displacement"),
            (
                "codePage[index] = 0xcc;",
                "codePage[index] = 0x90;",
                "code-page-trap-fill"),
            (
                "codePage[index] != 0xcc",
                "false",
                "code-page-fill-readback"),
            (
                ".TryInstall((ulong)probe)",
                ".TryInstallDisabled((ulong)probe)",
                "stack-probe-install"),
            (
                "codeFirmwareStatus == EfiSuccess;",
                "codeFirmwareStatus == EfiSuccess"
                    + " && codeAddress != 0;",
                "code-allocation-zero-ownership"),
            (
                "dataFirmwareStatus == EfiSuccess;",
                "dataFirmwareStatus == EfiSuccess"
                    + " && dataAddress != 0;",
                "data-allocation-zero-ownership"),
            (
                "TryClearStackProbeBytes(",
                "TryClearStackProbeBytesDisabled(",
                "stack-probe-clear-before-free"),
            (
                "probe[index] != 0",
                "probe[index] == 0",
                "stack-probe-clear-readback"),
            (
                "out ulong loadedImageFirmwareStatus",
                "out ulong ignoredLoadedImageFirmwareStatus",
                "loaded-image-status-out"),
            (
                "out ulong memoryMapFirmwareStatus",
                "out ulong ignoredMemoryMapFirmwareStatus",
                "memory-map-status-out"),
            (
                "firmwareStatus = bootServices->HandleProtocol",
                "ulong ignoredStatus = bootServices->HandleProtocol",
                "loaded-image-status-assignment"),
            (
                "firmwareStatus = bootServices->GetMemoryMap",
                "ulong ignoredStatus = bootServices->GetMemoryMap",
                "memory-map-status-assignment"),
            (
                "? loadedImageFirmwareStatus",
                "? NotAttemptedStatus",
                "loaded-image-status-selection"),
            (
                "? memoryMapFirmwareStatus",
                "? NotAttemptedStatus",
                "memory-map-status-selection")
        ];

        foreach ((string search, string replacement, string name)
            in mutations)
        {
            if (!TryReplaceFirstAffectingProbeBranch(
                    source,
                    search,
                    replacement,
                    out string mutated))
            {
                return Failure("mutation-anchor=" + name);
            }

            if (ValidateContract(mutated, out _))
            {
                return Failure("mutation-survived=" + name);
            }
        }

        if (!TryReplaceFirst(
                source,
                "MaterializeAndInstallStackProbe(",
                "M01ProbeOrderSwapPlaceholder(",
                out string orderSwapOne)
            || !TryReplaceFirst(
                orderSwapOne,
                "PrepareAndTransfer(",
                "MaterializeAndInstallStackProbe(",
                out string orderSwapTwo)
            || !TryReplaceFirst(
                orderSwapTwo,
                "M01ProbeOrderSwapPlaceholder(",
                "PrepareAndTransfer(",
                out string orderSwapped))
        {
            return Failure(
                "mutation-anchor=stack-probe-install-order");
        }

        if (ValidateContract(orderSwapped, out _))
        {
            return Failure(
                "mutation-survived=stack-probe-install-order");
        }

        string? runBody = FindStageOneAdapterRunBody(source);
        if (runBody is null)
        {
            return Failure("mutation-anchor=run-map-order");
        }

        int secondAllocation = NthInvocationIndex(
            runBody,
            "AllocatePages",
            2);
        int mapRecapture = NthInvocationIndex(
            runBody,
            "TryRecaptureMemoryMap",
            1);
        if (secondAllocation < 0
            || mapRecapture <= secondAllocation)
        {
            return Failure("mutation-anchor=run-map-order");
        }

        string swappedRun = ReplaceAt(
            runBody,
            mapRecapture,
            "TryRecaptureMemoryMap".Length,
            "M01MapOrderSwapPlaceholder");
        swappedRun = ReplaceAt(
            swappedRun,
            secondAllocation,
            "AllocatePages".Length,
            "TryRecaptureMemoryMap");
        int placeholder = swappedRun.IndexOf(
            "M01MapOrderSwapPlaceholder",
            StringComparison.Ordinal);
        if (placeholder < 0)
        {
            return Failure("mutation-anchor=run-map-order");
        }

        swappedRun = ReplaceAt(
            swappedRun,
            placeholder,
            "M01MapOrderSwapPlaceholder".Length,
            "AllocatePages");
        if (!TryReplaceFirst(
                source,
                runBody,
                swappedRun,
                out string mapOrderSwapped)
            || ValidateContract(mapOrderSwapped, out _))
        {
            return Failure(
                "mutation-survived=run-map-order");
        }

        (
            string Method,
            string Search,
            string Replacement,
            string Name)[] methodMutations =
        [
            (
                "TryValidateAllocation",
                "(address & (PageSize - 1)) != 0",
                "false",
                "allocation-alignment"),
            (
                "TryValidateAllocation",
                "pageCount > ~0UL / PageSize",
                "false",
                "allocation-multiply-overflow"),
            (
                "TryValidateAllocation",
                "address > ~0UL - byteLength",
                "false",
                "allocation-add-overflow"),
            (
                "RangesOverlap",
                "firstStart < secondEndExclusive",
                "false",
                "allocation-overlap-always-false"),
            (
                "MapContainsAllocation",
                "type != expectedType",
                "false",
                "map-allocation-type-filter"),
            (
                "WriteSuccessLine",
                "GetM01StageOneSuccessLineByte(",
                "GetM01StageOneSuccessLineByteDisabled(",
                "success-marker-byte-source"),
            (
                "WriteSuccessLine",
                "EfiEntry.WriteCrLf(console, serial);",
                "return;",
                "success-marker-crlf"),
            (
                "GetStackProbeByte",
                "return index switch",
                "return 1 ^ (index switch",
                "stack-probe-byte-plan-wrapper"),
            (
                "MaterializeAndInstallStackProbe",
                "return System.Runtime.X64StackProbeRuntime",
                "probe[0] ^= 1;"
                    + "\n            return System.Runtime"
                    + ".X64StackProbeRuntime",
                "stack-probe-post-readback-corruption")
        ];
        foreach ((
            string method,
            string search,
            string replacement,
            string name) in methodMutations)
        {
            if (!TryMutateFirstMethodBodyAffectingProbeBranch(
                    source,
                    method,
                    search,
                    replacement,
                    out string mutated))
            {
                return Failure(
                    "mutation-anchor=" + name);
            }

            if (ValidateContract(mutated, out _))
            {
                return Failure(
                    "mutation-survived=" + name);
            }
        }

        string runtime = LoadMinimalRuntimeSource();
        if (!ValidateRuntimeStackProbeContract(
                RemoveComments(runtime),
                out string runtimeBaselineFailure))
        {
            return Failure(
                "unmutated-runtime-contract="
                    + runtimeBaselineFailure);
        }

        (string Search, string Replacement, string Name)[]
            runtimeMutations =
        [
            (
                "[RuntimeExport(\"RhpStackProbe\")]",
                "[RuntimeExport(\"RhpStackProbeDisabled\")]",
                "runtime-export"),
            (
                "(probeEntry & 15) != 0",
                "(probeEntry & 7) != 0",
                "runtime-install-alignment"),
            (
                "delegate* managed<void>",
                "delegate* unmanaged<void>",
                "runtime-managed-tail-dispatch"),
            (
                "((delegate* managed<void>)(void*)probeEntry)();",
                "return;",
                "runtime-noop-bridge")
        ];
        foreach ((string search, string replacement, string name)
            in runtimeMutations)
        {
            if (!TryReplaceFirst(
                    runtime,
                    search,
                    replacement,
                    out string mutated))
            {
                return Failure(
                    "runtime-mutation-anchor=" + name);
            }

            if (ValidateRuntimeStackProbeContract(
                RemoveComments(mutated),
                out _))
            {
                return Failure(
                    "runtime-mutation-survived=" + name);
            }
        }

        string? bridgeBody = FindMethodBody(
            runtime,
            "RhpStackProbe");
        if (bridgeBody is null
            || !TryReplaceFirst(
                bridgeBody,
                "while (true)",
                "if (true)",
                out string mutatedBridge))
        {
            return Failure(
                "runtime-mutation-anchor="
                + "runtime-null-fail-closed");
        }

        string nullPathMutation = runtime.Replace(
            bridgeBody,
            mutatedBridge,
            StringComparison.Ordinal);
        if (ValidateRuntimeStackProbeContract(
                RemoveComments(nullPathMutation),
                out _))
        {
            return Failure(
                "runtime-mutation-survived="
                + "runtime-null-fail-closed");
        }

        return true;
    }

    private static bool TestIssueMarkerUniqueness()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string expected = Path.GetFullPath(
            Path.Combine(
                repositoryRoot,
                "tests",
                "OS.HostTests",
                "M01LoadedImageStackEfiEntryTests.cs"));
        string marker = "CLROS_" + "M01_021_READY";
        List<string> markerSources = [];

        foreach (string candidate in Directory.EnumerateFiles(
            repositoryRoot,
            "*.cs",
            SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(candidate);
            if (IsGeneratedOrReferencePath(fullPath)
                || !File.ReadAllText(fullPath).Contains(
                    marker,
                    StringComparison.Ordinal))
            {
                continue;
            }

            markerSources.Add(fullPath);
        }

        if (markerSources.Count != 1
            || !string.Equals(
                markerSources[0],
                expected,
                StringComparison.OrdinalIgnoreCase))
        {
            return Failure("issue-marker-source-count");
        }

        string testSource = File.ReadAllText(expected);
        return Regex.Matches(
                testSource,
                "Console\\.WriteLine\\(\"CLROS_M01_021_READY\"\\)",
                RegexOptions.CultureInvariant).Count == 1
            || Failure("issue-marker-emission-count");
    }

    private static bool ValidateContract(
        string source,
        out string failure)
    {
        try
        {
            if (!TrySelectProbeProfile(
                    source,
                    enabled: false,
                    out string defaultSource,
                    out failure)
                || defaultSource.Contains(
                    "M01LoadedImageStackIntegration",
                    StringComparison.Ordinal)
                || FindStageOneAdapterRunBody(defaultSource)
                    is not string defaultRun
                || !string.Equals(
                    RemoveAllWhitespace(defaultRun),
                    "returnfalse;",
                    StringComparison.Ordinal))
            {
                failure = "default-profile";
                return false;
            }

            string stage = RemoveComments(
                ExtractProbeEnabledBranches(source));
            List<string[]> allocations =
                FindInvocationArguments(stage, "AllocatePages");
            if (allocations.Count != 2
                || !allocations.Any(
                    args => args.Length == 4
                        && args[1].Contains(
                            "EfiLoaderCode",
                            StringComparison.Ordinal))
                || !allocations.Any(
                    args => args.Length == 4
                        && args[1].Contains(
                            "EfiLoaderData",
                            StringComparison.Ordinal)))
            {
                failure = "allocations";
                return false;
            }

            string[] exactCalls =
            [
                "M01LoadedImageStackIntegration"
                    + ".PrepareFromStaticScratch",
                "M01LoadedImageStackIntegration"
                    + ".RecordTransferFromStaticState",
                "M01LoadedImageStackIntegration"
                    + ".ObserveContinuationRspFromStaticState",
                "M01LoadedImageStackIntegration"
                    + ".ReportUnexpectedReturnFromStaticState"
            ];
            foreach (string call in exactCalls)
            {
                if (CountFlexibleQualifiedCalls(stage, call) != 1)
                {
                    failure = "call=" + call;
                    return false;
                }
            }

            if (CountFlexibleQualifiedCalls(
                    stage,
                    "M01LoadedImageStackIntegration.Prepare") != 0
                || CountFlexibleQualifiedCalls(
                    stage,
                    "M01LoadedImageStackIntegration"
                        + ".PrepareWithStubByteMode") != 0)
            {
                failure = "rich-prepare";
                return false;
            }

            if (!ValidateEfiStackProbeContract(
                    stage,
                    out string stackProbeFailure))
            {
                failure = "stack-probe=" + stackProbeFailure;
                return false;
            }

            if (!ValidateRunAndLedgerOrderContract(
                    stage,
                    out string orderFailure))
            {
                failure = "order=" + orderFailure;
                return false;
            }

            if (!ValidateSuccessLineWriterContract(
                    stage,
                    out string writerFailure))
            {
                failure = "success-writer=" + writerFailure;
                return false;
            }

            if (!ValidateAllocationSafetyHelperContract(
                    stage,
                    out string allocationHelperFailure))
            {
                failure =
                    "allocation-helper="
                    + allocationHelperFailure;
                return false;
            }

            if (!ValidateMapContainsAllocationContract(
                    stage,
                    out string mapAllocationFailure))
            {
                failure =
                    "map-allocation="
                    + mapAllocationFailure;
                return false;
            }

            string noWhitespace = RemoveAllWhitespace(stage);
            string[] tokens =
            [
                "privateconstuintAllocateAnyPages=0;",
                "privateconstuintEfiLoaderCode=1;",
                "privateconstuintEfiLoaderData=2;",
                "UnmanagedCallersOnly",
                "UefiMemoryMapCopyPlanResult.Create",
                "FirmwareMemoryMapRecord.FlagOwnedCopy",
                "BootReservationTableValidator.Validate",
                "reservationLedger[0]=loadedImage.Range.Reservation",
                "BootReservationKind.BootStackSwitchStub",
                "BootReservationKind.BootStackGuard",
                "BootReservationKind.BootStack"
            ];
            foreach (string token in tokens)
            {
                if (!noWhitespace.Contains(
                        token,
                        StringComparison.Ordinal))
                {
                    failure = "token=" + token;
                    return false;
                }
            }

            string? runBody = FindMethodBody(stage, "Run");
            string? cleanupBody = FindMethodBody(
                stage,
                "FailBeforeTransfer");
            string? clearBody = FindMethodBody(
                stage,
                "TryClearStackProbeBytes");
            string? captureBody = FindMethodBody(
                stage,
                "TryCaptureLoadedImage");
            string? mapBody = FindMethodBody(
                stage,
                "TryRecaptureMemoryMap");
            if (runBody is null
                || cleanupBody is null
                || clearBody is null
                || captureBody is null
                || mapBody is null)
            {
                failure = "adapter-method";
                return false;
            }

            string runCompact = RemoveAllWhitespace(runBody);
            string cleanupCompact =
                RemoveAllWhitespace(cleanupBody);
            string clearCompact =
                RemoveAllWhitespace(clearBody);
            string captureCompact =
                RemoveAllWhitespace(captureBody);
            string mapCompact =
                RemoveAllWhitespace(mapBody);
            int dataFree = cleanupCompact.IndexOf(
                "bootServices->FreePages("
                    + "dataAddress,DataArenaPageCount)",
                StringComparison.Ordinal);
            int clearProbe = cleanupCompact.IndexOf(
                "TryClearStackProbeBytes(codeAddress)",
                StringComparison.Ordinal);
            int codeFree = cleanupCompact.IndexOf(
                "bootServices->FreePages("
                    + "codeAddress,CodePageCount)",
                StringComparison.Ordinal);
            int runtimeClear = clearCompact.IndexOf(
                "X64StackProbeRuntime"
                    + ".TryClear(stackProbeAddress)",
                StringComparison.Ordinal);
            int clearBytes = clearCompact.IndexOf(
                "probe[index]=0;",
                StringComparison.Ordinal);
            int clearReadback = clearCompact.IndexOf(
                "probe[index]!=0",
                StringComparison.Ordinal);
            if (!runCompact.Contains(
                    "boolcodeAllocated="
                        + "codeFirmwareStatus==EfiSuccess;",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "booldataAllocated="
                        + "dataFirmwareStatus==EfiSuccess;",
                    StringComparison.Ordinal)
                || runCompact.Contains(
                    "codeFirmwareStatus==EfiSuccess&&",
                    StringComparison.Ordinal)
                || runCompact.Contains(
                    "dataFirmwareStatus==EfiSuccess&&",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "outulongloadedImageFirmwareStatus",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "captureStatus==M01StageOneAdapterStatus"
                        + ".LoadedImageProtocolFailed"
                        + "?loadedImageFirmwareStatus:",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "outulongmemoryMapFirmwareStatus",
                    StringComparison.Ordinal)
                || !runCompact.Contains(
                    "mapStatus==M01StageOneAdapterStatus"
                        + ".MemoryMapRecaptureFailed"
                        + "?memoryMapFirmwareStatus:",
                    StringComparison.Ordinal)
                || !captureCompact.Contains(
                    "firmwareStatus=NotAttemptedStatus;",
                    StringComparison.Ordinal)
                || !captureCompact.Contains(
                    "firmwareStatus=bootServices"
                        + "->HandleProtocol(",
                    StringComparison.Ordinal)
                || !mapCompact.Contains(
                    "firmwareStatus=NotAttemptedStatus;",
                    StringComparison.Ordinal)
                || !mapCompact.Contains(
                    "firmwareStatus=bootServices"
                        + "->GetMemoryMap(",
                    StringComparison.Ordinal)
                || dataFree < 0
                || clearProbe <= dataFree
                || codeFree <= clearProbe
                || runtimeClear < 0
                || clearBytes <= runtimeClear
                || clearReadback <= clearBytes)
            {
                failure = "ownership-status-or-cleanup";
                return false;
            }

            if (Regex.IsMatch(
                    stage,
                    @"\.\s*(?:RecordTransfer|ObserveContinuationRsp|"
                        + @"ReportUnexpectedReturn)\s*\(",
                    RegexOptions.CultureInvariant))
            {
                failure = "host-overload";
                return false;
            }

            failure = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            failure = ex.GetType().Name + ":" + ex.Message;
            return false;
        }
    }

    private static List<int> FindM01SuccessLineIndexes(
        string source)
    {
        List<int> result = [];
        foreach ((string name, string[] arguments, int _) in
            FindInvocations(source))
        {
            if (!name.Contains(
                    "M01StageOne",
                    StringComparison.OrdinalIgnoreCase)
                || !name.Contains(
                    "Success",
                    StringComparison.OrdinalIgnoreCase)
                || arguments.Length == 0)
            {
                continue;
            }

            string finalArgument = arguments[^1].Trim();
            if (int.TryParse(finalArgument, out int lineIndex))
            {
                result.Add(lineIndex);
            }
        }

        return result;
    }

    private static int FindSuccessLineCallIndex(
        string source,
        int lineIndex)
    {
        foreach ((string name, string[] arguments, int index) in
            FindInvocations(source))
        {
            if (name.Contains(
                    "M01StageOne",
                    StringComparison.OrdinalIgnoreCase)
                && name.Contains(
                    "Success",
                    StringComparison.OrdinalIgnoreCase)
                && arguments.Length != 0
                && string.Equals(
                    arguments[^1].Trim(),
                    lineIndex.ToString(),
                    StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static List<string[]> FindInvocationArguments(
        string source,
        string methodName)
    {
        List<string[]> result = [];
        foreach ((string name, string[] arguments, int _) in
            FindInvocations(source))
        {
            if (string.Equals(
                    name,
                    methodName,
                    StringComparison.Ordinal))
            {
                result.Add(arguments);
            }
        }

        return result;
    }

    private static int NthInvocationIndex(
        string source,
        string methodName,
        int ordinal)
    {
        int found = 0;
        foreach ((string name, string[] _, int index) in
            FindInvocations(source))
        {
            if (!string.Equals(
                    name,
                    methodName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            found++;
            if (found == ordinal)
            {
                return index;
            }
        }

        return -1;
    }

    private static List<(string Name, string[] Arguments, int Index)>
        FindInvocations(string source)
    {
        string sanitized = SanitizeStringsAndCharacters(source);
        List<(string Name, string[] Arguments, int Index)> calls = [];

        for (int index = 0; index < sanitized.Length; index++)
        {
            if (!IsIdentifierStart(sanitized[index]))
            {
                continue;
            }

            int nameStart = index;
            index++;
            while (index < sanitized.Length
                && IsIdentifierPart(sanitized[index]))
            {
                index++;
            }

            string name = sanitized[nameStart..index];
            int cursor = SkipWhitespace(sanitized, index);
            if (cursor >= sanitized.Length
                || sanitized[cursor] != '(')
            {
                index--;
                continue;
            }

            int close = FindMatchingDelimiter(
                sanitized,
                cursor,
                '(',
                ')');
            if (close < 0)
            {
                index--;
                continue;
            }

            int beforeName = PreviousNonWhitespace(
                sanitized,
                nameStart - 1);
            if (beforeName >= 0
                && sanitized[beforeName] == '*')
            {
                index--;
                continue;
            }

            string argumentsText =
                source[(cursor + 1)..close];
            calls.Add(
                (
                    name,
                    SplitTopLevelArguments(argumentsText),
                    nameStart));
            index = close;
        }

        return calls;
    }

    private static string[] SplitTopLevelArguments(
        string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return [];
        }

        string sanitized = SanitizeStringsAndCharacters(arguments);
        List<string> parts = [];
        int start = 0;
        int round = 0;
        int square = 0;
        int angle = 0;

        for (int index = 0; index < sanitized.Length; index++)
        {
            switch (sanitized[index])
            {
                case '(':
                    round++;
                    break;
                case ')':
                    round--;
                    break;
                case '[':
                    square++;
                    break;
                case ']':
                    square--;
                    break;
                case '<':
                    angle++;
                    break;
                case '>':
                    if (angle > 0)
                    {
                        angle--;
                    }
                    break;
                case ',':
                    if (round == 0
                        && square == 0
                        && angle == 0)
                    {
                        parts.Add(arguments[start..index]);
                        start = index + 1;
                    }
                    break;
            }
        }

        parts.Add(arguments[start..]);
        return [.. parts];
    }

    private static int CountFlexibleQualifiedCalls(
        string source,
        string qualifiedName)
    {
        string[] segments = qualifiedName.Split('.');
        string pattern = string.Join(
            @"\s*\.\s*",
            segments.Select(Regex.Escape));
        return Regex.Matches(
            source,
            pattern + @"\s*\(",
            RegexOptions.CultureInvariant).Count;
    }

    private static string? FindMethodBodyContaining(
        string source,
        string token)
    {
        int tokenIndex = source.IndexOf(
            token,
            StringComparison.Ordinal);
        if (tokenIndex < 0)
        {
            return null;
        }

        int open = source.LastIndexOf(
            '{',
            tokenIndex);
        while (open >= 0)
        {
            int close = FindMatchingDelimiter(
                SanitizeStringsAndCharacters(source),
                open,
                '{',
                '}');
            if (close > tokenIndex)
            {
                return source[(open + 1)..close];
            }

            open = source.LastIndexOf(
                '{',
                open - 1);
        }

        return null;
    }

    private static string? FindStageOneAdapterRunBody(string source)
    {
        // Unknown probe branches remain intact. Select the exact adapter before
        // looking for Run so an earlier or later sibling cannot satisfy its contract.
        string sanitized = SanitizeStringsAndCharacters(RemoveComments(source));
        Match adapter = Regex.Match(
            sanitized,
            @"\bclass\s+M01StageOneAdapter\s*\{",
            RegexOptions.CultureInvariant);
        if (!adapter.Success)
        {
            return null;
        }

        int open = adapter.Index + adapter.Length - 1;
        int close = FindMatchingDelimiter(sanitized, open, '{', '}');
        return close > open
            ? FindMethodBody(source[(open + 1)..close], "Run")
            : null;
    }

    private static string? FindMethodBody(
        string source,
        string methodName)
    {
        Match match = Regex.Match(
            source,
            @"\b(?:private|public|internal)\s+static\s+"
                + @"(?:unsafe\s+)?"
                + @"[A-Za-z_][A-Za-z_0-9.]*\s*\*?\s+"
                + Regex.Escape(methodName)
                + @"\s*\([^;{}]*\)\s*\{",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        int open = source.IndexOf(
            '{',
            match.Index + match.Length - 1);
        string sanitized = SanitizeStringsAndCharacters(source);
        int close = FindMatchingDelimiter(
            sanitized,
            open,
            '{',
            '}');
        return close > open
            ? source[(open + 1)..close]
            : null;
    }

    private static int FindMatchingDelimiter(
        string source,
        int openIndex,
        char open,
        char close)
    {
        int depth = 0;
        for (int index = openIndex; index < source.Length; index++)
        {
            if (source[index] == open)
            {
                depth++;
            }
            else if (source[index] == close)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static bool TrySelectProbeProfile(
        string source,
        bool enabled,
        out string selected,
        out string failure)
    {
        StringBuilder output = new();
        Stack<PreprocessorFrame> stack = new();
        bool emit = true;

        foreach (string line in NormalizeLineEndings(source)
            .Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
            {
                string condition = trimmed[4..].Trim();
                bool isStageOne = string.Equals(
                    condition,
                    ProbeSymbol,
                    StringComparison.Ordinal);
                bool isProbe = isStageOne || string.Equals(condition, DoubleFaultSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, UnexpectedPanicSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, MemoryMapSymbol, StringComparison.Ordinal)
            || string.Equals(condition, EntropySymbol, StringComparison.Ordinal)
            || string.Equals(condition, TypeManagerSymbol, StringComparison.Ordinal);
                bool selectedValue = isStageOne && enabled;
                stack.Push(
                    new PreprocessorFrame(
                        isProbe,
                        emit,
                        SeenElse: false,
                        SelectedValue: selectedValue));
                if (isProbe)
                {
                    emit = emit && selectedValue;
                }
                else if (emit)
                {
                    output.AppendLine(line);
                }

                continue;
            }

            if (trimmed.StartsWith("#elif", StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                {
                    selected = string.Empty;
                    failure = "orphan-elif";
                    return false;
                }

                PreprocessorFrame frame = stack.Peek();
                if (frame.IsProbe)
                {
                    selected = string.Empty;
                    failure = "probe-elif";
                    return false;
                }

                if (emit)
                {
                    output.AppendLine(line);
                }

                continue;
            }

            if (string.Equals(
                    trimmed,
                    "#else",
                    StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                {
                    selected = string.Empty;
                    failure = "orphan-else";
                    return false;
                }

                PreprocessorFrame frame = stack.Pop();
                if (frame.SeenElse)
                {
                    selected = string.Empty;
                    failure = "duplicate-else";
                    return false;
                }

                frame = frame with { SeenElse = true };
                stack.Push(frame);
                if (frame.IsProbe)
                {
                    emit = frame.ParentEmit && !frame.SelectedValue;
                }
                else if (emit)
                {
                    output.AppendLine(line);
                }

                continue;
            }

            if (string.Equals(
                    trimmed,
                    "#endif",
                    StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                {
                    selected = string.Empty;
                    failure = "orphan-endif";
                    return false;
                }

                PreprocessorFrame frame = stack.Pop();
                if (!frame.IsProbe && emit)
                {
                    output.AppendLine(line);
                }

                emit = frame.ParentEmit;
                continue;
            }

            if (emit)
            {
                output.AppendLine(line);
            }
        }

        if (stack.Count != 0)
        {
            selected = string.Empty;
            failure = "unterminated-if";
            return false;
        }

        selected = output.ToString();
        failure = string.Empty;
        return true;
    }

    private static string ExtractProbeEnabledBranches(
        string source)
    {
        StringBuilder output = new();
        Stack<PreprocessorFrame> stack = new();
        int probeDepth = 0;
        bool emit = false;

        foreach (string line in NormalizeLineEndings(source)
            .Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
            {
                bool isProbe = string.Equals(
                    trimmed[4..].Trim(),
                    ProbeSymbol,
                    StringComparison.Ordinal);
                stack.Push(
                    new PreprocessorFrame(
                        isProbe,
                        emit,
                        SeenElse: false));
                if (isProbe)
                {
                    probeDepth++;
                    emit = true;
                }

                continue;
            }

            if (string.Equals(
                    trimmed,
                    "#else",
                    StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Orphan preprocessor #else.");
                }

                PreprocessorFrame frame = stack.Pop();
                stack.Push(frame with { SeenElse = true });
                if (frame.IsProbe)
                {
                    emit = false;
                }

                continue;
            }

            if (string.Equals(
                    trimmed,
                    "#endif",
                    StringComparison.Ordinal))
            {
                if (stack.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Orphan preprocessor #endif.");
                }

                PreprocessorFrame frame = stack.Pop();
                if (frame.IsProbe)
                {
                    probeDepth--;
                    emit = frame.ParentEmit;
                }

                continue;
            }

            if (probeDepth > 0 && emit)
            {
                output.AppendLine(line);
            }
        }

        if (stack.Count != 0)
        {
            throw new InvalidOperationException(
                "Unterminated preprocessor block.");
        }

        return output.ToString();
    }

    private static string SelectProbeProfileOrThrow(
        string source,
        bool enabled)
    {
        if (!TrySelectProbeProfile(
                source,
                enabled,
                out string selected,
                out string failure))
        {
            throw new InvalidOperationException(
                "Could not select probe profile: " + failure);
        }

        return selected;
    }

    private static string LoadProbeOnlySource()
    {
        return ExtractProbeEnabledBranches(
            LoadEfiEntrySource());
    }

    private static string LoadEfiEntrySource()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "Boot",
                "EfiEntry.cs"));
    }

    private static string LoadMinimalRuntimeSource()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "Runtime",
                "MinimalRuntime.cs"));
    }

    private static bool TryReplaceFirstAffectingProbeBranch(
        string source,
        string search,
        string replacement,
        out string mutated)
    {
        mutated = source;
        if (search.Length == 0)
        {
            return false;
        }

        string baseline =
            ExtractProbeEnabledBranches(source);
        int index = source.IndexOf(
            search,
            StringComparison.Ordinal);
        while (index >= 0)
        {
            string candidate =
                source[..index]
                + replacement
                + source[(index + search.Length)..];
            if (!string.Equals(
                    ExtractProbeEnabledBranches(candidate),
                    baseline,
                    StringComparison.Ordinal))
            {
                mutated = candidate;
                return true;
            }

            index = source.IndexOf(
                search,
                index + 1,
                StringComparison.Ordinal);
        }

        return false;
    }

    private static bool TryMutateFirstMethodBodyAffectingProbeBranch(
        string source,
        string methodName,
        string search,
        string replacement,
        out string mutated)
    {
        mutated = source;
        if (methodName.Length == 0 || search.Length == 0)
        {
            return false;
        }

        string baseline =
            ExtractProbeEnabledBranches(source);
        string sanitized =
            SanitizeStringsAndCharacters(source);
        MatchCollection methods = Regex.Matches(
            sanitized,
            @"\b(?:private|public|internal)\s+static\s+"
                + @"(?:unsafe\s+)?"
                + @"[A-Za-z_][A-Za-z_0-9.]*\s*\*?\s+"
                + Regex.Escape(methodName)
                + @"\s*\([^;{}]*\)\s*\{",
            RegexOptions.CultureInvariant);
        foreach (Match method in methods)
        {
            int open = sanitized.IndexOf(
                '{',
                method.Index + method.Length - 1);
            if (open < 0)
            {
                continue;
            }

            int close = FindMatchingDelimiter(
                sanitized,
                open,
                '{',
                '}');
            if (close <= open)
            {
                continue;
            }

            int bodyStart = open + 1;
            int searchIndex = source.IndexOf(
                search,
                bodyStart,
                close - bodyStart,
                StringComparison.Ordinal);
            while (searchIndex >= 0)
            {
                string candidate =
                    source[..searchIndex]
                    + replacement
                    + source[(searchIndex + search.Length)..];
                if (!string.Equals(
                        ExtractProbeEnabledBranches(candidate),
                        baseline,
                        StringComparison.Ordinal))
                {
                    mutated = candidate;
                    return true;
                }

                int nextStart = searchIndex + 1;
                int remaining = close - nextStart;
                if (remaining < search.Length)
                {
                    break;
                }

                searchIndex = source.IndexOf(
                    search,
                    nextStart,
                    remaining,
                    StringComparison.Ordinal);
            }
        }

        return false;
    }

    private static bool TryReplaceFirst(
        string source,
        string search,
        string replacement,
        out string mutated)
    {
        int index = source.IndexOf(
            search,
            StringComparison.Ordinal);
        if (index < 0)
        {
            mutated = source;
            return false;
        }

        mutated =
            source[..index]
            + replacement
            + source[(index + search.Length)..];
        return true;
    }

    private static bool TryMutateMethodBody(
        string source,
        string methodName,
        string search,
        string replacement,
        out string mutated)
    {
        string? body = FindMethodBody(source, methodName);
        if (body is null
            || !TryReplaceFirst(
                body,
                search,
                replacement,
                out string mutatedBody)
            || !TryReplaceFirst(
                source,
                body,
                mutatedBody,
                out mutated))
        {
            mutated = source;
            return false;
        }

        return true;
    }

    private static string ReplaceAt(
        string source,
        int index,
        int length,
        string replacement)
    {
        return source[..index]
            + replacement
            + source[(index + length)..];
    }

    private static string RemoveComments(string source)
    {
        string sanitized = SanitizeStringsAndCharacters(source);
        StringBuilder result = new(source.Length);

        for (int index = 0; index < source.Length;)
        {
            if (index + 1 < source.Length
                && sanitized[index] == '/'
                && sanitized[index + 1] == '/')
            {
                while (index < source.Length
                    && source[index] != '\r'
                    && source[index] != '\n')
                {
                    result.Append(' ');
                    index++;
                }

                continue;
            }

            if (index + 1 < source.Length
                && sanitized[index] == '/'
                && sanitized[index + 1] == '*')
            {
                result.Append("  ");
                index += 2;
                while (index + 1 < source.Length
                    && !(sanitized[index] == '*'
                        && sanitized[index + 1] == '/'))
                {
                    result.Append(
                        source[index] is '\r' or '\n'
                            ? source[index]
                            : ' ');
                    index++;
                }

                if (index + 1 < source.Length)
                {
                    result.Append("  ");
                    index += 2;
                }

                continue;
            }

            result.Append(source[index]);
            index++;
        }

        return result.ToString();
    }

    private static string DecodeByteCharacterLiterals(
        string source)
    {
        StringBuilder result = new();
        foreach (Match match in Regex.Matches(
            source,
            @"\(\s*byte\s*\)\s*'(?<value>\\.|[^'])'",
            RegexOptions.CultureInvariant))
        {
            string value = match.Groups["value"].Value;
            result.Append(
                value switch
                {
                    @"\0" => '\0',
                    @"\r" => '\r',
                    @"\n" => '\n',
                    @"\t" => '\t',
                    @"\'" => '\'',
                    @"\\" => '\\',
                    _ => value[0]
                });
        }

        return result.ToString();
    }

    private static string SanitizeStringsAndCharacters(
        string source)
    {
        char[] result = source.ToCharArray();
        bool inString = false;
        bool inCharacter = false;
        bool inVerbatim = false;
        bool escaped = false;

        for (int index = 0; index < result.Length; index++)
        {
            char current = result[index];
            if (inString)
            {
                if (current is '\r' or '\n')
                {
                    continue;
                }

                result[index] = ' ';
                if (inVerbatim)
                {
                    if (current == '"'
                        && index + 1 < result.Length
                        && result[index + 1] == '"')
                    {
                        result[index + 1] = ' ';
                        index++;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                        inVerbatim = false;
                    }
                }
                else if (!escaped && current == '"')
                {
                    inString = false;
                }
                else
                {
                    escaped = !escaped && current == '\\';
                }

                continue;
            }

            if (inCharacter)
            {
                if (current is '\r' or '\n')
                {
                    inCharacter = false;
                    escaped = false;
                    continue;
                }

                result[index] = ' ';
                if (!escaped && current == '\'')
                {
                    inCharacter = false;
                }
                else
                {
                    escaped = !escaped && current == '\\';
                }

                continue;
            }

            if (current == '"')
            {
                inString = true;
                inVerbatim = index > 0
                    && result[index - 1] == '@';
                escaped = false;
                result[index] = ' ';
            }
            else if (current == '\'')
            {
                inCharacter = true;
                escaped = false;
                result[index] = ' ';
            }
        }

        return new string(result);
    }

    private static string RemoveAllWhitespace(string source)
    {
        StringBuilder result = new(source.Length);
        foreach (char value in NormalizeLineEndings(source))
        {
            if (!char.IsWhiteSpace(value))
            {
                result.Append(value);
            }
        }

        return result.ToString();
    }

    private static string NormalizeLineEndings(string source)
    {
        return source.Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal);
    }

    private static int IndexOfAny(
        string source,
        params string[] values)
    {
        int earliest = -1;
        foreach (string value in values)
        {
            int index = source.IndexOf(
                value,
                StringComparison.Ordinal);
            if (index >= 0 && (earliest < 0 || index < earliest))
            {
                earliest = index;
            }
        }

        return earliest;
    }

    private static int SkipWhitespace(
        string source,
        int index)
    {
        while (index < source.Length
            && char.IsWhiteSpace(source[index]))
        {
            index++;
        }

        return index;
    }

    private static int PreviousNonWhitespace(
        string source,
        int index)
    {
        while (index >= 0
            && char.IsWhiteSpace(source[index]))
        {
            index--;
        }

        return index;
    }

    private static bool IsIdentifierStart(char value)
    {
        return value == '_' || char.IsLetter(value);
    }

    private static bool IsIdentifierPart(char value)
    {
        return value == '_' || char.IsLetterOrDigit(value);
    }

    private static bool IsGeneratedOrReferencePath(
        string fullPath)
    {
        string normalized = fullPath.Replace('\\', '/');
        return normalized.Contains(
                "/bin/",
                StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(
                "/obj/",
                StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(
                "/artifacts/",
                StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(
                "/references/",
                StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(
                "/.git/",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool Failure(string reason)
    {
        Console.WriteLine("M01_021_DIAGNOSTIC " + reason);
        return false;
    }

    private readonly record struct PreprocessorFrame(
        bool IsProbe,
        bool ParentEmit,
        bool SeenElse,
        bool SelectedValue = false);
}
