using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;

internal static class M01InstructionPublicationEfiEntryTests
{
    private const string MemoryMapSymbol = "M03_MEMORY_MAP_CAPTURE_PROBE";
    private const string TypeManagerSymbol = "M04_TYPE_MANAGER_MODULE_REGISTRATION_PROBE";
    private const string TypedEbsSymbol = "M03_TYPED_EXIT_BOOT_SERVICES_CALL_PROBE";
    private const string GcStaticSymbol = "M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE";
    private const string EntropySymbol = "M03_FIRMWARE_ENTROPY_SEED_PROBE";
    private const string FirstExitSymbol = "M03_FIRST_REAL_EXIT_BOOT_SERVICES_PROBE";
    private const string IssueTestName = "m01.035";
    private const string ReadyMarker = "CLROS_M01_035_READY";
    private const string StageOneSymbol =
        "M01_LOADED_IMAGE_STACK_PROBE";
    private const string StageTwoSymbol =
        "M01_LIVE_ALLOCATOR_PROBE";
    private const string StageThreeSymbol =
        "M01_OWNED_PAGING_PROBE";
    private const string M02OwnedGdtTssTerminalSymbol =
        "M02_OWNED_GDT_TSS_TERMINAL_PROBE";
    private const string M02IdtrContinuedExecutionSymbol =
        "M02_IDTR_CONTINUED_EXECUTION_PROBE";
    private const string M02Ud2VectorStateSymbol =
        "M02_UD2_VECTOR_STATE_PROBE";
    private const string M02PageFaultCr2ErrorCodeSymbol =
        "M02_PAGE_FAULT_CR2_ERROR_CODE_PROBE";
    private const string M02DoubleFaultIstSymbol = "M02_DOUBLE_FAULT_IST_PROBE";
    private const string M02UnexpectedVectorPanicSymbol = "M02_UNEXPECTED_VECTOR_PANIC_PROBE";
    private const string M04RhpNewFastSymbol = "M04_RHP_NEW_FAST_PROBE";
    private const string M04RhpBeforeBindSymbol = "M04_RHP_NEW_FAST_BEFORE_BIND_FAILURE_PROBE";
    private const string M04RhpExhaustionSymbol = "M04_RHP_NEW_FAST_EXHAUSTION_FAILURE_PROBE";
    private const string M04ArraySymbol = "M04_SZ_ARRAY_ALLOCATION_PROBE";
    private const string M04ArrayBeforeSymbol = "M04_SZ_ARRAY_ALLOCATION_BEFORE_BIND_PROBE";
    private const string M04ArrayExhaustionSymbol = "M04_SZ_ARRAY_ALLOCATION_EXHAUSTION_PROBE";
    private const string M04StringSymbol = "M04_STRING_ALLOCATION_PROBE";
    private const string M04StringBeforeSymbol = "M04_STRING_ALLOCATION_BEFORE_BIND_PROBE";
    private const string M04StringExhaustionSymbol = "M04_STRING_ALLOCATION_EXHAUSTION_PROBE";
    private const string M04StringInvalidLengthSymbol = "M04_STRING_ALLOCATION_INVALID_LENGTH_PROBE";
    private const string SerializerBinding =
        "global::OS.Boot.X64InstructionPublication";
    private const string SerializerCall =
        SerializerBinding + ".Serialize()";
    private const string ExpectedCookie =
        SerializerBinding + ".ExpectedSuccessCookie";
    private const string PublicationMask =
        "EfiEntry.s_m01InstructionPublicationEpochMask";
    private const string PublicationInitialized =
        "EfiEntry.s_m01InstructionPublicationInitialized";
    private const string CookieFailureWriter =
        "WriteM01InstructionPublicationCookieFailure";
    private const string EpochsVerifiedWriter =
        "WriteM01InstructionPublicationEpochsVerified";
    private const string CookieFailureMarker =
        "M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED";
    private const string EpochsVerifiedMarker =
        "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED";

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);
        Run(
            "m01.035 profile matrix binds exactly four direct calls",
            TestProfileMatrixAndDirectBinding,
            ref failures);
        Run(
            "m01.035 terminal M02 profile adds exactly one direct call",
            TestTerminalM02ProfileAddsOneDirectCall,
            ref failures);
        Run(
            "m01.035 shared state initializes once and survives switch",
            TestSharedStateInitializationAndSurvival,
            ref failures);
        Run(
            "m01.035 epoch one follows stack-probe publication",
            TestEpochOneStackProbeOrdering,
            ref failures);
        Run(
            "m01.035 epoch two dominates the one-way stack switch",
            TestEpochTwoStackSwitchOrdering,
            ref failures);
        Run(
            "m01.035 epoch three is inside architectural preflight",
            TestEpochThreeArchitecturalPreflightOrdering,
            ref failures);
        Run(
            "m01.035 epoch four and aggregate marker are exact",
            TestEpochFourAndAggregateMarkerOrdering,
            ref failures);
        Run(
            "m01.035 malformed and bypass mutations fail closed",
            TestMutationMatrix,
            ref failures);
        Run(
            "m01.035 issue marker and evidence boundary are unique",
            TestIssueMarkerUniqueness,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-035 instruction-publication source tests failed: "
                + failures);
        }

        Console.WriteLine(ReadyMarker);
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

    private static bool TestProfileMatrixAndDirectBinding()
    {
        string source = LoadEfiEntrySource();
        string profile000 = SelectProfile(
            source,
            stageOneEnabled: false,
            stageTwoEnabled: false,
            stageThreeEnabled: false,
            terminalEnabled: false);
        string profile100 = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: false,
            stageThreeEnabled: false,
            terminalEnabled: false);
        string profile110 = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: true,
            stageThreeEnabled: false,
            terminalEnabled: false);
        string profile111 = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: true,
            stageThreeEnabled: true,
            terminalEnabled: false);
        string pageFaultProfile = SelectProfile(
            source,
            stageOneEnabled: true,
            stageTwoEnabled: true,
            stageThreeEnabled: true,
            terminalEnabled: false,
            pageFaultCr2ErrorCodeEnabled: true);

        if (CountDirectSerializerCalls(profile000) != 0
            || CountDirectSerializerCalls(profile100) != 2
            || CountDirectSerializerCalls(profile110) != 2
            || CountDirectSerializerCalls(profile111) != 4
            || CountIdentifierOccurrences(
                profile000,
                "s_m01InstructionPublicationInitialized") != 0
            || CountIdentifierOccurrences(
                profile000,
                "s_m01InstructionPublicationEpochMask") != 0)
        {
            return Failure("profile-call-cardinality");
        }

        if (CountDirectSerializerCalls(pageFaultProfile) != 6
            || CountIdentifierOccurrences(
                profile111, "M02PageFaultAdapter") != 0
            || CountIdentifierOccurrences(
                pageFaultProfile, "M02PageFaultAdapter") != 2)
        {
            return Failure("page-fault-profile-isolation");
        }

        string df = SelectProfile(source, true, true, true, false, doubleFaultIstEnabled: true);
        if (CountDirectSerializerCalls(df) != 6
            || CountIdentifierOccurrences(df, "M02DoubleFaultIstAdapter") != 2
            || CountIdentifierOccurrences(profile111, "M02DoubleFaultIstAdapter") != 0
            || CountIdentifierOccurrences(pageFaultProfile, "M02DoubleFaultIstAdapter") != 0
            || CountIdentifierOccurrences(df, "M02PageFaultAdapter") != 0)
            return Failure("double-fault-publication-isolation");
        string dfBranches = "#if " + M02DoubleFaultIstSymbol + "\n" + SerializerCall + ";\n#else\nDisabled();\n#endif\n"
            + "#if UNRELATED_PROBE\nUnknown();\n#else\nUnknownElse();\n#endif\n";
        string dfOn = SelectProfile(dfBranches, true, true, true, false, doubleFaultIstEnabled: true);
        string dfOff = SelectProfile(dfBranches, true, true, true, false);
        string dfAlias = SelectProfile(dfBranches.Replace(M02DoubleFaultIstSymbol, M02DoubleFaultIstSymbol.ToLowerInvariant(), StringComparison.Ordinal), false, false, false, false);
        if (CountDirectSerializerCalls(dfOn) != 1 || CountDirectSerializerCalls(dfOff) != 0
            || dfOn.Contains("Disabled();", StringComparison.Ordinal) || !dfOff.Contains("Disabled();", StringComparison.Ordinal)
            || !dfOn.Contains("#if UNRELATED_PROBE", StringComparison.Ordinal) || !dfOn.Contains("UnknownElse();", StringComparison.Ordinal)
            || !dfAlias.Contains("#if m02_double_fault_ist_probe", StringComparison.Ordinal))
            return Failure("double-fault-selector-ordinal-and-unknown");

        string panic = SelectProfile(source, true, true, true, false, unexpectedVectorPanicEnabled: true);
        if (CountDirectSerializerCalls(panic) != 6 || CountIdentifierOccurrences(panic, "M02UnexpectedVectorPanicAdapter") != 2 ||
            CountIdentifierOccurrences(profile111, "M02UnexpectedVectorPanicAdapter") != 0 ||
            CountIdentifierOccurrences(panic, "M02DoubleFaultIstAdapter") != 0 || CountIdentifierOccurrences(panic, "M02PageFaultAdapter") != 0)
            return Failure("unexpected-vector-publication-isolation");
        string panicBranches = dfBranches.Replace(M02DoubleFaultIstSymbol, M02UnexpectedVectorPanicSymbol, StringComparison.Ordinal);
        string panicOn = SelectProfile(panicBranches, true, true, true, false, unexpectedVectorPanicEnabled: true);
        string panicOff = SelectProfile(panicBranches, true, true, true, false);
        string panicAlias = SelectProfile(panicBranches.Replace(M02UnexpectedVectorPanicSymbol, M02UnexpectedVectorPanicSymbol.ToLowerInvariant(), StringComparison.Ordinal), false, false, false, false);
        if (CountDirectSerializerCalls(panicOn) != 1 || CountDirectSerializerCalls(panicOff) != 0 ||
            panicOn.Contains("Disabled();", StringComparison.Ordinal) || !panicOff.Contains("Disabled();", StringComparison.Ordinal) ||
            !panicOn.Contains("#if UNRELATED_PROBE", StringComparison.Ordinal) || !panicOn.Contains("UnknownElse();", StringComparison.Ordinal) ||
            !panicAlias.Contains("#if m02_unexpected_vector_panic_probe", StringComparison.Ordinal))
            return Failure("unexpected-vector-selector-ordinal-and-unknown");

        if (!TestFixedAllocationProfileIsolation(source)
            || !TestFirstExitProfileIsolation(source)) return false;

        string sanitized = SanitizeCode(profile111);
        if (Regex.Matches(
                sanitized,
                @"(?<![A-Za-z0-9_])"
                    + @"global\s*::\s*OS\s*\.\s*Boot\s*\.\s*"
                    + @"X64InstructionPublication\s*\.\s*"
                    + @"Serialize\s*\(",
                RegexOptions.CultureInvariant).Count != 4
            || CountIdentifierOccurrences(
                sanitized,
                "X64InstructionPublication") != 8
            || sanitized.Contains(
                "FakeX64InstructionPublication",
                StringComparison.Ordinal)
            || sanitized.Contains(
                "SerializeFake",
                StringComparison.Ordinal)
            || sanitized.Contains(
                "RuntimeImport(",
                StringComparison.Ordinal)
            || sanitized.Contains(
                "53 B8 00 00",
                StringComparison.Ordinal))
        {
            return Failure("direct-binding-shape");
        }

        return ValidateContract(profile111, out string failure)
            || Failure("positive-contract=" + failure);
    }

    private static bool TestFirstExitProfileIsolation(string source)
    {
        string disabled = SelectProfile(source, true, true, false, false);
        string enabled = SelectProfile(source, true, true, false, false,
            firstExitEnabled: true);
        string adapter = FindTypeBody(enabled, "M03FirstExitAdapter") ?? string.Empty;
        string prepare = FindMethodBody(adapter, "Prepare") ?? string.Empty;
        string query = FindMethodBody(adapter, "Query") ?? string.Empty;
        // First exit retains the two stage-one publications and adds one each
        // for the architectural query and final retained code bundle.
        if (CountDirectSerializerCalls(disabled) != 2
            || CountIdentifierOccurrences(disabled, "M03FirstExitAdapter") != 0
            || CountDirectSerializerCalls(enabled) != 4
            || CountDirectExpectedCookieReferences(enabled) != 4
            || CountDirectSerializerCalls(adapter) != 2
            || CountDirectSerializerCalls(prepare) != 1
            || CountDirectExpectedCookieReferences(prepare) != 1
            || CountDirectSerializerCalls(query) != 1
            || CountDirectExpectedCookieReferences(query) != 1
            || FindTypeBody(enabled, "M01StageThreeAdapter") is not null)
            return Failure("first-exit-publication-isolation");

        string synthetic = "#if " + FirstExitSymbol + "\n" + SerializerCall
            + ";\n#else\nFirstExitOff();\n#endif\n"
            + "#if UNRELATED_PROBE\nUnknown();\n#else\nUnknownElse();\n#endif\n"
            + "#if M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE\nOwnedOnly();\n"
            + "#elif M03_FIRMWARE_ADAPTER_REVOCATION_PROBE\nRevocationOnly();\n"
            + "#else\nOwnedOff();\n#endif\n"
            + "#if M03_OWNED_SUBSYSTEM_PANIC_PROBE\nOwnedPanicOnly();\n#endif\n";
        string selected = SelectProfile(synthetic, true, true, false, false,
            firstExitEnabled: true);
        string unselected = SelectProfile(synthetic, true, true, false, false,
            stringInvalidLengthEnabled: true);
        string alias = SelectProfile(synthetic.Replace(FirstExitSymbol,
            FirstExitSymbol.ToLowerInvariant(), StringComparison.Ordinal), false, false, false, false);
        if (CountDirectSerializerCalls(selected) != 1
            || CountDirectSerializerCalls(unselected) != 0
            || selected.Contains("FirstExitOff();", StringComparison.Ordinal)
            || !unselected.Contains("FirstExitOff();", StringComparison.Ordinal)
            || !selected.Contains("#if UNRELATED_PROBE", StringComparison.Ordinal)
            || !selected.Contains("UnknownElse();", StringComparison.Ordinal)
            || !selected.Contains("OwnedOff();", StringComparison.Ordinal)
            || selected.Contains("OwnedOnly();", StringComparison.Ordinal)
            || selected.Contains("RevocationOnly();", StringComparison.Ordinal)
            || selected.Contains("OwnedPanicOnly();", StringComparison.Ordinal)
            || !alias.Contains("#if m03_first_real_exit_boot_services_probe", StringComparison.Ordinal))
            return Failure("first-exit-selector-ordinal-and-unknown");
        return true;
    }

    private static bool TestFixedAllocationProfileIsolation(string source)
    {
        string synthetic = "#if " + M04RhpNewFastSymbol + "\n" + SerializerCall + ";\n#endif\n"
            + "#if " + M04RhpBeforeBindSymbol + "\nBeforeBindOnly();\n#endif\n"
            + "#if " + M04RhpExhaustionSymbol + "\nExhaustionOnly();\n#endif\n"
            + "#if UNRELATED_PROBE\nUnknownBranch();\n#else\nUnknownElse();\n#endif\n";
        for (int old = 0; old < 7; old++)
        {
            string selected = SelectProfile(synthetic, old != 0, old >= 2, old >= 3, old == 4,
                idtrContinuedExecutionEnabled: old == 5, pageFaultCr2ErrorCodeEnabled: old == 6);
            if (CountDirectSerializerCalls(selected) != 0 || selected.Contains("BeforeBindOnly", StringComparison.Ordinal)
                || selected.Contains("ExhaustionOnly", StringComparison.Ordinal)
                || !selected.Contains("#if UNRELATED_PROBE", StringComparison.Ordinal)
                || !selected.Contains("UnknownElse", StringComparison.Ordinal)) return Failure("fixed-allocation-historical-selector");
        }
        for (int lane = 0; lane < 3; lane++)
        {
            string actual = SelectProfile(source, true, true, false, false, rhpNewFastEnabled: true,
                rhpBeforeBindEnabled: lane == 1, rhpExhaustionEnabled: lane == 2);
            string selected = SelectProfile(synthetic, true, true, false, false, rhpNewFastEnabled: true,
                rhpBeforeBindEnabled: lane == 1, rhpExhaustionEnabled: lane == 2);
            if (CountDirectSerializerCalls(actual) != 2 || CountDirectSerializerCalls(selected) != 1
                || selected.Contains("BeforeBindOnly", StringComparison.Ordinal) != (lane == 1)
                || selected.Contains("ExhaustionOnly", StringComparison.Ordinal) != (lane == 2)
                || !actual.Contains("RunRhpNewFastAfterZeroCheckpoint", StringComparison.Ordinal))
                return Failure("fixed-allocation-profile-selector");
        }
        string arraySynthetic = "#if " + M04ArraySymbol + "\nArrayMain();\n#endif\n"
            + "#if " + M04ArrayBeforeSymbol + "\nArrayBefore();\n#endif\n"
            + "#if " + M04ArrayExhaustionSymbol + "\nArrayExhaustion();\n#endif\n";
        for (int lane = 0; lane < 4; lane++)
        {
            bool enabled = lane != 0;
            string selected = SelectProfile(arraySynthetic, enabled, enabled, false, false,
                rhpNewFastEnabled: enabled, arrayEnabled: enabled,
                arrayBeforeBindEnabled: lane == 2, arrayExhaustionEnabled: lane == 3);
            string actual = SelectProfile(source, enabled, enabled, false, false,
                rhpNewFastEnabled: enabled, arrayEnabled: enabled,
                arrayBeforeBindEnabled: lane == 2, arrayExhaustionEnabled: lane == 3);
            if (selected.Contains("ArrayMain", StringComparison.Ordinal) != enabled
                || selected.Contains("ArrayBefore", StringComparison.Ordinal) != (lane == 2)
                || selected.Contains("ArrayExhaustion", StringComparison.Ordinal) != (lane == 3)
                || CountDirectSerializerCalls(selected) != 0
                || CountDirectSerializerCalls(actual) != (enabled ? 2 : 0)) return Failure("array-profile-publication-selector");
        }
        string memorySynthetic = "#if " + MemoryMapSymbol + "\nMemoryOnly();\n#else\nMemoryOff();\n#endif\n";
        string memorySelected = SelectProfile(memorySynthetic, false, false, false, false);
        string memoryAlias = SelectProfile(memorySynthetic.Replace(MemoryMapSymbol, MemoryMapSymbol.ToLowerInvariant(), StringComparison.Ordinal), false, false, false, false);
        if (memorySelected.Contains("MemoryOnly();", StringComparison.Ordinal) || !memorySelected.Contains("MemoryOff();", StringComparison.Ordinal)
            || !memoryAlias.Contains("#if m03_memory_map_capture_probe", StringComparison.Ordinal) || CountDirectSerializerCalls(memorySelected) != 0)
            return Failure("memory-map-selector-ordinal-publication");
        string stringSynthetic = "#if " + M04StringSymbol + "\nStringMain();\n#endif\n"
            + "#if " + M04StringBeforeSymbol + "\nStringBefore();\n#endif\n"
            + "#if " + M04StringExhaustionSymbol + "\nStringExhaustion();\n#endif\n"
            + "#if " + M04StringInvalidLengthSymbol + "\nStringInvalidLength();\n#endif\n";
        for (int lane = 0; lane < 5; lane++)
        {
            bool enabled = lane != 0;
            string selected = SelectProfile(stringSynthetic, enabled, enabled, false, false,
                rhpNewFastEnabled: enabled, arrayEnabled: enabled, stringEnabled: enabled,
                stringBeforeBindEnabled: lane == 2, stringExhaustionEnabled: lane == 3, stringInvalidLengthEnabled: lane == 4);
            string actual = SelectProfile(source, enabled, enabled, false, false,
                rhpNewFastEnabled: enabled, arrayEnabled: enabled, stringEnabled: enabled,
                stringBeforeBindEnabled: lane == 2, stringExhaustionEnabled: lane == 3, stringInvalidLengthEnabled: lane == 4);
            if (selected.Contains("StringMain", StringComparison.Ordinal) != enabled
                || selected.Contains("StringBefore", StringComparison.Ordinal) != (lane == 2)
                || selected.Contains("StringExhaustion", StringComparison.Ordinal) != (lane == 3)
                || selected.Contains("StringInvalidLength", StringComparison.Ordinal) != (lane == 4)
                || CountDirectSerializerCalls(selected) != 0
                || CountDirectSerializerCalls(actual) != (enabled ? 2 : 0)) return Failure("string-profile-publication-selector");
        }
        string stringAlias = SelectProfile(stringSynthetic.Replace(M04StringSymbol, M04StringSymbol.ToLowerInvariant(), StringComparison.Ordinal), false, false, false, false);
        if (!stringAlias.Contains("#if m04_string_allocation_probe", StringComparison.Ordinal)) return Failure("string-selector-ordinal");
        string arrayAlias = SelectProfile(arraySynthetic.Replace(M04ArraySymbol, M04ArraySymbol.ToLowerInvariant(), StringComparison.Ordinal), false, false, false, false);
        if (!arrayAlias.Contains("#if m04_sz_array_allocation_probe", StringComparison.Ordinal)) return Failure("array-selector-ordinal");
        string alias = SelectProfile(synthetic.Replace(M04RhpNewFastSymbol, M04RhpNewFastSymbol.ToLowerInvariant(), StringComparison.Ordinal),
            false, false, false, false);
        return alias.Contains("#if m04_rhp_new_fast_probe", StringComparison.Ordinal)
            || Failure("fixed-allocation-selector-must-be-ordinal");
    }

    private static bool TestTerminalM02ProfileAddsOneDirectCall()
    {
        string historicalProfile = LoadAllSwitchProfile();
        string terminalProfile = LoadM02TerminalProfile();
        string failure = string.Empty;
        if (CountDirectSerializerCalls(historicalProfile) != 4
            || CountDirectSerializerCalls(terminalProfile) != 5
            || !ValidateM02TerminalProfile(
                terminalProfile,
                out failure))
        {
            return Failure(
                "terminal-positive-contract=" + failure);
        }

        MutationCase[] mutations =
        [
            new(
                "terminal serializer call missing",
                "terminal-binding-total",
                value => ReplaceInMethod(
                    value,
                    "EfiEntry",
                    "EnterM02OwnedGdtTssTerminalProbe",
                    "X64InstructionPublication.Serialize()",
                    "X64InstructionPublication.ExpectedSuccessCookie")),
            new(
                "terminal serializer result bypassed",
                "terminal-preentry-guard",
                value => ReplaceInMethod(
                    value,
                    "EfiEntry",
                    "EnterM02OwnedGdtTssTerminalProbe",
                    "X64InstructionPublication.Serialize() !=",
                    "X64InstructionPublication.Serialize() ==")),
            new(
                "terminal serializer call duplicated",
                "terminal-binding-total",
                value => ReplaceInMethod(
                    value,
                    "EfiEntry",
                    "EnterM02OwnedGdtTssTerminalProbe",
                    "X64InstructionPublication.Serialize() !=",
                    "X64InstructionPublication.Serialize() == "
                        + "X64InstructionPublication.ExpectedSuccessCookie || "
                        + "X64InstructionPublication.Serialize() !="))
        ];

        foreach (MutationCase mutation in mutations)
        {
            string mutated = mutation.Apply(terminalProfile);
            if (ValidateM02TerminalProfile(
                    mutated,
                    out string mutationFailure)
                || !mutationFailure.Contains(
                    mutation.ExpectedFailure,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "terminal-mutation="
                    + mutation.Name
                    + " failure="
                    + mutationFailure);
            }
        }

        return true;
    }

    private static bool TestSharedStateInitializationAndSurvival()
    {
        string profile = LoadAllSwitchProfile();
        string sanitized = SanitizeCode(profile);
        if (CountTopLevelFieldDeclarations(
                profile,
                "s_m01InstructionPublicationInitialized") != 1
            || CountTopLevelFieldDeclarations(
                profile,
                "s_m01InstructionPublicationEpochMask") != 1
            || CountIdentifierOccurrences(
                sanitized,
                "s_m01InstructionPublicationInitialized") != 3
            || CountIdentifierOccurrences(
                sanitized,
                "s_m01InstructionPublicationEpochMask") != 14)
        {
            return Failure("state-field-shape");
        }

        string stageOne =
            FindTypeBody(profile, "M01StageOneAdapter")
            ?? string.Empty;
        string run =
            FindMethodBody(stageOne, "Run")
            ?? string.Empty;
        string continuation =
            FindMethodBody(stageOne, "M01StageOneContinuation")
            ?? string.Empty;
        string runCompact = Compact(run);
        string continuationCompact = Compact(continuation);

        bool initializedInOrder = RequireOrdered(
            runCompact,
            "EfiBootServices*bootServices=systemTable->BootServices;",
            "s_m01InstructionPublicationInitialized!=0",
            "s_m01InstructionPublicationEpochMask!=0",
            "s_m01InstructionPublicationEpochMask=0;",
            "s_m01InstructionPublicationInitialized=1;",
            "TryCaptureLoadedImage(");
        string initializationCondition =
            PublicationInitialized
                + "!=0||"
                + PublicationMask
                + "!=0";
        if (!initializedInOrder
            || CountStateMutations(
                profile,
                "s_m01InstructionPublicationInitialized") != 1
            || CountAssignments(
                profile,
                "s_m01InstructionPublicationInitialized",
                "1") != 1
            || !TryFindExactIfBlock(
                run,
                initializationCondition,
                out SourceRegion initializationFailure,
                out int initializationEnd)
            || !ValidateFailureBlock(
                run[initializationFailure.ContentStart
                    ..initializationFailure.ContentEnd],
                CookieFailureWriter + "(console,serial);",
                "returnFailBeforeTransfer(",
                "InstructionPublicationRejected")
            || !Compact(run[initializationEnd..]).StartsWith(
                PublicationMask
                    + "=0;"
                    + PublicationInitialized
                    + "=1;M01StageOneAdapterStatuscaptureStatus="
                    + "TryCaptureLoadedImage(",
                StringComparison.Ordinal)
            || !continuationCompact.Contains(
                "s_m01InstructionPublicationEpochMask!=3",
                StringComparison.Ordinal)
            || continuationCompact.IndexOf(
                    "s_m01InstructionPublicationEpochMask!=3",
                    StringComparison.Ordinal)
                > continuationCompact.IndexOf(
                    "ObserveContinuationRspFromStaticState(",
                    StringComparison.Ordinal)
            || CountAnyAssignments(
                continuation,
                "s_m01InstructionPublicationEpochMask") != 0)
        {
            return Failure("state-initialize-or-survive");
        }

        string reject =
            FindMethodBody(
                FindTypeBody(profile, "M01StageThreeAdapter")
                    ?? string.Empty,
                "RejectInstructionPublicationBeforeEntry")
            ?? string.Empty;
        return ValidateFailureBlock(
                reject,
                CookieFailureWriter + "(console,serial);",
                "returnFailBeforeEntry(",
                "InstructionPublicationRejected")
            || Failure("failure-marker-before-cleanup");
    }

    private static bool TestEpochOneStackProbeOrdering()
    {
        string stageOne =
            FindTypeBody(
                LoadAllSwitchProfile(),
                "M01StageOneAdapter")
            ?? string.Empty;
        string run =
            FindMethodBody(stageOne, "Run")
            ?? string.Empty;
        return ValidateEpochOne(run, out string failure)
            || Failure(failure);
    }

    private static bool TestEpochTwoStackSwitchOrdering()
    {
        string stageOne =
            FindTypeBody(
                LoadAllSwitchProfile(),
                "M01StageOneAdapter")
            ?? string.Empty;
        string prepare =
            FindMethodBody(stageOne, "PrepareAndTransfer")
            ?? string.Empty;
        string continuation =
            FindMethodBody(stageOne, "M01StageOneContinuation")
            ?? string.Empty;
        return ValidateEpochTwo(
                prepare,
                continuation,
                out string failure)
            || Failure(failure);
    }

    private static bool TestEpochThreeArchitecturalPreflightOrdering()
    {
        string stageThree =
            FindTypeBody(
                LoadAllSwitchProfile(),
                "M01StageThreeAdapter")
            ?? string.Empty;
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string capture =
            FindMethodBody(
                stageThree,
                "CaptureArchitecturalPreflight")
            ?? string.Empty;
        return ValidateEpochThree(
                run,
                capture,
                out string failure)
            || Failure(failure);
    }

    private static bool TestEpochFourAndAggregateMarkerOrdering()
    {
        string profile = LoadAllSwitchProfile();
        string stageThree =
            FindTypeBody(profile, "M01StageThreeAdapter")
            ?? string.Empty;
        string run =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        if (!ValidateEpochFour(run, out string failure))
        {
            return Failure(failure);
        }

        string cookieWriter =
            FindTopLevelMethodBody(
                profile,
                "EfiEntry",
                CookieFailureWriter)
            ?? string.Empty;
        string verifiedWriter =
            FindTopLevelMethodBody(
                profile,
                "EfiEntry",
                EpochsVerifiedWriter)
            ?? string.Empty;
        if (!TryDecodeMarkerWriter(
                cookieWriter,
                out string cookieMarker)
            || !TryDecodeMarkerWriter(
                verifiedWriter,
                out string verifiedMarker)
            || !string.Equals(
                cookieMarker,
                CookieFailureMarker,
                StringComparison.Ordinal)
            || !string.Equals(
                verifiedMarker,
                EpochsVerifiedMarker,
                StringComparison.Ordinal)
            || !string.Equals(
                ReadStageThreeLine(8),
                "M01_PRE_CR3",
                StringComparison.Ordinal)
            || !string.Equals(
                ReadStageThreeLine(13),
                "M01_POST_CR3",
                StringComparison.Ordinal))
        {
            return Failure("marker-bytes-or-boot-lines");
        }

        return true;
    }

    private static bool TestMutationMatrix()
    {
        string source = LoadAllSwitchProfile();
        List<MutationCase> mutations =
        [
            new(
                "epoch one missing direct call",
                "epoch=1",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    SerializerCall,
                    ExpectedCookie)),
            new(
                "epoch one duplicate direct call",
                "epoch=1",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    SerializerCall,
                    SerializerCall + " + " + SerializerCall)),
            new(
                "epoch one branch-only call",
                "epoch=1",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    SerializerCall,
                    "(codeAllocated ? "
                        + SerializerCall
                        + " : "
                        + ExpectedCookie
                        + ")")),
            new(
                "epoch one ignores the return",
                "epoch=1",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "uint epochOnePublicationCookie =\n"
                        + "                "
                        + SerializerCall,
                    "_ = "
                        + SerializerCall
                        + ";\n            "
                        + "uint epochOnePublicationCookie =\n"
                        + "                0u")),
            new(
                "epoch one compares a synthetic constant",
                "epoch=1",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    ".ExpectedSuccessCookie",
                    ".ExpectedSuccessCookie + 1")),
            new(
                "epoch one presets the transition",
                "epoch=1",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "uint epochOnePublicationCookie =",
                    PublicationMask + " = 1; ")),
            new(
                "state omits the one-shot initialize flag",
                "state",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "EfiEntry.s_m01InstructionPublicationInitialized = 1;",
                    string.Empty)),
            new(
                "state duplicates the reset",
                "state",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    PublicationMask + " = 0;",
                    PublicationMask + " = 0; "
                        + PublicationMask + " = 0;")),
            new(
                "state seeds stale publication",
                "epoch=1",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    PublicationMask + " = 0;",
                    PublicationMask + " = 1;")),
            new(
                "state weakens stale guard to conjunction",
                "state",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "s_m01InstructionPublicationInitialized != 0\n"
                        + "                || EfiEntry",
                    "s_m01InstructionPublicationInitialized != 0\n"
                        + "                && EfiEntry")),
            new(
                "state resets initialization authority",
                "state",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "M01StageOneAdapterStatus captureStatus =",
                    PublicationInitialized + " = 0; ")),
            new(
                "state fields move under nested owner",
                "state",
                value => ReplaceExactlyOnce(
                    value,
                    "private static byte "
                        + "s_m01InstructionPublicationInitialized;",
                    "private static class NestedPublicationState "
                        + "{ private static byte "
                        + "s_m01InstructionPublicationInitialized; }")),
            new(
                "serializer binding is shadowed by nested type",
                "epoch=1",
                value => ReplaceExactlyOnce(
                    value,
                    "private static class M01StageOneAdapter",
                    "private static class X64InstructionPublication { }\n"
                        + "    private static class "
                        + "M01StageOneAdapter")),
            new(
                "epoch one cookie failure moves to dead branch",
                "epoch=1",
                value => MoveFailureBodyToDeadBranchInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "epochOnePublicationCookie!="
                        + ExpectedCookie)),
            new(
                "epoch one failure marker alone moves to dead branch",
                "epoch=1",
                value => MoveFailureMarkerToDeadBranchInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "epochOnePublicationCookie!="
                        + ExpectedCookie)),
            new(
                "epoch one failure marker has no-brace dead guard",
                "epoch=1",
                value => MoveFailureMarkerToDeadBranchInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "epochOnePublicationCookie!="
                        + ExpectedCookie,
                    useBraces: false)),
            new(
                "epoch one stops before failure marker",
                "epoch=1",
                value => InsertAtExactIfBodyStartInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "epochOnePublicationCookie!="
                        + ExpectedCookie,
                    "Spin(); ")),
            new(
                "epoch one callback follows cookie branch",
                "epoch=1",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    PublicationMask + " = 1;",
                    "EfiEntry.Write(console, serial, (byte)'X'); ")),
            new(
                "epoch one probe readback is bypassed",
                "epoch=1",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "MaterializeAndInstallStackProbe",
                    "if (probe[index]\n"
                        + "                    != GetStackProbeByte(index))",
                    "if (false)")),
            new(
                "epoch one probe changes after readback",
                "epoch=1",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "MaterializeAndInstallStackProbe",
                    "return System.Runtime.X64StackProbeRuntime",
                    "probe[0] = 0;\n\n"
                        + "            ")),
            new(
                "epoch one probe executes after readback",
                "epoch=1",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "MaterializeAndInstallStackProbe",
                    "return System.Runtime.X64StackProbeRuntime",
                    "((delegate* unmanaged<void>)(void*)probe)();"
                        + "\n\n            ")),
            new(
                "epoch one writes after final probe readback",
                "epoch=1",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "if (stackProbeStatus\n"
                        + "                != "
                        + "M01StageOneAdapterStatus.Success)",
                    "((byte*)codeAddress)[0] = 0;\n\n"
                        + "            ")),
            new(
                "epoch one executes after final probe readback",
                "epoch=1",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "Run",
                    "if (stackProbeStatus\n"
                        + "                != "
                        + "M01StageOneAdapterStatus.Success)",
                    "((delegate* unmanaged<void>)0)();\n\n"
                        + "            ")),
            new(
                "epoch two missing direct call",
                "epoch=2",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "PrepareAndTransfer",
                    SerializerCall,
                    ExpectedCookie)),
            new(
                "epoch two duplicate direct call",
                "epoch=2",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "PrepareAndTransfer",
                    SerializerCall,
                    SerializerCall + " + " + SerializerCall)),
            new(
                "epoch two wrong cookie",
                "epoch=2",
                value => ReplaceInMethod(
                    value,
                    "M01StageOneAdapter",
                    "PrepareAndTransfer",
                    ".ExpectedSuccessCookie",
                    ".ExpectedSuccessCookie + 1")),
            new(
                "epoch two transitions before serialization",
                "epoch=2",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "PrepareAndTransfer",
                    "uint epochTwoPublicationCookie =",
                    PublicationMask + " = 3; ")),
            new(
                "epoch two cookie failure moves to dead branch",
                "epoch=2",
                value => MoveFailureBodyToDeadBranchInMethod(
                    value,
                    "M01StageOneAdapter",
                    "PrepareAndTransfer",
                    "epochTwoPublicationCookie!="
                        + ExpectedCookie)),
            new(
                "epoch two callback follows cookie branch",
                "epoch=2",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "PrepareAndTransfer",
                    PublicationMask + " = 3;",
                    "EfiEntry.Write(console, serial, (byte)'X'); ")),
            new(
                "continuation reconstructs publication state",
                "epoch=2",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "M01StageOneContinuation",
                    "VirtualAddress observedRspAddress",
                    PublicationMask + " = 3; ")),
            new(
                "continuation invokes a function pointer before guard",
                "epoch=2",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "M01StageOneContinuation",
                    "if (EfiEntry\n"
                        + "                        "
                        + ".s_m01InstructionPublicationEpochMask\n"
                        + "                    != 3)",
                    "((delegate* unmanaged<void>)0)();\n"
                        + "                ")),
            new(
                "epoch two writes after final switch readback",
                "epoch=2",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "PrepareAndTransfer",
                    "coordinatorStatus =\n"
                        + "                    (ulong)prepared.Status;",
                    "((byte*)codeAddress)[0] = 0;\n"
                        + "                ")),
            new(
                "epoch two executes after final switch readback",
                "epoch=2",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageOneAdapter",
                    "PrepareAndTransfer",
                    "coordinatorStatus =\n"
                        + "                    (ulong)prepared.Status;",
                    "((delegate* unmanaged<void>)0)();\n"
                        + "                ")),
            new(
                "epoch three caller-side barrier is too late",
                "epoch=3",
                MoveEpochThreeCallToCaller),
            new(
                "epoch three caller corrupts a prior thunk",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "out plannerCallEntry)\n"
                        + "                    || "
                        + "!CaptureArchitecturalPreflight(",
                    "out plannerCallEntry)\n"
                        + "                    || "
                        + "(((byte*)plannerCallEntry)[0] = 0) != 0\n"
                        + "                    || "
                        + "!CaptureArchitecturalPreflight(")),
            new(
                "epoch three caller executes a prior thunk",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "out plannerCallEntry)\n"
                        + "                    || "
                        + "!CaptureArchitecturalPreflight(",
                    "out plannerCallEntry)\n"
                        + "                    || "
                        + "((delegate* unmanaged<uint>)"
                        + "plannerCallEntry)() == 0\n"
                        + "                    || "
                        + "!CaptureArchitecturalPreflight(")),
            new(
                "epoch three transfer readback is bypassed",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "MaterializeTransferThunk",
                    "if (destination[index]\n"
                        + "                    != GetTransferThunkByte(index))",
                    "if (false)")),
            new(
                "epoch three planner readback is bypassed",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "MaterializePlannerCallThunk",
                    "if (destination[index]\n"
                        + "                    != GetPlannerCallThunkByte(index))",
                    "if (false)")),
            new(
                "epoch three transfer thunk changes after readback",
                "epoch=3",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "MaterializeTransferThunk",
                    "entryAddress =\n"
                        + "                "
                        + "stageOneCodeAllocation.VirtualStart",
                    "destination[0] = 0;\n\n"
                        + "            ")),
            new(
                "epoch three planner thunk changes after readback",
                "epoch=3",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "MaterializePlannerCallThunk",
                    "entryAddress =\n"
                        + "                "
                        + "stageOneCodeAllocation.VirtualStart",
                    "destination[0] = 0;\n\n"
                        + "            ")),
            new(
                "epoch three call follows first execution",
                "epoch=3",
                MoveEpochThreeCallAfterPreflight),
            new(
                "epoch three executes preflight before publication guard",
                "epoch=3",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    "if (EfiEntry\n"
                        + "                    "
                        + ".s_m01InstructionPublicationEpochMask\n"
                        + "                != 3)",
                    "preflight(observation); ")),
            new(
                "epoch three rewrites preflight after validation",
                "epoch=3",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    "byte* patReader =",
                    "codePage[0] = 0;\n\n"
                        + "            ")),
            new(
                "epoch three executes preflight after validation",
                "epoch=3",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    "byte* patReader =",
                    "((delegate* unmanaged<void>)"
                        + "(void*)codeAddress)();\n\n"
                        + "            ")),
            new(
                "epoch three branch-only call",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "(codeAddress != 0 ? "
                        + SerializerCall
                        + " : "
                        + ExpectedCookie
                        + ")")),
            new(
                "epoch three cookie failure moves to dead branch",
                "epoch=3",
                value => MoveFailureBodyToDeadBranchInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    "epochThreePublicationCookie!="
                        + ExpectedCookie)),
            new(
                "epoch three callback precedes mask transition",
                "epoch=3",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    PublicationMask + " = 7;",
                    "EfiEntry.Write(console, serial, (byte)'X'); ")),
            new(
                "epoch three stops after flag and before failure marker",
                "epoch=3",
                value => ReplaceInExactIfBodyInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    "epochThreePublicationCookie!="
                        + ExpectedCookie,
                    "instructionPublicationRejected = true;",
                    "instructionPublicationRejected = true; Spin();")),
            new(
                "epoch three redirects failure marker",
                "epoch=3",
                value => ReplaceInExactIfBodyInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    "epochThreePublicationCookie!="
                        + ExpectedCookie,
                    "EfiEntry\n"
                        + "                    ."
                        + CookieFailureWriter,
                    "Noop." + CookieFailureWriter)),
            new(
                "epoch three writes after final thunk readback",
                "epoch=3",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    "delegate* unmanaged<\n"
                        + "                OS.Kernel.Memory\n"
                        + "                    "
                        + ".X64PagingModePreflightObservation*,\n"
                        + "                void> preflight =",
                    "patReader[0] = 0;\n\n"
                        + "            ")),
            new(
                "epoch three executes after final readback",
                "epoch=3",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    "delegate* unmanaged<\n"
                        + "                OS.Kernel.Memory\n"
                        + "                    "
                        + ".X64PagingModePreflightObservation*,\n"
                        + "                void> preflight =",
                    "((delegate* unmanaged<void>)0)();\n\n"
                        + "            ")),
            new(
                "epoch three uses wrong binding",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "FakeX64InstructionPublication.Serialize()")),
            new(
                "epoch three substitutes published CPUID bytes",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "GetPatReaderByte(13)")),
            new(
                "epoch three substitutes LFENCE",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "RunLfenceSubstitute()")),
            new(
                "epoch three substitutes MFENCE",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "RunMfenceSubstitute()")),
            new(
                "epoch three substitutes volatile access",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "ReadVolatilePublicationState()")),
            new(
                "epoch three substitutes lock-prefixed access",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "RunLockPrefixedSubstitute()")),
            new(
                "epoch three dynamically generates a barrier",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "GeneratePublicationBarrierBytes()")),
            new(
                "epoch three substitutes ordinary managed call",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "GetPatReaderByte(0)")),
            new(
                "epoch three substitutes firmware callback",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "InvokeFirmwareCallbackAsBarrier()")),
            new(
                "epoch three substitutes no-op default value",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    SerializerCall,
                    "0u")),
            new(
                "epoch three writes after publication",
                "epoch=3",
                value => InsertAfterInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    PublicationMask + " = 7;",
                    " codePage[0] = 0;")),
            new(
                "epoch three makes unrelated managed call",
                "epoch=3",
                value => InsertAfterInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    PublicationMask + " = 7;",
                    " _ = GetPatReaderByte(0);")),
            new(
                "epoch three makes firmware callback",
                "epoch=3",
                value => InsertAfterInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    PublicationMask + " = 7;",
                    " EfiEntry.Write(console, serial, (byte)'X');")),
            new(
                "epoch three binds zero preflight",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    "void>)(void*)codeAddress;",
                    "void>)(void*)0;")),
            new(
                "epoch three regresses transition",
                "epoch=3",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "CaptureArchitecturalPreflight",
                    PublicationMask + " = 7;",
                    PublicationMask + " = 3;")),
            new(
                "epoch four missing direct call",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    SerializerCall,
                    ExpectedCookie)),
            new(
                "epoch four duplicate direct call",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    SerializerCall,
                    SerializerCall + " + " + SerializerCall)),
            new(
                "epoch four serializes before M01_PRE_CR3",
                "epoch=4",
                MoveEpochFourCallBeforePreMarker),
            new(
                "epoch four serializes after transfer",
                "epoch=4",
                MoveEpochFourCallAfterTransfer),
            new(
                "epoch four enters transfer before publication guard",
                "epoch=4",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "if (EfiEntry\n"
                        + "                        "
                        + ".s_m01InstructionPublicationEpochMask\n"
                        + "                    != 7)",
                    "_ = transfer(runtimeState, gateObservation, "
                        + "s_gateReadback.GateEntryAddress, "
                        + "s_preparation.State.OriginalRflags); ")),
            new(
                "epoch four wrong cookie",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    ".ExpectedSuccessCookie",
                    ".ExpectedSuccessCookie + 1")),
            new(
                "epoch four probe final readback is omitted",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "!ValidateProtectionProbeReadback(",
                    "false && ValidateProtectionProbeReadback(")),
            new(
                "epoch four protection probe changes after readback",
                "epoch=4",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "ValidateProtectionProbeReadback",
                    "return true;",
                    "probePage[0] = 0;\n\n"
                        + "            ")),
            new(
                "epoch four transfer thunk changes after readback",
                "epoch=4",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "ValidateTransferThunkReadback",
                    "return true;",
                    "source[0] = 0;\n\n"
                        + "            ")),
            new(
                "epoch four planner thunk changes after readback",
                "epoch=4",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "ValidatePlannerCallThunkReadback",
                    "return true;",
                    "source[0] = 0;\n\n"
                        + "            ")),
            new(
                "epoch four cookie failure moves to dead branch",
                "epoch=4",
                value => MoveFailureBodyToDeadBranchInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "epochFourPublicationCookie!="
                        + ExpectedCookie)),
            new(
                "epoch four callback precedes mask transition",
                "epoch=4",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    PublicationMask + " = 15;",
                    "EfiEntry.Write(console, serial, (byte)'X'); ")),
            new(
                "epoch four cleanup helper conditionally suppresses marker",
                "epoch=4",
                value => GuardTopLevelFailureMarkerInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RejectInstructionPublicationBeforeEntry")),
            new(
                "epoch four writes after final binding",
                "epoch=4",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "if (!s_gateReadback.IsReadbackVerified",
                    "((byte*)codeAddress)[0] = 0;\n\n"
                        + "                ")),
            new(
                "epoch four executes after final readback",
                "epoch=4",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "if (!s_gateReadback.IsReadbackVerified",
                    "((delegate* unmanaged<void>)0)();\n\n"
                        + "                ")),
            new(
                "epoch four writes after publication",
                "epoch=4",
                value => InsertAfterInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    PublicationMask + " = 15;",
                    " ((byte*)codeAddress)[0] = 0;")),
            new(
                "epoch four rebinds after publication",
                "epoch=4",
                value => InsertAfterInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    PublicationMask + " = 15;",
                    " transfer = default;")),
            new(
                "epoch four makes firmware callback",
                "epoch=4",
                value => InsertAfterInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    PublicationMask + " = 15;",
                    " EfiEntry.Write(console, serial, (byte)'X');")),
            new(
                "epoch four accepts partial mask",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    PublicationMask + " = 15;",
                    PublicationMask + " = 7;")),
            new(
                "aggregate marker accepts partial state",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "s_m01InstructionPublicationEpochMask\n"
                        + "                            != 15",
                    "s_m01InstructionPublicationEpochMask\n"
                        + "                            != 7")),
            new(
                "aggregate marker is omitted",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    EpochsVerifiedWriter,
                    "WriteSuccessLine")),
            new(
                "post-return audit failure is ignored",
                "epoch=4",
                value => SetExactIfBodyInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "!s_gateAudit.IsObservedAudit",
                    string.Empty)),
            new(
                "aggregate marker is emitted early",
                "epoch=4",
                value => InsertBeforeInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    "fixed (M01OwnedCr3CompositeCallGateResult*\n"
                        + "                    gateReadback = "
                        + "&s_gateReadback)",
                    "EfiEntry."
                        + EpochsVerifiedWriter
                        + "(console, serial); ")),
            new(
                "aggregate marker is not immediately pre-post-cr3",
                "epoch=4",
                value => InsertAfterInMethod(
                    value,
                    "M01StageThreeAdapter",
                    "RunAfterStageTwo",
                    ".WriteM01InstructionPublicationEpochsVerified(\n"
                        + "                                console,\n"
                        + "                                serial);",
                    " EfiEntry.Write(console, serial, (byte)'X');")),
            new(
                "failure marker byte is comment-spoofed",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "EfiEntry",
                    CookieFailureWriter,
                    "(byte)'D',\n            0,",
                    "0 /* (byte)'D' */,\n            0,")),
            new(
                "failure marker byte is expression-spoofed",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "EfiEntry",
                    CookieFailureWriter,
                    "(byte)'D',\n            0,",
                    "(byte)(0 * (byte)'D'),\n            0,")),
            new(
                "failure marker byte group is conditionally suppressed",
                "epoch=4",
                value => GuardFirstCallWithNoBraceIfInMethod(
                    value,
                    "EfiEntry",
                    CookieFailureWriter,
                    "WriteM01InstructionPublicationAscii")),
            new(
                "failure marker middle byte group is redirected",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "EfiEntry",
                    CookieFailureWriter,
                    "WriteM01InstructionPublicationAscii(\n"
                        + "            console,\n"
                        + "            serial,\n"
                        + "            (byte)'R',",
                    "Noop.WriteM01InstructionPublicationAscii(\n"
                        + "            console,\n"
                        + "            serial,\n"
                        + "            (byte)'R',")),
            new(
                "failure marker newline is conditionally suppressed",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "EfiEntry",
                    CookieFailureWriter,
                    "WriteCrLf(console, serial);",
                    "if (false) WriteCrLf(console, serial);")),
            new(
                "failure marker newline is redirected",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "EfiEntry",
                    CookieFailureWriter,
                    "WriteCrLf(console, serial);",
                    "Noop.WriteCrLf(console, serial);")),
            new(
                "marker forwarder drops final byte slot",
                "epoch=4",
                value => ReplaceInMethod(
                    value,
                    "EfiEntry",
                    "WriteM01InstructionPublicationAscii",
                    "if (b7 != 0) Write(console, serial, b7);",
                    "if (false) Write(console, serial, b7);"))
        ];

        foreach (MutationCase mutation in mutations)
        {
            string mutated;
            try
            {
                mutated = mutation.Apply(source);
            }
            catch (Exception ex)
            {
                return Failure(
                    "mutation-anchor="
                    + mutation.Name
                    + " "
                    + ex.Message);
            }
            if (string.Equals(mutated, source, StringComparison.Ordinal))
            {
                return Failure(
                    "mutation-did-not-change=" + mutation.Name);
            }

            if (ValidateContract(mutated, out string failure)
                || !failure.Contains(
                    mutation.ExpectedFailure,
                    StringComparison.Ordinal))
            {
                return Failure(
                    "mutation="
                    + mutation.Name
                    + " failure="
                    + failure);
            }
        }

        return true;
    }

    private static bool TestIssueMarkerUniqueness()
    {
        string repositoryRoot =
            HostToolProcess.FindRepositoryRoot();
        string testsDirectory = Path.Combine(
            repositoryRoot,
            "tests",
            "OS.HostTests");
        int readyMarkerCount = 0;
        foreach (string path in Directory.EnumerateFiles(
            testsDirectory,
            "*.cs",
            SearchOption.TopDirectoryOnly))
        {
            readyMarkerCount += Regex.Matches(
                File.ReadAllText(path),
                Regex.Escape(ReadyMarker),
                RegexOptions.CultureInvariant).Count;
        }

        string source = LoadEfiEntrySource();
        string defaultProfile = SelectProfile(
            source,
            stageOneEnabled: false,
            stageTwoEnabled: false,
            stageThreeEnabled: false,
            terminalEnabled: false);
        string allProfile = LoadAllSwitchProfile();
        string compact = Compact(allProfile);
        string[] failClosedTokens =
        [
            "RepeatedUse",
            "prepared=default;",
            "transfer=default;",
            "TryValidateAllocation(",
            ">~0UL-",
            "ReadbackRejected",
            "TryClearStackProbeBytes(",
            "InstructionPublicationRejected",
            "Spin();"
        ];
        return (readyMarkerCount == 1
                && CountDirectSerializerCalls(defaultProfile) == 0
                && failClosedTokens.All(token =>
                    compact.Contains(token, StringComparison.Ordinal))
                && !allProfile.Contains(
                    CookieFailureMarker,
                    StringComparison.Ordinal)
                && !allProfile.Contains(
                    EpochsVerifiedMarker,
                    StringComparison.Ordinal))
            || Failure("marker-unique-or-fail-closed");
    }

    private static bool ValidateContract(
        string source,
        out string failure)
    {
        failure = string.Empty;
        string stageOne =
            FindTypeBody(source, "M01StageOneAdapter")
            ?? string.Empty;
        string stageThree =
            FindTypeBody(source, "M01StageThreeAdapter")
            ?? string.Empty;
        string runOne =
            FindMethodBody(stageOne, "Run")
            ?? string.Empty;
        string prepare =
            FindMethodBody(stageOne, "PrepareAndTransfer")
            ?? string.Empty;
        string continuation =
            FindMethodBody(stageOne, "M01StageOneContinuation")
            ?? string.Empty;
        string runThree =
            FindMethodBody(stageThree, "RunAfterStageTwo")
            ?? string.Empty;
        string capture =
            FindMethodBody(
                stageThree,
                "CaptureArchitecturalPreflight")
            ?? string.Empty;
        if (stageOne.Length == 0
            || stageThree.Length == 0
            || runOne.Length == 0
            || prepare.Length == 0
            || continuation.Length == 0
            || runThree.Length == 0
            || capture.Length == 0)
        {
            failure = "state=required-method-missing";
            return false;
        }

        if (!ValidatePublicationPrerequisites(
                stageOne,
                stageThree,
                runThree,
                out failure)
            || !ValidateEpochOne(runOne, out failure)
            || !ValidateEpochTwo(
                prepare,
                continuation,
                out failure)
            || !ValidateEpochThree(
                runThree,
                capture,
                out failure)
            || !ValidateEpochFour(runThree, out failure)
            || !ValidateMarkerContract(
                source,
                runThree,
                out failure)
            || !ValidateState(
                source,
                runOne,
                continuation,
                out failure))
        {
            return false;
        }

        if (CountDirectSerializerCalls(source) != 4
            || CountIdentifierOccurrences(
                source,
                "X64InstructionPublication") != 8
            || CountOccurrences(
                Compact(source),
                ExpectedCookie) != 4)
        {
            failure =
                "epoch=1 epoch=2 epoch=3 epoch=4 binding-total";
            return false;
        }

        return true;
    }

    private static bool ValidateM02TerminalProfile(
        string source,
        out string failure)
    {
        failure = string.Empty;
        string terminal = FindTopLevelMethodBody(
                source,
                "EfiEntry",
                "EnterM02OwnedGdtTssTerminalProbe")
            ?? string.Empty;
        string stageOne =
            FindTypeBody(source, "M01StageOneAdapter")
            ?? string.Empty;
        string continuation =
            FindMethodBody(
                stageOne,
                "M01StageOneContinuation")
            ?? string.Empty;
        if (terminal.Length == 0 || continuation.Length == 0)
        {
            failure = "terminal-required-method-missing";
            return false;
        }

        if (CountDirectSerializerCalls(source) != 5
            || CountDirectExpectedCookieReferences(source) != 5
            || CountDirectSerializerCalls(terminal) != 1
            || CountDirectExpectedCookieReferences(terminal) != 1
            || CountIdentifierOccurrences(
                terminal,
                "X64InstructionPublication") != 2)
        {
            failure = "terminal-binding-total";
            return false;
        }

        const string exactGuard =
            "!ValidateM02OwnedGdtTssTerminalResources()||"
            + "!M01StageThreeAdapter."
            + "TryWriteM02OwnedGdtTssTerminalStartLine()||"
            + "X64InstructionPublication.Serialize()!="
            + "X64InstructionPublication.ExpectedSuccessCookie";
        if (!TryFindExactIfBlock(
                terminal,
                exactGuard,
                out SourceRegion guardFailure,
                out int guardEnd))
        {
            failure = "terminal-preentry-guard";
            return false;
        }

        string guardBody = terminal[
            guardFailure.ContentStart..guardFailure.ContentEnd];
        string guardCompact = Compact(guardBody);
        if (!guardCompact.StartsWith(
                "_=FailM02OwnedGdtTssTerminalBeforeEntry(",
                StringComparison.Ordinal)
            || !RequireOrdered(
                guardCompact,
                "_=FailM02OwnedGdtTssTerminalBeforeEntry(",
                "SpinM02OwnedGdtTssTerminalProbe();")
            || !guardCompact.EndsWith(
                "SpinM02OwnedGdtTssTerminalProbe();",
                StringComparison.Ordinal)
            || CountUnqualifiedCalls(
                guardBody,
                "FailM02OwnedGdtTssTerminalBeforeEntry") != 1
            || CountUnqualifiedCalls(
                guardBody,
                "SpinM02OwnedGdtTssTerminalProbe") != 1
            || CountTopLevelKeyword(guardBody, "return") != 0
            || guardCompact.Contains('{')
            || guardCompact.Contains('}'))
        {
            failure = "terminal-preentry-failure";
            return false;
        }

        string terminalSuccess = Compact(terminal[guardEnd..]);
        if (!RequireOrdered(
                terminalSuccess,
                "s_m02OwnedGdtTssTerminalState="
                    + "M02OwnedGdtTssTerminalEnteredState;",
                "terminalEntry=(delegate*unmanaged<",
                "terminalEntry(",
                "TryWriteM02OwnedGdtTssTerminalUnexpectedReturnLine();",
                "SpinM02OwnedGdtTssTerminalProbe();"))
        {
            failure = "terminal-entry-order";
            return false;
        }

        string continuationCompact = Compact(continuation);
        if (CountScopedCalls(
                continuation,
                "EnterM02OwnedGdtTssTerminalProbe") != 1
            || !RequireOrdered(
                continuationCompact,
                "EfiEntry.WriteBootSequencePlan(",
                "EfiEntry.EnterM02OwnedGdtTssTerminalProbe(",
                "Spin();"))
        {
            failure = "terminal-invocation-order";
            return false;
        }

        return true;
    }

    private static bool ValidateState(
        string source,
        string run,
        string continuation,
        out string failure)
    {
        failure = string.Empty;
        string sanitized = SanitizeCode(source);
        if (CountTopLevelFieldDeclarations(
                source,
                "s_m01InstructionPublicationInitialized") != 1
            || CountTopLevelFieldDeclarations(
                source,
                "s_m01InstructionPublicationEpochMask") != 1
            || CountIdentifierOccurrences(
                sanitized,
                "s_m01InstructionPublicationInitialized") != 3
            || CountIdentifierOccurrences(
                sanitized,
                "s_m01InstructionPublicationEpochMask") != 14
            || CountStateMutations(
                sanitized,
                "s_m01InstructionPublicationInitialized") != 1
            || CountStateMutations(
                sanitized,
                "s_m01InstructionPublicationEpochMask") != 5)
        {
            failure = "state=field-or-operator";
            return false;
        }

        string[] values = ["0", "1", "3", "7", "15"];
        foreach (string value in values)
        {
            if (CountAssignments(
                    source,
                    "s_m01InstructionPublicationEpochMask",
                    value) != 1)
            {
                failure = "state=transition-" + value;
                return false;
            }
        }

        if (CountAnyAssignments(
                source,
                "s_m01InstructionPublicationEpochMask") != 5
            || CountAssignments(
                run,
                "s_m01InstructionPublicationInitialized",
                "1") != 1
            || CountAnyAssignments(
                continuation,
                "s_m01InstructionPublicationEpochMask") != 0
            || !Compact(continuation).Contains(
                "s_m01InstructionPublicationEpochMask!=3",
                StringComparison.Ordinal))
        {
            failure = "state=assignment-or-continuation";
            return false;
        }

        string initializationCondition =
            PublicationInitialized
                + "!=0||"
                + PublicationMask
                + "!=0";
        if (!TryFindExactIfBlock(
                run,
                initializationCondition,
                out SourceRegion initializationFailure,
                out int initializationEnd)
            || !ValidateFailureBlock(
                run[initializationFailure.ContentStart
                    ..initializationFailure.ContentEnd],
                CookieFailureWriter + "(console,serial);",
                "returnFailBeforeTransfer(",
                "InstructionPublicationRejected")
            || !Compact(run[initializationEnd..]).StartsWith(
                PublicationMask
                    + "=0;"
                    + PublicationInitialized
                    + "=1;M01StageOneAdapterStatuscaptureStatus="
                    + "TryCaptureLoadedImage(",
                StringComparison.Ordinal))
        {
            failure = "state=initialization-guard";
            return false;
        }

        return true;
    }

    private static bool ValidatePublicationPrerequisites(
        string stageOne,
        string stageThree,
        string stageThreeRun,
        out string failure)
    {
        failure = string.Empty;
        string stackProbe =
            FindMethodBody(
                stageOne,
                "MaterializeAndInstallStackProbe")
            ?? string.Empty;
        string transferThunk =
            FindMethodBody(
                stageThree,
                "MaterializeTransferThunk")
            ?? string.Empty;
        string plannerThunk =
            FindMethodBody(
                stageThree,
                "MaterializePlannerCallThunk")
            ?? string.Empty;
        string protectionReadback =
            FindMethodBody(
                stageThree,
                "ValidateProtectionProbeReadback")
            ?? string.Empty;
        string transferReadback =
            FindMethodBody(
                stageThree,
                "ValidateTransferThunkReadback")
            ?? string.Empty;
        string plannerReadback =
            FindMethodBody(
                stageThree,
                "ValidatePlannerCallThunkReadback")
            ?? string.Empty;
        if (!RequireOrdered(
                Compact(stackProbe),
                "codePage[index]=0xcc;",
                "if(codePage[index]!=0xcc)",
                "probe[index]=GetStackProbeByte(index);",
                "if(probe[index]!=GetStackProbeByte(index))",
                "returnSystem.Runtime.X64StackProbeRuntime"
                    + ".TryInstall((ulong)probe)")
            || !ValidateExactTerminalWindow(
                Compact(stackProbe),
                "for(ulongindex=0;"
                    + "index<StackProbeByteLength;index++){"
                    + "if(probe[index]!="
                    + "GetStackProbeByte(index))",
                ":M01StageOneAdapterStatus"
                    + ".StackProbeInstallRejected;",
                307,
                "d57753e7529fe226196ff955d7de237e"
                    + "4d0969567ec87740b8fa2f9818fef0c8"))
        {
            failure = "epoch=1 publication-prerequisite-helper";
            return false;
        }

        if (!RequireOrdered(
                Compact(transferThunk),
                "if(destination[index]!=0xcc)",
                "destination[index]=GetTransferThunkByte(index);",
                "if(destination[index]"
                    + "!=GetTransferThunkByte(index))",
                "entryAddress=stageOneCodeAllocation"
                    + ".VirtualStart+TransferThunkEntryOffset;",
                "returnentryAddress==(ulong)destination;")
            || !RequireOrdered(
                Compact(plannerThunk),
                "if(destination[index]!=0xcc)",
                "destination[index]=GetPlannerCallThunkByte(index);",
                "if(destination[index]"
                    + "!=GetPlannerCallThunkByte(index))",
                "entryAddress=stageOneCodeAllocation"
                    + ".VirtualStart+PlannerCallThunkEntryOffset;",
                "returnentryAddress==(ulong)destination;")
            || !ValidateExactTerminalWindow(
                Compact(transferThunk),
                "for(ulongindex=0;"
                    + "index<TransferThunkByteLength;index++){"
                    + "if(destination[index]!="
                    + "GetTransferThunkByte(index))",
                "returnentryAddress==(ulong)destination;",
                235,
                "29720b05c95ceab16b29ac23bdeb84d9"
                    + "25b5825483b6d3b1129ff47f33666bcd")
            || !ValidateExactTerminalWindow(
                Compact(plannerThunk),
                "for(ulongindex=0;"
                    + "index<PlannerCallThunkByteLength;index++){"
                    + "if(destination[index]!="
                    + "GetPlannerCallThunkByte(index))",
                "returnentryAddress==(ulong)destination;",
                244,
                "3585ab1d6adc1febc83c3883f502910b"
                    + "52dbf901422e4a05796dbddcbd911b7e"))
        {
            failure = "epoch=3 publication-prerequisite-helper";
            return false;
        }

        if (!ValidateExactTerminalWindow(
                Compact(protectionReadback),
                "for(ulongindex="
                    + "OS.Kernel.Memory"
                    + ".X64ProtectionProbeStubPlan"
                    + ".RequiredByteLength;"
                    + "index<PageSize;index++){"
                    + "if(probePage[index]!=0)",
                "returntrue;",
                151,
                "60cea84dd3d8445f747a4af5c2483f22"
                    + "6b3b0c4973134c4f0b7f6c0d311db69d")
            || !ValidateExactTerminalWindow(
                Compact(transferReadback),
                "for(ulongindex=0;"
                    + "index<TransferThunkByteLength;index++){"
                    + "if(source[index]!="
                    + "GetTransferThunkByte(index))",
                "returntrue;",
                128,
                "dc8541b8af1f99f5b1429a6d2a875ffa"
                    + "6c7227ed1d76d9b73a49d3a1f88b510a")
            || !ValidateExactTerminalWindow(
                Compact(plannerReadback),
                "for(ulongindex=0;"
                    + "index<PlannerCallThunkByteLength;index++){"
                    + "if(source[index]!="
                    + "GetPlannerCallThunkByte(index))",
                "returntrue;",
                134,
                "a6c1ab2883a6d2e84132cd3a863805b9"
                    + "85bdd4b2a10d1f240adaa8aa8544aea5"))
        {
            failure = "epoch=4 final-readback-helper";
            return false;
        }

        string runCompact = Compact(stageThreeRun);
        if (!ValidateExactPublicationWindow(
                runCompact,
                "boolinstructionPublicationRejected=false;"
                    + "if(!MaterializeTransferThunk(",
                "outinstructionPublicationRejected))",
                395,
                "4f67d6fb81477339251b2260b87353cb"
                    + "90d63c2bfd549f76cc270a413f5a3d26"))
        {
            failure = "epoch=3 caller-publication-seam";
            return false;
        }

        if (CountScopedCalls(
                stageThreeRun,
                "MaterializeTransferThunk") != 1
            || CountScopedCalls(
                stageThreeRun,
                "MaterializePlannerCallThunk") != 1
            || CountScopedCalls(
                stageThreeRun,
                "RunPreparationOnPlannerStack") != 1
            || CountScopedCalls(
                stageThreeRun,
                "ValidateProtectionProbeReadback") != 1
            || CountScopedCalls(
                stageThreeRun,
                "ValidateTransferThunkReadback") != 1
            || CountScopedCalls(
                stageThreeRun,
                "ValidatePlannerCallThunkReadback") != 1
            || !runCompact.Contains(
                "||!ValidateProtectionProbeReadback(",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "||!ValidateTransferThunkReadback(",
                StringComparison.Ordinal)
            || !runCompact.Contains(
                "||!ValidatePlannerCallThunkReadback(",
                StringComparison.Ordinal)
            || !RequireOrdered(
                runCompact,
                "MaterializeTransferThunk(",
                "MaterializePlannerCallThunk(",
                "CaptureArchitecturalPreflight(",
                "RunPreparationOnPlannerStack(",
                "M01OwnedCr3CompositeTransition"
                    + ".PrepareCallGate(",
                "M01OwnedCr3CompositeTransition"
                    + ".TryMaterializeCallGate(",
                "M01OwnedCr3CompositeTransition"
                    + ".ValidateCallGateReadback(",
                "ValidateProtectionProbeReadback(",
                "ValidateTransferThunkReadback(",
                "ValidatePlannerCallThunkReadback(",
                "ValidatePreTransferState(",
                "void*)transferEntry;",
                "WriteSuccessLine(console,serial,bootServices,8);"))
        {
            failure = "epoch=3 epoch=4 final-readback-binding";
            return false;
        }

        return true;
    }

    private static bool ValidateEpochOne(
        string run,
        out string failure)
    {
        failure = string.Empty;
        string compact = Compact(run);
        if (!ValidateExactPublicationWindow(
                compact,
                "M01StageOneAdapterStatusstackProbeStatus="
                    + "MaterializeAndInstallStackProbe(",
                "if(" + PublicationMask + "!=0)",
                440,
                "2bdb040797fbb46e5540b19c33df1f7b"
                    + "22d8f5b69a941bdfed796ec56270f37f"))
        {
            failure = "epoch=1 final-readback-to-guard";
            return false;
        }

        if (CountDirectSerializerCalls(run) != 1
            || DirectCallBraceDepth(run) != 0
            || CountAssignments(
                run,
                "s_m01InstructionPublicationEpochMask",
                "1") != 1
            || CountScopedCalls(
                run,
                CookieFailureWriter) < 3
            || !compact.Contains(
                "if(epochOnePublicationCookie!="
                    + ExpectedCookie
                    + ")",
                StringComparison.Ordinal)
            || !RequireOrdered(
                compact,
                "MaterializeAndInstallStackProbe(",
                "stackProbeStatus!=M01StageOneAdapterStatus.Success",
                "s_m01InstructionPublicationEpochMask!=0",
                "uintepochOnePublicationCookie="
                    + SerializerCall
                    + ";",
                "epochOnePublicationCookie!="
                    + ExpectedCookie,
                CookieFailureWriter + "(console,serial);",
                "returnFailBeforeTransfer(",
                "s_m01InstructionPublicationEpochMask=1;",
                "PrepareAndTransfer("))
        {
            failure = "epoch=1 ordering-or-cookie";
            return false;
        }

        if (!ValidateGuardedEpochFlow(
                run,
                PublicationMask + "!=0",
                "epochOnePublicationCookie",
                PublicationMask + "=1;",
                "M01StageOneAdapterStatusprepareStatus="
                    + "PrepareAndTransfer(",
                [
                    CookieFailureWriter + "(console,serial);",
                    "returnFailBeforeTransfer(",
                    "InstructionPublicationRejected"
                ],
                [
                    CookieFailureWriter + "(console,serial);",
                    "returnFailBeforeTransfer(",
                    "InstructionPublicationRejected"
                ]))
        {
            failure = "epoch=1 guarded-flow";
            return false;
        }

        return true;
    }

    private static bool ValidateEpochTwo(
        string prepare,
        string continuation,
        out string failure)
    {
        failure = string.Empty;
        string compact = Compact(prepare);
        string continuationCompact = Compact(continuation);
        if (CountDirectSerializerCalls(prepare) != 1
            || DirectCallBraceDepth(prepare) != 1
            || CountScopedCalls(prepare, "switchEntry") != 1
            || CountAssignments(
                prepare,
                "s_m01InstructionPublicationEpochMask",
                "3") != 1
            || CountScopedCalls(
                prepare,
                CookieFailureWriter) != 2
            || !compact.Contains(
                "if(epochTwoPublicationCookie!="
                    + ExpectedCookie
                    + ")",
                StringComparison.Ordinal)
            || !RequireOrdered(
                compact,
                "M01LoadedImageStackIntegration"
                    + ".PrepareFromStaticScratch(",
                "if(!prepared.IsPrepared)",
                "s_console=console;",
                "s_serial=serial;",
                "s_bootServices=bootServices;",
                "transfer=default;",
                "s_m01InstructionPublicationEpochMask!=1",
                "uintepochTwoPublicationCookie="
                    + SerializerCall
                    + ";",
                "epochTwoPublicationCookie!="
                    + ExpectedCookie,
                "s_m01InstructionPublicationEpochMask=3;",
                "void>switchEntry=",
                "switchEntry(")
            || !continuationCompact.Contains(
                "s_m01InstructionPublicationEpochMask!=3",
                StringComparison.Ordinal)
            || CountAnyAssignments(
                continuation,
                "s_m01InstructionPublicationEpochMask") != 0)
        {
            failure = "epoch=2 ordering-state-or-cookie";
            return false;
        }

        if (!ValidateExactPublicationWindow(
                compact,
                "M01LoadedImageStackStaticPreparationResult"
                    + "prepared=M01LoadedImageStackIntegration"
                    + ".PrepareFromStaticScratch(",
                "if(" + PublicationMask + "!=1)",
                2869,
                "a38fc57aad0ca7ed05acd83261bf1ddbe"
                    + "aa22c2736349f2cca4ccacc69139ddd"))
        {
            failure = "epoch=2 final-readback-to-guard";
            return false;
        }

        int success = compact.IndexOf(
            "s_m01InstructionPublicationEpochMask=3;",
            StringComparison.Ordinal);
        int execute = compact.IndexOf(
            "switchEntry(",
            success,
            StringComparison.Ordinal);
        if (success < 0
            || execute <= success
            || ContainsAny(
                compact[success..execute],
                SerializerCall,
                "WriteSuccessLine(",
                "EfiEntry.Write(",
                "TryMaterialize",
                "Readback",
                "s_console=",
                "s_serial=",
                "s_bootServices="))
        {
            failure = "epoch=2 post-cookie-window";
            return false;
        }

        if (!TryFindExactIfBlock(
                continuation,
                PublicationMask + "!=3",
                out SourceRegion continuationFailure,
                out _)
            || !ValidateSpinFailureBlock(
                continuation[
                    continuationFailure.ContentStart
                    ..continuationFailure.ContentEnd],
                CookieFailureWriter + "(s_console,s_serial);",
                "WriteFailure(",
                "InstructionPublicationRejected",
                "Spin();")
            || !ContinuationGuardPrecedesEveryCall(
                continuation,
                PublicationMask + "!=3"))
        {
            failure = "epoch=2 continuation-guard";
            return false;
        }

        if (!ValidateGuardedEpochFlow(
                prepare,
                PublicationMask + "!=1",
                "epochTwoPublicationCookie",
                PublicationMask + "=3;",
                "delegate*unmanaged<",
                [
                    CookieFailureWriter + "(console,serial);",
                    "returnM01StageOneAdapterStatus"
                        + ".InstructionPublicationRejected;"
                ],
                [
                    CookieFailureWriter + "(console,serial);",
                    "returnM01StageOneAdapterStatus"
                        + ".InstructionPublicationRejected;"
                ]))
        {
            failure = "epoch=2 guarded-flow";
            return false;
        }

        return true;
    }

    private static bool ValidateEpochThree(
        string run,
        string capture,
        out string failure)
    {
        failure = string.Empty;
        string runCompact = Compact(run);
        string compact = Compact(capture);
        if (!ValidateExactPublicationWindow(
                compact,
                "OS.Kernel.Memory"
                    + ".X64PagingModePreflightStubResult"
                    + "materialized=",
                "if(" + PublicationMask + "!=3)",
                1539,
                "53eb01f963a345a5282a8d299e9e2108"
                    + "2a8d456a5ffe881ba65e1ba350faf6d1"))
        {
            failure = "epoch=3 final-readback-to-guard";
            return false;
        }

        if (CountDirectSerializerCalls(capture) != 1
            || DirectCallBraceDepth(capture) != 0
            || CountScopedCalls(capture, "preflight") != 1
            || CountScopedCalls(capture, "readPat") != 1
            || CountAssignments(
                capture,
                "s_m01InstructionPublicationEpochMask",
                "7") != 1
            || CountScopedCalls(
                capture,
                CookieFailureWriter) != 2
            || !compact.Contains(
                "if(epochThreePublicationCookie!="
                    + ExpectedCookie
                    + ")",
                StringComparison.Ordinal)
            || !RequireOrdered(
                runCompact,
                "MaterializeTransferThunk(",
                "MaterializePlannerCallThunk(",
                "CaptureArchitecturalPreflight(",
                "BuildPlanningLedger(")
            || !RequireOrdered(
                compact,
                "X64PagingModePreflightStubPlan"
                    + ".TryMaterialize(",
                "X64PagingModePreflightStubPlan"
                    + ".TryValidateMaterialized(",
                "patReader[index]=GetPatReaderByte(index);",
                "patReader[index]!=GetPatReaderByte(index)",
                "void>preflight=",
                "delegate*unmanaged<ulong*,uint>readPat=",
                "s_m01InstructionPublicationEpochMask!=3",
                "uintepochThreePublicationCookie="
                    + SerializerCall
                    + ";",
                "epochThreePublicationCookie!="
                    + ExpectedCookie,
                "s_m01InstructionPublicationEpochMask=7;",
                "preflight(observation);",
                "readPat(rawIa32Pat)"))
        {
            failure = "epoch=3 placement-order-or-cookie";
            return false;
        }

        if (!compact.Contains(
                "void>)(void*)codeAddress;",
                StringComparison.Ordinal)
            || !compact.Contains(
                "(void*)(codeAddress+PatReaderEntryOffset);"
                    + "if("
                    + PublicationMask
                    + "!=3)",
                StringComparison.Ordinal)
            || !compact.Contains(
                "s_m01InstructionPublicationEpochMask=7;"
                    + "preflight(observation);",
                StringComparison.Ordinal))
        {
            failure = "epoch=3 binding-or-success-window";
            return false;
        }

        if (!ValidateGuardedEpochFlow(
                capture,
                PublicationMask + "!=3",
                "epochThreePublicationCookie",
                PublicationMask + "=7;",
                "preflight(observation);",
                [
                    "instructionPublicationRejected=true;",
                    CookieFailureWriter + "(console,serial);",
                    "returnfalse;"
                ],
                [
                    "instructionPublicationRejected=true;",
                    CookieFailureWriter + "(console,serial);",
                    "returnfalse;"
                ]))
        {
            failure = "epoch=3 guarded-flow";
            return false;
        }

        return true;
    }

    private static bool ValidateEpochFour(
        string run,
        out string failure)
    {
        failure = string.Empty;
        string compact = Compact(run);
        if (CountDirectSerializerCalls(run) != 1
            || DirectCallBraceDepth(run) != 1
            || CountScopedCalls(run, "transfer") != 1
            || CountAssignments(
                run,
                "s_m01InstructionPublicationEpochMask",
                "15") != 1
            || CountScopedCalls(
                run,
                "RejectInstructionPublicationBeforeEntry") != 2
            || CountScopedCalls(
                run,
                EpochsVerifiedWriter) != 1
            || !compact.Contains(
                "if(epochFourPublicationCookie!="
                    + ExpectedCookie
                    + ")",
                StringComparison.Ordinal)
            || !RequireOrdered(
                compact,
                "ValidatePlannerCallThunkReadback(",
                "ValidatePreTransferState(",
                "void*)transferEntry;",
                "WriteSuccessLine(console,serial,bootServices,8);",
                "s_m01InstructionPublicationEpochMask!=7",
                "uintepochFourPublicationCookie="
                    + SerializerCall
                    + ";",
                "epochFourPublicationCookie!="
                    + ExpectedCookie,
                "s_m01InstructionPublicationEpochMask=15;",
                "uintruntimeReturn=transfer(",
                "AuditCallGateObservation(",
                "if(!s_gateAudit.IsObservedAudit)",
                "for(intlineIndex=9;",
                "if(lineIndex==13)",
                "s_m01InstructionPublicationEpochMask!=15",
                EpochsVerifiedWriter + "(console,serial);",
                "WriteSuccessLine(console,serial,bootServices,lineIndex);"))
        {
            failure = "epoch=4 placement-order-or-marker";
            return false;
        }

        if (!compact.Contains(
                "s_m01InstructionPublicationEpochMask=15;"
                    + "uintruntimeReturn=transfer(",
                StringComparison.Ordinal))
        {
            failure = "epoch=4 success-window";
            return false;
        }

        if (!ValidateExactPublicationWindow(
                compact,
                "fixed(M01OwnedCr3CompositeCallGateResult*"
                    + "gateMaterialization=&s_gateMaterialization){"
                    + "s_gateReadback=",
                "if(" + PublicationMask + "!=7)",
                1871,
                "7a0cc8c360fbc0d25d5ce189ee799601"
                    + "e465643cb5ad45f3112f0327cfe4425e"))
        {
            failure = "epoch=4 final-binding-to-guard";
            return false;
        }

        int publication = compact.IndexOf(
            "s_m01InstructionPublicationEpochMask=15;",
            StringComparison.Ordinal);
        int execute = compact.IndexOf(
            "uintruntimeReturn=transfer(",
            publication,
            StringComparison.Ordinal);
        if (publication < 0
            || execute <= publication
            || ContainsAny(
                compact[publication..execute],
                "Write(",
                "WriteSuccessLine(",
                "Readback",
                "Materialize",
                "Validate",
                "transfer=default",
                "console",
                "serial"))
        {
            failure = "epoch=4 post-cookie-window";
            return false;
        }

        if (!ValidateGuardedEpochFlow(
                run,
                PublicationMask + "!=7",
                "epochFourPublicationCookie",
                PublicationMask + "=15;",
                "uintruntimeReturn=transfer(",
                [
                    "returnRejectInstructionPublicationBeforeEntry("
                ],
                [
                    "returnRejectInstructionPublicationBeforeEntry("
                ]))
        {
            failure = "epoch=4 guarded-flow";
            return false;
        }

        return true;
    }

    private static bool ValidateGuardedEpochFlow(
        string method,
        string precondition,
        string cookieVariable,
        string transition,
        string firstPostTransitionOperation,
        string[] preconditionFailureTokens,
        string[] cookieFailureTokens)
    {
        if (!TryFindExactIfBlock(
                method,
                precondition,
                out SourceRegion preconditionFailure,
                out int preconditionEnd)
            || !ValidateFailureBlock(
                method[preconditionFailure.ContentStart
                    ..preconditionFailure.ContentEnd],
                preconditionFailureTokens))
        {
            return false;
        }

        string cookieCondition =
            cookieVariable + "!=" + ExpectedCookie;
        string expectedAfterPrecondition =
            "uint"
                + cookieVariable
                + "="
                + SerializerCall
                + ";if("
                + cookieCondition
                + ")";
        if (!Compact(method[preconditionEnd..]).StartsWith(
                expectedAfterPrecondition,
                StringComparison.Ordinal)
            || !TryFindExactIfBlock(
                method,
                cookieCondition,
                out SourceRegion cookieFailure,
                out int cookieEnd)
            || !ValidateFailureBlock(
                method[cookieFailure.ContentStart
                    ..cookieFailure.ContentEnd],
                cookieFailureTokens)
            || !Compact(method[cookieEnd..]).StartsWith(
                transition + firstPostTransitionOperation,
                StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool ValidateMarkerContract(
        string source,
        string stageThreeRun,
        out string failure)
    {
        failure = string.Empty;
        string cookieWriter =
            FindTopLevelMethodBody(
                source,
                "EfiEntry",
                CookieFailureWriter)
            ?? string.Empty;
        string verifiedWriter =
            FindTopLevelMethodBody(
                source,
                "EfiEntry",
                EpochsVerifiedWriter)
            ?? string.Empty;
        string asciiForwarder =
            FindTopLevelMethodBody(
                source,
                "EfiEntry",
                "WriteM01InstructionPublicationAscii")
            ?? string.Empty;
        string rejectionHelper =
            FindMethodBody(
                FindTypeBody(
                    source,
                    "M01StageThreeAdapter")
                    ?? string.Empty,
                "RejectInstructionPublicationBeforeEntry")
            ?? string.Empty;
        string exactForwarder =
            "if(b0!=0)Write(console,serial,b0);"
                + "if(b1!=0)Write(console,serial,b1);"
                + "if(b2!=0)Write(console,serial,b2);"
                + "if(b3!=0)Write(console,serial,b3);"
                + "if(b4!=0)Write(console,serial,b4);"
                + "if(b5!=0)Write(console,serial,b5);"
                + "if(b6!=0)Write(console,serial,b6);"
                + "if(b7!=0)Write(console,serial,b7);";
        if (cookieWriter.Length == 0
            || verifiedWriter.Length == 0
            || asciiForwarder.Length == 0
            || !TryDecodeMarkerWriter(
                cookieWriter,
                out string cookieMarker)
            || !TryDecodeMarkerWriter(
                verifiedWriter,
                out string verifiedMarker)
            || !string.Equals(
                cookieMarker,
                CookieFailureMarker,
                StringComparison.Ordinal)
            || !string.Equals(
                verifiedMarker,
                EpochsVerifiedMarker,
                StringComparison.Ordinal)
            || !ValidateMarkerWriterShape(cookieWriter)
            || !ValidateMarkerWriterShape(verifiedWriter)
            || !ValidateFailureBlock(
                rejectionHelper,
                CookieFailureWriter + "(console,serial);",
                "returnFailBeforeEntry(",
                "InstructionPublicationRejected")
            || CountScopedCalls(
                source,
                EpochsVerifiedWriter) != 2
            || !string.Equals(
                Compact(asciiForwarder),
                exactForwarder,
                StringComparison.Ordinal))
        {
            failure = "epoch=4 marker-bytes-forwarder-or-newline";
            return false;
        }

        if (!TryFindExactIfBlock(
                stageThreeRun,
                "!s_gateAudit.IsObservedAudit",
                out SourceRegion auditFailure,
                out int auditFailureEnd)
            || !ValidateFailureBlock(
                stageThreeRun[
                    auditFailure.ContentStart
                    ..auditFailure.ContentEnd],
                "returnFailAfterEntry(",
                "PostReturnAuditRejected",
                "(ulong)s_gateAudit.Status")
            || !Compact(
                stageThreeRun[auditFailureEnd..]).StartsWith(
                    "for(intlineIndex=9;",
                    StringComparison.Ordinal))
        {
            failure = "epoch=4 post-return-audit-dominance";
            return false;
        }

        if (!TryFindExactIfBlock(
                stageThreeRun,
                "lineIndex==13",
                out SourceRegion lineThirteen,
                out int lineThirteenEnd))
        {
            failure = "epoch=4 aggregate-line-branch";
            return false;
        }

        string lineBody =
            stageThreeRun[
                lineThirteen.ContentStart
                ..lineThirteen.ContentEnd];
        if (!TryFindExactIfBlock(
                lineBody,
                PublicationMask + "!=15",
                out SourceRegion partialMaskFailure,
                out int partialMaskEnd)
            || !ValidateFailureBlock(
                lineBody[partialMaskFailure.ContentStart
                    ..partialMaskFailure.ContentEnd],
                "returnFailAfterEntry(",
                "InstructionPublicationRejected",
                PublicationMask)
            || CountScopedCalls(
                lineBody,
                EpochsVerifiedWriter) != 1
            || !string.Equals(
                Compact(lineBody[partialMaskEnd..]),
                "EfiEntry."
                    + EpochsVerifiedWriter
                    + "(console,serial);",
                StringComparison.Ordinal)
            || !Compact(
                stageThreeRun[lineThirteenEnd..]).StartsWith(
                    "WriteSuccessLine(console,serial,bootServices,lineIndex);",
                    StringComparison.Ordinal))
        {
            failure = "epoch=4 aggregate-mask-or-placement";
            return false;
        }

        return true;
    }

    private static bool ValidateMarkerWriterShape(
        string writer)
    {
        string compact = Compact(writer);
        return CountUnqualifiedCalls(
                writer,
                "WriteM01InstructionPublicationAscii") == 6
            && CountUnqualifiedCalls(writer, "WriteCrLf") == 1
            && CountScopedCalls(
                writer,
                "WriteM01InstructionPublicationAscii") == 6
            && CountScopedCalls(writer, "WriteCrLf") == 1
            && CountOccurrences(compact, ";") == 7
            && !compact.Contains('{')
            && !compact.Contains('}')
            && IsStraightLineFailureBody(compact)
            && compact.StartsWith(
                "WriteM01InstructionPublicationAscii(",
                StringComparison.Ordinal)
            && compact.EndsWith(
                "WriteCrLf(console,serial);",
                StringComparison.Ordinal);
    }

    private static string MoveEpochThreeCallToCaller(
        string source)
    {
        string mutated = ReplaceInMethod(
            source,
            "M01StageThreeAdapter",
            "CaptureArchitecturalPreflight",
            SerializerCall,
            ExpectedCookie);
        return InsertBeforeInMethod(
            mutated,
            "M01StageThreeAdapter",
            "RunAfterStageTwo",
            "if (!BuildPlanningLedger(",
            "_ = " + SerializerCall + "; ");
    }

    private static string MoveEpochThreeCallAfterPreflight(
        string source)
    {
        string mutated = ReplaceInMethod(
            source,
            "M01StageThreeAdapter",
            "CaptureArchitecturalPreflight",
            SerializerCall,
            ExpectedCookie);
        return InsertAfterInMethod(
            mutated,
            "M01StageThreeAdapter",
            "CaptureArchitecturalPreflight",
            "preflight(observation);",
            " _ = " + SerializerCall + ";");
    }

    private static string MoveEpochFourCallBeforePreMarker(
        string source)
    {
        string mutated = ReplaceInMethod(
            source,
            "M01StageThreeAdapter",
            "RunAfterStageTwo",
            SerializerCall,
            ExpectedCookie);
        return InsertBeforeInMethod(
            mutated,
            "M01StageThreeAdapter",
            "RunAfterStageTwo",
            "WriteSuccessLine(console, serial, bootServices, 8);",
            "_ = " + SerializerCall + "; ");
    }

    private static string MoveEpochFourCallAfterTransfer(
        string source)
    {
        string mutated = ReplaceInMethod(
            source,
            "M01StageThreeAdapter",
            "RunAfterStageTwo",
            SerializerCall,
            ExpectedCookie);
        return InsertBeforeInMethod(
            mutated,
            "M01StageThreeAdapter",
            "RunAfterStageTwo",
            "fixed (M01OwnedCr3CompositeCallGateResult*\n"
                + "                    gateReadback = &s_gateReadback)",
            "_ = " + SerializerCall + "; ");
    }

    private static string MoveFailureBodyToDeadBranchInMethod(
        string source,
        string typeName,
        string methodName,
        string compactCondition)
    {
        SourceRegion type = FindTypeRegion(source, typeName)
            ?? throw new InvalidOperationException(
                "Type not found: " + typeName);
        SourceRegion method = FindMethodRegion(
                source,
                type,
                methodName)
            ?? throw new InvalidOperationException(
                "Method not found: " + methodName);
        string methodBody =
            source[method.ContentStart..method.ContentEnd];
        if (!TryFindExactIfBlock(
                methodBody,
                compactCondition,
                out SourceRegion failureBody,
                out int statementEnd))
        {
            throw new InvalidOperationException(
                "Failure branch is not unique in "
                    + methodName
                    + ": "
                    + compactCondition);
        }

        string originalFailure =
            methodBody[
                failureBody.ContentStart
                ..failureBody.ContentEnd];
        string mutatedMethod =
            methodBody[..failureBody.ContentStart]
                + methodBody[
                    failureBody.ContentEnd
                    ..statementEnd]
                + " if (false) {"
                + originalFailure
                + "}"
                + methodBody[statementEnd..];
        return source[..method.ContentStart]
            + mutatedMethod
            + source[method.ContentEnd..];
    }

    private static string ReplaceInExactIfBodyInMethod(
        string source,
        string typeName,
        string methodName,
        string compactCondition,
        string oldValue,
        string newValue)
    {
        SourceRegion type = FindTypeRegion(source, typeName)
            ?? throw new InvalidOperationException(
                "Type not found: " + typeName);
        SourceRegion method = FindMethodRegion(
                source,
                type,
                methodName)
            ?? throw new InvalidOperationException(
                "Method not found: " + methodName);
        string methodBody =
            source[method.ContentStart..method.ContentEnd];
        if (!TryFindExactIfBlock(
                methodBody,
                compactCondition,
                out SourceRegion failureBody,
                out _))
        {
            throw new InvalidOperationException(
                "Failure branch is not unique in "
                    + methodName
                    + ": "
                    + compactCondition);
        }

        string body =
            methodBody[
                failureBody.ContentStart
                ..failureBody.ContentEnd];
        if (CountOccurrences(body, oldValue) != 1)
        {
            throw new InvalidOperationException(
                "Mutation anchor is not unique in failure branch "
                    + methodName
                    + ": "
                    + oldValue);
        }

        string mutatedBody = body.Replace(
            oldValue,
            newValue,
            StringComparison.Ordinal);
        string mutatedMethod =
            methodBody[..failureBody.ContentStart]
                + mutatedBody
                + methodBody[failureBody.ContentEnd..];
        return source[..method.ContentStart]
            + mutatedMethod
            + source[method.ContentEnd..];
    }

    private static string SetExactIfBodyInMethod(
        string source,
        string typeName,
        string methodName,
        string compactCondition,
        string newBody)
    {
        SourceRegion type = FindTypeRegion(source, typeName)
            ?? throw new InvalidOperationException(
                "Type not found: " + typeName);
        SourceRegion method = FindMethodRegion(
                source,
                type,
                methodName)
            ?? throw new InvalidOperationException(
                "Method not found: " + methodName);
        string methodBody =
            source[method.ContentStart..method.ContentEnd];
        if (!TryFindExactIfBlock(
                methodBody,
                compactCondition,
                out SourceRegion body,
                out _))
        {
            throw new InvalidOperationException(
                "Branch is not unique in "
                    + methodName
                    + ": "
                    + compactCondition);
        }

        string mutatedMethod =
            methodBody[..body.ContentStart]
                + newBody
                + methodBody[body.ContentEnd..];
        return source[..method.ContentStart]
            + mutatedMethod
            + source[method.ContentEnd..];
    }

    private static string InsertAtExactIfBodyStartInMethod(
        string source,
        string typeName,
        string methodName,
        string compactCondition,
        string insertion)
    {
        SourceRegion type = FindTypeRegion(source, typeName)
            ?? throw new InvalidOperationException(
                "Type not found: " + typeName);
        SourceRegion method = FindMethodRegion(
                source,
                type,
                methodName)
            ?? throw new InvalidOperationException(
                "Method not found: " + methodName);
        string methodBody =
            source[method.ContentStart..method.ContentEnd];
        if (!TryFindExactIfBlock(
                methodBody,
                compactCondition,
                out SourceRegion failureBody,
                out _))
        {
            throw new InvalidOperationException(
                "Failure branch is not unique in "
                    + methodName
                    + ": "
                    + compactCondition);
        }

        string mutatedMethod =
            methodBody[..failureBody.ContentStart]
                + insertion
                + methodBody[failureBody.ContentStart..];
        return source[..method.ContentStart]
            + mutatedMethod
            + source[method.ContentEnd..];
    }

    private static string MoveFailureMarkerToDeadBranchInMethod(
        string source,
        string typeName,
        string methodName,
        string compactCondition,
        bool useBraces = true)
    {
        SourceRegion type = FindTypeRegion(source, typeName)
            ?? throw new InvalidOperationException(
                "Type not found: " + typeName);
        SourceRegion method = FindMethodRegion(
                source,
                type,
                methodName)
            ?? throw new InvalidOperationException(
                "Method not found: " + methodName);
        string methodBody =
            source[method.ContentStart..method.ContentEnd];
        if (!TryFindExactIfBlock(
                methodBody,
                compactCondition,
                out SourceRegion failureBody,
                out _))
        {
            throw new InvalidOperationException(
                "Failure branch is not unique in "
                    + methodName
                    + ": "
                    + compactCondition);
        }

        string body =
            methodBody[
                failureBody.ContentStart
                ..failureBody.ContentEnd];
        string sanitized = SanitizeCode(body);
        int writer = sanitized.IndexOf(
            CookieFailureWriter,
            StringComparison.Ordinal);
        int statementStart = writer < 0
            ? -1
            : sanitized.LastIndexOf(
                "EfiEntry",
                writer,
                StringComparison.Ordinal);
        int statementEnd = writer < 0
            ? -1
            : sanitized.IndexOf(';', writer);
        if (statementStart < 0
            || statementEnd <= statementStart)
        {
            throw new InvalidOperationException(
                "Marker statement not found in "
                    + methodName
                    + ".");
        }
        statementEnd++;

        string mutatedBody =
            body[..statementStart]
                + (useBraces
                    ? "if (false) {"
                    : "if (false) ")
                + body[statementStart..statementEnd]
                + (useBraces ? "}" : string.Empty)
                + body[statementEnd..];
        string mutatedMethod =
            methodBody[..failureBody.ContentStart]
                + mutatedBody
                + methodBody[failureBody.ContentEnd..];
        return source[..method.ContentStart]
            + mutatedMethod
            + source[method.ContentEnd..];
    }

    private static string GuardTopLevelFailureMarkerInMethod(
        string source,
        string typeName,
        string methodName)
    {
        SourceRegion type = FindTypeRegion(source, typeName)
            ?? throw new InvalidOperationException(
                "Type not found: " + typeName);
        SourceRegion method = FindMethodRegion(
                source,
                type,
                methodName)
            ?? throw new InvalidOperationException(
                "Method not found: " + methodName);
        string body =
            source[method.ContentStart..method.ContentEnd];
        string sanitized = SanitizeCode(body);
        int writer = sanitized.IndexOf(
            CookieFailureWriter,
            StringComparison.Ordinal);
        int statementStart = writer < 0
            ? -1
            : sanitized.LastIndexOf(
                "EfiEntry",
                writer,
                StringComparison.Ordinal);
        int statementEnd = writer < 0
            ? -1
            : sanitized.IndexOf(';', writer);
        if (statementStart < 0
            || statementEnd <= statementStart
            || sanitized.IndexOf(
                CookieFailureWriter,
                writer + CookieFailureWriter.Length,
                StringComparison.Ordinal) >= 0)
        {
            throw new InvalidOperationException(
                "Top-level marker statement is not unique in "
                    + methodName
                    + ".");
        }
        statementEnd++;

        string mutatedBody =
            body[..statementStart]
                + "if (false) "
                + body[statementStart..statementEnd]
                + body[statementEnd..];
        return source[..method.ContentStart]
            + mutatedBody
            + source[method.ContentEnd..];
    }

    private static string GuardFirstCallWithNoBraceIfInMethod(
        string source,
        string typeName,
        string methodName,
        string callName)
    {
        SourceRegion type = FindTypeRegion(source, typeName)
            ?? throw new InvalidOperationException(
                "Type not found: " + typeName);
        SourceRegion method = FindMethodRegion(
                source,
                type,
                methodName)
            ?? throw new InvalidOperationException(
                "Method not found: " + methodName);
        string body =
            source[method.ContentStart..method.ContentEnd];
        string sanitized = SanitizeCode(body);
        int call = sanitized.IndexOf(
            callName,
            StringComparison.Ordinal);
        int statementStart = 0;
        while (statementStart < call
            && char.IsWhiteSpace(body[statementStart]))
        {
            statementStart++;
        }
        int statementEnd = call < 0
            ? -1
            : sanitized.IndexOf(';', call);
        if (call < 0
            || statementStart > call
            || statementEnd <= call)
        {
            throw new InvalidOperationException(
                "First call statement not found in "
                    + methodName
                    + ": "
                    + callName);
        }
        statementEnd++;

        string mutatedBody =
            body[..statementStart]
                + "if (false) "
                + body[statementStart..statementEnd]
                + body[statementEnd..];
        return source[..method.ContentStart]
            + mutatedBody
            + source[method.ContentEnd..];
    }

    private static string ReplaceExactlyOnce(
        string source,
        string oldValue,
        string newValue)
    {
        if (CountOccurrences(source, oldValue) != 1)
        {
            throw new InvalidOperationException(
                "Global mutation anchor is not unique: "
                    + oldValue);
        }

        return source.Replace(
            oldValue,
            newValue,
            StringComparison.Ordinal);
    }

    private static string ReplaceInMethod(
        string source,
        string typeName,
        string methodName,
        string oldValue,
        string newValue)
    {
        SourceRegion type = FindTypeRegion(source, typeName)
            ?? throw new InvalidOperationException(
                "Type not found: " + typeName);
        SourceRegion method = FindMethodRegion(
                source,
                type,
                methodName)
            ?? throw new InvalidOperationException(
                "Method not found: " + methodName);
        string body = source[
            method.ContentStart..method.ContentEnd];
        if (CountOccurrences(body, oldValue) != 1)
        {
            throw new InvalidOperationException(
                "Mutation anchor is not unique in "
                + methodName
                + ": "
                + oldValue);
        }

        string replaced = body.Replace(
            oldValue,
            newValue,
            StringComparison.Ordinal);
        return source[..method.ContentStart]
            + replaced
            + source[method.ContentEnd..];
    }

    private static string InsertBeforeInMethod(
        string source,
        string typeName,
        string methodName,
        string anchor,
        string insertion)
    {
        return ReplaceInMethod(
            source,
            typeName,
            methodName,
            anchor,
            insertion + anchor);
    }

    private static string InsertAfterInMethod(
        string source,
        string typeName,
        string methodName,
        string anchor,
        string insertion)
    {
        return ReplaceInMethod(
            source,
            typeName,
            methodName,
            anchor,
            anchor + insertion);
    }

    private static int CountDirectSerializerCalls(
        string source)
    {
        return Regex.Matches(
            SanitizeCode(source),
            @"(?<![A-Za-z0-9_.])"
                + @"(?:global\s*::\s*OS\s*\.\s*Boot\s*\.\s*)?"
                + @"X64InstructionPublication\s*\.\s*"
                + @"Serialize\s*\(",
            RegexOptions.CultureInvariant).Count;
    }

    private static int CountDirectExpectedCookieReferences(
        string source)
    {
        return Regex.Matches(
            SanitizeCode(source),
            @"(?<![A-Za-z0-9_.])"
                + @"(?:global\s*::\s*OS\s*\.\s*Boot\s*\.\s*)?"
                + @"X64InstructionPublication\s*\.\s*"
                + @"ExpectedSuccessCookie\b",
            RegexOptions.CultureInvariant).Count;
    }

    private static int DirectCallBraceDepth(string methodBody)
    {
        string sanitized = SanitizeCode(methodBody);
        Match call = Regex.Match(
            sanitized,
            @"(?<![A-Za-z0-9_])"
                + @"global\s*::\s*OS\s*\.\s*Boot\s*\.\s*"
                + @"X64InstructionPublication\s*\.\s*"
                + @"Serialize\s*\(",
            RegexOptions.CultureInvariant);
        if (!call.Success)
        {
            return -1;
        }

        int depth = 0;
        for (int index = 0; index < call.Index; index++)
        {
            if (sanitized[index] == '{')
            {
                depth++;
            }
            else if (sanitized[index] == '}')
            {
                depth--;
            }
        }

        return depth;
    }

    private static int CountScopedCalls(
        string source,
        string methodName)
    {
        return Regex.Matches(
            SanitizeCode(source),
            @"(?<![A-Za-z0-9_])"
                + Regex.Escape(methodName)
                + @"\s*\(",
            RegexOptions.CultureInvariant).Count;
    }

    private static int CountUnqualifiedCalls(
        string source,
        string methodName)
    {
        return Regex.Matches(
            SanitizeCode(source),
            @"(?<![A-Za-z0-9_.])"
                + Regex.Escape(methodName)
                + @"\s*\(",
            RegexOptions.CultureInvariant).Count;
    }

    private static bool ValidateExactPublicationWindow(
        string compact,
        string startToken,
        string endToken,
        int expectedLength,
        string expectedSha256)
    {
        if (CountOccurrences(compact, startToken) != 1
            || CountOccurrences(compact, endToken) != 1)
        {
            return false;
        }

        int start = compact.IndexOf(
            startToken,
            StringComparison.Ordinal);
        int end = compact.IndexOf(
            endToken,
            start,
            StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            return false;
        }

        int length = end + endToken.Length - start;
        if (length != expectedLength)
        {
            return false;
        }
        string window = compact.Substring(start, length);
        byte[] hash =
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(window));
        return string.Equals(
            Convert.ToHexString(hash).ToLowerInvariant(),
            expectedSha256,
            StringComparison.Ordinal);
    }

    private static bool ValidateExactTerminalWindow(
        string compact,
        string startToken,
        string endToken,
        int expectedLength,
        string expectedSha256)
    {
        return compact.EndsWith(
                endToken,
                StringComparison.Ordinal)
            && ValidateExactPublicationWindow(
                compact,
                startToken,
                endToken,
                expectedLength,
                expectedSha256);
    }

    private static int CountAssignments(
        string source,
        string identifier,
        string value)
    {
        return Regex.Matches(
            SanitizeCode(source),
            @"\b"
                + Regex.Escape(identifier)
                + @"\s*=\s*"
                + Regex.Escape(value)
                + @"\s*;",
            RegexOptions.CultureInvariant).Count;
    }

    private static int CountAnyAssignments(
        string source,
        string identifier)
    {
        return Regex.Matches(
            SanitizeCode(source),
            @"\b"
                + Regex.Escape(identifier)
                + @"\s*=\s*(?!=)",
            RegexOptions.CultureInvariant).Count;
    }

    private static int CountStateMutations(
        string source,
        string identifier)
    {
        return Regex.Matches(
            SanitizeCode(source),
            @"\b"
                + Regex.Escape(identifier)
                + @"\b\s*(?:"
                + @"\+\+|--|<<=|>>=|\?\?=|"
                + @"\+=|-=|\*=|/=|%=|&=|\|=|\^=|"
                + @"(?<![=!<>])=(?![=>]))",
            RegexOptions.CultureInvariant).Count;
    }

    private static int CountIdentifierOccurrences(
        string source,
        string identifier)
    {
        return Regex.Matches(
            SanitizeCode(source),
            @"\b" + Regex.Escape(identifier) + @"\b",
            RegexOptions.CultureInvariant).Count;
    }

    private static int CountTopLevelFieldDeclarations(
        string source,
        string identifier)
    {
        SourceRegion type = FindTypeRegion(source, "EfiEntry")
            ?? default;
        if (type.ContentEnd <= type.ContentStart)
        {
            return 0;
        }

        string sanitized = SanitizeCode(source);
        MatchCollection matches = Regex.Matches(
            sanitized[
                type.ContentStart
                ..type.ContentEnd],
            @"\bprivate\s+static\s+byte\s+"
                + Regex.Escape(identifier)
                + @"\s*;",
            RegexOptions.CultureInvariant);
        int count = 0;
        foreach (Match match in matches)
        {
            int absolute = type.ContentStart + match.Index;
            if (BraceDepthAt(
                    sanitized,
                    type.ContentStart,
                    absolute) == 0)
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryFindExactIfBlock(
        string source,
        string exactCompactCondition,
        out SourceRegion body,
        out int statementEnd)
    {
        body = default;
        statementEnd = -1;
        string sanitized = SanitizeCode(source);
        int matchCount = 0;
        foreach (Match match in Regex.Matches(
            sanitized,
            @"\bif\b",
            RegexOptions.CultureInvariant))
        {
            int openParen = SkipWhitespace(
                sanitized,
                match.Index + match.Length,
                sanitized.Length);
            if (openParen >= sanitized.Length
                || sanitized[openParen] != '(')
            {
                continue;
            }

            int closeParen = FindMatchingDelimiter(
                sanitized,
                openParen,
                '(',
                ')');
            if (closeParen < 0
                || !string.Equals(
                    Compact(
                        sanitized[
                            (openParen + 1)
                            ..closeParen]),
                    exactCompactCondition,
                    StringComparison.Ordinal))
            {
                continue;
            }

            int openBrace = SkipWhitespace(
                sanitized,
                closeParen + 1,
                sanitized.Length);
            if (openBrace >= sanitized.Length
                || sanitized[openBrace] != '{')
            {
                return false;
            }

            int closeBrace = FindMatchingDelimiter(
                sanitized,
                openBrace,
                '{',
                '}');
            if (closeBrace <= openBrace)
            {
                return false;
            }

            matchCount++;
            body = new SourceRegion(openBrace + 1, closeBrace);
            statementEnd = closeBrace + 1;
        }

        return matchCount == 1;
    }

    private static bool ValidateFailureBlock(
        string body,
        params string[] orderedTokens)
    {
        string compact = Compact(body);
        return orderedTokens.Length != 0
            && StartsWithRequiredAction(
                compact,
                orderedTokens[0])
            && RequireOrdered(compact, orderedTokens)
            && ValidateFailureMarkerAdjacency(
                compact,
                orderedTokens)
            && CountTopLevelKeyword(body, "return") == 1
            && !compact.Contains('{')
            && !compact.Contains('}')
            && IsStraightLineFailureBody(compact)
            && !ContainsAny(
                compact,
                "goto",
                "continue;",
                "break;");
    }

    private static bool ValidateFailureMarkerAdjacency(
        string compact,
        string[] orderedTokens)
    {
        int markerTokenIndex = Array.FindIndex(
            orderedTokens,
            token => token.StartsWith(
                CookieFailureWriter + "(",
                StringComparison.Ordinal));
        if (markerTokenIndex < 0)
        {
            return true;
        }

        string markerToken = orderedTokens[markerTokenIndex];
        int markerOffset;
        if (markerTokenIndex == 0)
        {
            markerOffset = 0;
        }
        else
        {
            string previousToken =
                orderedTokens[markerTokenIndex - 1];
            int previous = compact.IndexOf(
                previousToken,
                StringComparison.Ordinal);
            if (previous < 0)
            {
                return false;
            }

            markerOffset = previous + previousToken.Length;
        }

        int markerEnd;
        if (compact.AsSpan(markerOffset).StartsWith(
                markerToken,
                StringComparison.Ordinal))
        {
            markerEnd = markerOffset + markerToken.Length;
        }
        else
        {
            string qualified = "EfiEntry." + markerToken;
            if (!compact.AsSpan(markerOffset).StartsWith(
                    qualified,
                    StringComparison.Ordinal))
            {
                return false;
            }

            markerEnd = markerOffset + qualified.Length;
        }

        if (CountOccurrences(
                compact,
                CookieFailureWriter + "(") != 1)
        {
            return false;
        }

        return markerTokenIndex + 1 >= orderedTokens.Length
            || compact.AsSpan(markerEnd).StartsWith(
                orderedTokens[markerTokenIndex + 1],
                StringComparison.Ordinal);
    }

    private static bool ValidateSpinFailureBlock(
        string body,
        params string[] orderedTokens)
    {
        string compact = Compact(body);
        return orderedTokens.Length != 0
            && StartsWithRequiredAction(
                compact,
                orderedTokens[0])
            && RequireOrdered(compact, orderedTokens)
            && CountTopLevelKeyword(body, "return") == 0
            && compact.EndsWith("Spin();", StringComparison.Ordinal)
            && !compact.Contains('{')
            && !compact.Contains('}')
            && IsStraightLineFailureBody(compact)
            && !ContainsAny(
                compact,
                "goto",
                "continue;",
                "break;");
    }

    private static bool IsStraightLineFailureBody(
        string compact)
    {
        return !Regex.IsMatch(
                compact,
                @"(?<![A-Za-z0-9_])"
                    + @"(?:if|else|for|foreach|while|do|switch|"
                    + @"try|catch|finally)"
                    + @"(?![A-Za-z0-9_])",
                RegexOptions.CultureInvariant)
            && !ContainsAny(
                compact,
                "?",
                "&&",
                "||",
                "=>");
    }

    private static bool StartsWithRequiredAction(
        string compact,
        string firstAction)
    {
        return compact.StartsWith(
                firstAction,
                StringComparison.Ordinal)
            || compact.StartsWith(
                "EfiEntry." + firstAction,
                StringComparison.Ordinal);
    }

    private static bool ContinuationGuardPrecedesEveryCall(
        string continuation,
        string guardCondition)
    {
        string compact = Compact(continuation);
        int guard = compact.IndexOf(
            "if(" + guardCondition + ")",
            StringComparison.Ordinal);
        if (guard < 0)
        {
            return false;
        }

        return string.Equals(
            compact[..guard],
            "fixed(M01LoadedImageStackStaticState*"
                + "staticState=&s_staticState){",
            StringComparison.Ordinal);
    }

    private static int CountTopLevelKeyword(
        string source,
        string keyword)
    {
        string sanitized = SanitizeCode(source);
        int count = 0;
        foreach (Match match in Regex.Matches(
            sanitized,
            @"\b" + Regex.Escape(keyword) + @"\b",
            RegexOptions.CultureInvariant))
        {
            if (BraceDepthAt(
                    sanitized,
                    0,
                    match.Index) == 0)
            {
                count++;
            }
        }

        return count;
    }

    private static int BraceDepthAt(
        string source,
        int start,
        int endExclusive)
    {
        int depth = 0;
        for (int index = start;
            index < endExclusive;
            index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}')
            {
                depth--;
            }
        }

        return depth;
    }

    private static bool RequireOrdered(
        string source,
        params string[] tokens)
    {
        int cursor = 0;
        foreach (string token in tokens)
        {
            int index = source.IndexOf(
                token,
                cursor,
                StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            cursor = index + token.Length;
        }

        return true;
    }

    private static bool ContainsAny(
        string source,
        params string[] tokens)
    {
        return tokens.Any(token =>
            source.Contains(token, StringComparison.Ordinal));
    }

    private static string LoadAllSwitchProfile()
    {
        return SelectProfile(
            LoadEfiEntrySource(),
            stageOneEnabled: true,
            stageTwoEnabled: true,
            stageThreeEnabled: true,
            terminalEnabled: false);
    }

    private static string LoadM02TerminalProfile()
    {
        return SelectProfile(
            LoadEfiEntrySource(),
            stageOneEnabled: true,
            stageTwoEnabled: true,
            stageThreeEnabled: true,
            terminalEnabled: true);
    }

    private static string LoadEfiEntrySource()
    {
        string repositoryRoot =
            HostToolProcess.FindRepositoryRoot();
        return NormalizeLineEndings(
            File.ReadAllText(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "OS",
                    "Boot",
                    "EfiEntry.cs")));
    }

    private static string SelectProfile(
        string source,
        bool stageOneEnabled,
        bool stageTwoEnabled,
        bool stageThreeEnabled,
        bool terminalEnabled,
        bool idtrContinuedExecutionEnabled = false,
        bool ud2VectorStateEnabled = false,
        bool pageFaultCr2ErrorCodeEnabled = false,
        bool rhpNewFastEnabled = false,
        bool rhpBeforeBindEnabled = false,
        bool rhpExhaustionEnabled = false,
        bool doubleFaultIstEnabled = false,
        bool arrayEnabled = false,
        bool arrayBeforeBindEnabled = false,
        bool arrayExhaustionEnabled = false,
        bool unexpectedVectorPanicEnabled = false,
        bool stringEnabled = false,
        bool stringBeforeBindEnabled = false,
        bool stringExhaustionEnabled = false,
        bool stringInvalidLengthEnabled = false,
        bool firstExitEnabled = false)
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
                // Explicit Diagnostic selection leaves the normal policy false.
                // Reduce only the reviewed wrappers; preserve unknown terms.
                foreach (string symbol in new[] { StageOneSymbol, StageTwoSymbol,
                    FirstExitSymbol, "ACPI_PHYSICAL_DISCOVERY_PROBE" })
                    condition = condition.Replace("(" + symbol + " || CLROS_NORMAL_VERIFIED_EXIT)",
                        symbol, StringComparison.Ordinal);
                bool firstExitCondition = string.Equals(condition, FirstExitSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, "M04_MANAGED_HEAP_PAGE_RANGE_PROBE || " + FirstExitSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, TypeManagerSymbol + " || " + GcStaticSymbol + " || " + FirstExitSymbol, StringComparison.Ordinal);
                bool loadedImageCondition = string.Equals(condition,
                    M04RhpNewFastSymbol + " || " + M02UnexpectedVectorPanicSymbol + " || " + TypeManagerSymbol + " || " + FirstExitSymbol,
                    StringComparison.Ordinal);
                bool selected =
                    firstExitCondition || loadedImageCondition || string.Equals(
                        condition,
                        StageOneSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(
                        condition,
                        StageTwoSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(
                        condition,
                        StageThreeSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(
                        condition,
                        M02OwnedGdtTssTerminalSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(
                        condition,
                        M02IdtrContinuedExecutionSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(
                        condition,
                        M02Ud2VectorStateSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(
                        condition,
                        M02PageFaultCr2ErrorCodeSymbol,
                        StringComparison.Ordinal)
                    || string.Equals(condition, M02DoubleFaultIstSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M02UnexpectedVectorPanicSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04RhpNewFastSymbol + " || " + M02UnexpectedVectorPanicSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04RhpNewFastSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04RhpBeforeBindSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04RhpExhaustionSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04ArraySymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04ArrayBeforeSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04ArrayExhaustionSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04StringSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04StringBeforeSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04StringExhaustionSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, M04StringInvalidLengthSymbol, StringComparison.Ordinal)
                    || string.Equals(condition, MemoryMapSymbol, StringComparison.Ordinal)
            || string.Equals(condition, EntropySymbol, StringComparison.Ordinal)
            || string.Equals(condition, TypeManagerSymbol, StringComparison.Ordinal)
            || string.Equals(condition, TypedEbsSymbol, StringComparison.Ordinal)
            || string.Equals(condition, GcStaticSymbol, StringComparison.Ordinal)
            || string.Equals(condition, "M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE", StringComparison.Ordinal)
            || string.Equals(condition, "M03_OWNED_SUBSYSTEM_PANIC_PROBE", StringComparison.Ordinal)
            || string.Equals(condition, TypeManagerSymbol + " || " + GcStaticSymbol, StringComparison.Ordinal)
            || string.Equals(condition, "M04_CHECKED_REFERENCE_BARRIER_PROBE || " + GcStaticSymbol, StringComparison.Ordinal)
            || string.Equals(condition, "M04_CHECKED_REFERENCE_BARRIER_PROBE || " + GcStaticSymbol + " || M04_X64_UNWIND_METADATA_PROBE", StringComparison.Ordinal);
                bool selectedValue =
                    firstExitCondition ? firstExitEnabled
                    : loadedImageCondition ? rhpNewFastEnabled || unexpectedVectorPanicEnabled || firstExitEnabled
                    : string.Equals(
                        condition,
                        StageOneSymbol,
                        StringComparison.Ordinal)
                        ? stageOneEnabled
                        : string.Equals(
                            condition,
                            StageTwoSymbol,
                            StringComparison.Ordinal)
                            ? stageTwoEnabled
                            : string.Equals(
                            condition,
                            StageThreeSymbol,
                            StringComparison.Ordinal)
                            ? stageThreeEnabled
                            : string.Equals(
                                condition,
                                M02OwnedGdtTssTerminalSymbol,
                                StringComparison.Ordinal)
                                ? terminalEnabled
                                : string.Equals(
                                    condition,
                                    M02IdtrContinuedExecutionSymbol,
                                    StringComparison.Ordinal)
                                    ? idtrContinuedExecutionEnabled
                                    : string.Equals(
                                        condition,
                                        M02Ud2VectorStateSymbol,
                                        StringComparison.Ordinal)
                                        ? ud2VectorStateEnabled
                                        : string.Equals(condition, M02PageFaultCr2ErrorCodeSymbol, StringComparison.Ordinal)
                                            ? pageFaultCr2ErrorCodeEnabled
                                            : string.Equals(condition, M02DoubleFaultIstSymbol, StringComparison.Ordinal)
                                                ? doubleFaultIstEnabled
                                                : string.Equals(condition, M02UnexpectedVectorPanicSymbol, StringComparison.Ordinal)
                                                    ? unexpectedVectorPanicEnabled
                                                    : string.Equals(condition, M04RhpNewFastSymbol + " || " + M02UnexpectedVectorPanicSymbol, StringComparison.Ordinal)
                                                        ? rhpNewFastEnabled || unexpectedVectorPanicEnabled
                                                : string.Equals(condition, M04RhpNewFastSymbol, StringComparison.Ordinal)
                                                ? rhpNewFastEnabled
                                                : string.Equals(condition, M04RhpBeforeBindSymbol, StringComparison.Ordinal)
                                                    ? rhpBeforeBindEnabled
                                                    : string.Equals(condition, M04RhpExhaustionSymbol, StringComparison.Ordinal) ? rhpExhaustionEnabled
                                                    : string.Equals(condition, M04ArraySymbol, StringComparison.Ordinal) ? arrayEnabled
                                                    : string.Equals(condition, M04ArrayBeforeSymbol, StringComparison.Ordinal) ? arrayBeforeBindEnabled
                                                    : string.Equals(condition, M04ArrayExhaustionSymbol, StringComparison.Ordinal) ? arrayExhaustionEnabled
                                                    : string.Equals(condition, M04StringSymbol, StringComparison.Ordinal) ? stringEnabled
                                                    : string.Equals(condition, M04StringBeforeSymbol, StringComparison.Ordinal) ? stringBeforeBindEnabled
                                                    : string.Equals(condition, M04StringExhaustionSymbol, StringComparison.Ordinal) ? stringExhaustionEnabled
                                                    : string.Equals(condition, M04StringInvalidLengthSymbol, StringComparison.Ordinal) ? stringInvalidLengthEnabled
                                                    : false;
                stack.Push(
                    new PreprocessorFrame(
                        selected,
                        emit,
                        selectedValue,
                        SeenElse: false,
                        IsOwnedSurvival: string.Equals(condition, "M03_OWNED_SUBSYSTEM_SURVIVAL_PROBE", StringComparison.Ordinal)));
                if (selected)
                {
                    emit = emit && selectedValue;
                }
                else if (emit)
                {
                    output.AppendLine(line);
                }

                continue;
            }

            if (string.Equals(trimmed, "#elif M03_FIRMWARE_ADAPTER_REVOCATION_PROBE", StringComparison.Ordinal)
                && stack.Count != 0 && stack.Peek().IsOwnedSurvival)
            {
                PreprocessorFrame frame = stack.Pop();
                if (frame.SeenElse || frame.SeenOwnedRevocation)
                    throw new InvalidOperationException("Invalid owned revocation branch.");
                // Both children are disabled in this existing first-exit profile.
                stack.Push(frame with { SeenOwnedRevocation = true });
                emit = false;
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
                        "Orphan #else.");
                }

                PreprocessorFrame frame = stack.Pop();
                if (frame.SeenElse)
                {
                    throw new InvalidOperationException(
                        "Duplicate #else.");
                }

                frame = frame with { SeenElse = true };
                stack.Push(frame);
                if (frame.IsSelected)
                {
                    emit = frame.ParentEmit
                        && !frame.SelectedValue;
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
                    throw new InvalidOperationException(
                        "Orphan #endif.");
                }

                PreprocessorFrame frame = stack.Pop();
                if (frame.IsSelected)
                {
                    emit = frame.ParentEmit;
                }
                else if (emit)
                {
                    output.AppendLine(line);
                }

                continue;
            }

            if (emit)
            {
                output.AppendLine(line);
            }
        }

        if (stack.Count != 0)
        {
            throw new InvalidOperationException(
                "Unclosed preprocessor directive.");
        }

        return NormalizeLineEndings(output.ToString());
    }

    private static string? FindTypeBody(
        string source,
        string typeName)
    {
        SourceRegion? region = FindTypeRegion(source, typeName);
        return region is null
            ? null
            : source[
                region.Value.ContentStart
                ..region.Value.ContentEnd];
    }

    private static SourceRegion? FindTypeRegion(
        string source,
        string typeName)
    {
        string sanitized = SanitizeCode(source);
        Match match = Regex.Match(
            sanitized,
            @"\bclass\s+" + Regex.Escape(typeName) + @"\b",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return null;
        }

        int open = sanitized.IndexOf(
            '{',
            match.Index + match.Length);
        int close = FindMatchingDelimiter(
            sanitized,
            open,
            '{',
            '}');
        return open >= 0 && close > open
            ? new SourceRegion(open + 1, close)
            : null;
    }

    private static string? FindMethodBody(
        string source,
        string methodName)
    {
        SourceRegion whole = new(0, source.Length);
        SourceRegion? region = FindMethodRegion(
            source,
            whole,
            methodName);
        return region is null
            ? null
            : source[
                region.Value.ContentStart
                ..region.Value.ContentEnd];
    }

    private static string? FindTopLevelMethodBody(
        string source,
        string typeName,
        string methodName)
    {
        SourceRegion type = FindTypeRegion(source, typeName)
            ?? default;
        if (type.ContentEnd <= type.ContentStart)
        {
            return null;
        }

        string sanitized = SanitizeCode(source);
        SourceRegion found = default;
        int foundCount = 0;
        int search = type.ContentStart;
        while (search < type.ContentEnd)
        {
            int name = sanitized.IndexOf(
                methodName,
                search,
                type.ContentEnd - search,
                StringComparison.Ordinal);
            if (name < 0)
            {
                break;
            }

            int before = name - 1;
            int after = name + methodName.Length;
            if ((before >= type.ContentStart
                    && IsIdentifierPart(sanitized[before]))
                || (after < type.ContentEnd
                    && IsIdentifierPart(sanitized[after]))
                || BraceDepthAt(
                    sanitized,
                    type.ContentStart,
                    name) != 0)
            {
                search = after;
                continue;
            }

            int openParen = SkipWhitespace(
                sanitized,
                after,
                type.ContentEnd);
            if (openParen >= type.ContentEnd
                || sanitized[openParen] != '(')
            {
                search = after;
                continue;
            }

            int closeParen = FindMatchingDelimiter(
                sanitized,
                openParen,
                '(',
                ')');
            int openBrace = closeParen < 0
                ? -1
                : SkipWhitespace(
                    sanitized,
                    closeParen + 1,
                    type.ContentEnd);
            if (openBrace < 0
                || openBrace >= type.ContentEnd
                || sanitized[openBrace] != '{')
            {
                search = after;
                continue;
            }

            int closeBrace = FindMatchingDelimiter(
                sanitized,
                openBrace,
                '{',
                '}');
            if (closeBrace <= openBrace
                || closeBrace > type.ContentEnd)
            {
                return null;
            }

            found = new SourceRegion(openBrace + 1, closeBrace);
            foundCount++;
            search = closeBrace + 1;
        }

        return foundCount == 1
            ? source[found.ContentStart..found.ContentEnd]
            : null;
    }

    private static SourceRegion? FindMethodRegion(
        string source,
        SourceRegion scope,
        string methodName)
    {
        string sanitized = SanitizeCode(source);
        int search = scope.ContentStart;
        while (search < scope.ContentEnd)
        {
            int name = sanitized.IndexOf(
                methodName,
                search,
                scope.ContentEnd - search,
                StringComparison.Ordinal);
            if (name < 0)
            {
                return null;
            }

            int before = name - 1;
            int after = name + methodName.Length;
            if ((before >= scope.ContentStart
                    && IsIdentifierPart(sanitized[before]))
                || (after < scope.ContentEnd
                    && IsIdentifierPart(sanitized[after])))
            {
                search = after;
                continue;
            }

            int openParen = SkipWhitespace(
                sanitized,
                after,
                scope.ContentEnd);
            if (openParen >= scope.ContentEnd
                || sanitized[openParen] != '(')
            {
                search = after;
                continue;
            }

            int closeParen = FindMatchingDelimiter(
                sanitized,
                openParen,
                '(',
                ')');
            if (closeParen < 0)
            {
                return null;
            }

            int openBrace = SkipWhitespace(
                sanitized,
                closeParen + 1,
                scope.ContentEnd);
            if (openBrace >= scope.ContentEnd
                || sanitized[openBrace] != '{')
            {
                search = after;
                continue;
            }

            int closeBrace = FindMatchingDelimiter(
                sanitized,
                openBrace,
                '{',
                '}');
            return closeBrace > openBrace
                ? new SourceRegion(openBrace + 1, closeBrace)
                : null;
        }

        return null;
    }

    private static int FindMatchingDelimiter(
        string source,
        int open,
        char openValue,
        char closeValue)
    {
        if (open < 0
            || open >= source.Length
            || source[open] != openValue)
        {
            return -1;
        }

        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == openValue)
            {
                depth++;
            }
            else if (source[index] == closeValue)
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

    private static int SkipWhitespace(
        string source,
        int index,
        int limit)
    {
        while (index < limit
            && char.IsWhiteSpace(source[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsIdentifierPart(char value)
    {
        return char.IsLetterOrDigit(value)
            || value == '_';
    }

    private static string SanitizeCode(string source)
    {
        char[] result = source.ToCharArray();
        int state = 0;
        bool escaped = false;
        for (int index = 0; index < result.Length; index++)
        {
            char current = result[index];
            char next = index + 1 < result.Length
                ? result[index + 1]
                : '\0';
            if (state == 1)
            {
                if (current is '\r' or '\n')
                {
                    state = 0;
                    continue;
                }

                result[index] = ' ';
                continue;
            }

            if (state == 2)
            {
                result[index] = current is '\r' or '\n'
                    ? current
                    : ' ';
                if (current == '*' && next == '/')
                {
                    result[index + 1] = ' ';
                    index++;
                    state = 0;
                }

                continue;
            }

            if (state == 3)
            {
                result[index] = current is '\r' or '\n'
                    ? current
                    : ' ';
                if (!escaped && current == '"')
                {
                    state = 0;
                }
                escaped = !escaped && current == '\\';
                continue;
            }

            if (state == 4)
            {
                result[index] = current is '\r' or '\n'
                    ? current
                    : ' ';
                if (current == '"' && next == '"')
                {
                    result[index + 1] = ' ';
                    index++;
                }
                else if (current == '"')
                {
                    state = 0;
                }

                continue;
            }

            if (state == 5)
            {
                result[index] = current is '\r' or '\n'
                    ? current
                    : ' ';
                if (!escaped && current == '\'')
                {
                    state = 0;
                }
                escaped = !escaped && current == '\\';
                continue;
            }

            if (current == '/' && next == '/')
            {
                result[index] = ' ';
                result[index + 1] = ' ';
                index++;
                state = 1;
            }
            else if (current == '/' && next == '*')
            {
                result[index] = ' ';
                result[index + 1] = ' ';
                index++;
                state = 2;
            }
            else if (current == '"')
            {
                result[index] = ' ';
                escaped = false;
                state = index > 0 && source[index - 1] == '@'
                    ? 4
                    : 3;
            }
            else if (current == '\'')
            {
                result[index] = ' ';
                escaped = false;
                state = 5;
            }
        }

        return new string(result);
    }

    private static string Compact(string source)
    {
        StringBuilder result = new(source.Length);
        foreach (char value in SanitizeCode(source))
        {
            if (!char.IsWhiteSpace(value))
            {
                result.Append(value);
            }
        }

        return result.ToString();
    }

    private static bool TryDecodeMarkerWriter(
        string source,
        out string marker)
    {
        marker = string.Empty;
        string sanitized =
            SanitizeCommentsAndStringsPreservingCharacters(
                source);
        StringBuilder compactBuilder =
            new(sanitized.Length);
        foreach (char value in sanitized)
        {
            if (!char.IsWhiteSpace(value))
            {
                compactBuilder.Append(value);
            }
        }

        string compact = compactBuilder.ToString();
        const string byteValue =
            @"(?:\(byte\)'(?:\\.|[^'])'|0)";
        string pattern =
            @"(?<![A-Za-z0-9_.])"
                + @"WriteM01InstructionPublicationAscii"
                + @"\(console,serial,"
                + @"(?<b0>" + byteValue + @"),"
                + @"(?<b1>" + byteValue + @"),"
                + @"(?<b2>" + byteValue + @"),"
                + @"(?<b3>" + byteValue + @"),"
                + @"(?<b4>" + byteValue + @"),"
                + @"(?<b5>" + byteValue + @"),"
                + @"(?<b6>" + byteValue + @"),"
                + @"(?<b7>" + byteValue + @")\);";
        MatchCollection calls = Regex.Matches(
            compact,
            pattern,
            RegexOptions.CultureInvariant);
        if (calls.Count != 6)
        {
            return false;
        }

        StringBuilder decoded = new();
        foreach (Match call in calls)
        {
            for (int index = 0; index < 8; index++)
            {
                string token =
                    call.Groups["b" + index].Value;
                if (string.Equals(
                        token,
                        "0",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Match literal = Regex.Match(
                    token,
                    @"^\(byte\)'(?<value>\\.|[^'])'$",
                    RegexOptions.CultureInvariant);
                if (!literal.Success)
                {
                    return false;
                }

                string value =
                    literal.Groups["value"].Value;
                decoded.Append(
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
        }

        marker = decoded.ToString();
        return true;
    }

    private static string
        SanitizeCommentsAndStringsPreservingCharacters(
            string source)
    {
        char[] result = source.ToCharArray();
        int state = 0;
        bool escaped = false;
        for (int index = 0; index < result.Length; index++)
        {
            char current = result[index];
            char next = index + 1 < result.Length
                ? result[index + 1]
                : '\0';
            if (state == 1)
            {
                if (current is '\r' or '\n')
                {
                    state = 0;
                    continue;
                }

                result[index] = ' ';
                continue;
            }

            if (state == 2)
            {
                result[index] = current is '\r' or '\n'
                    ? current
                    : ' ';
                if (current == '*' && next == '/')
                {
                    result[index + 1] = ' ';
                    index++;
                    state = 0;
                }

                continue;
            }

            if (state == 3)
            {
                result[index] = current is '\r' or '\n'
                    ? current
                    : ' ';
                if (!escaped && current == '"')
                {
                    state = 0;
                }
                escaped = !escaped && current == '\\';
                continue;
            }

            if (state == 4)
            {
                result[index] = current is '\r' or '\n'
                    ? current
                    : ' ';
                if (current == '"' && next == '"')
                {
                    result[index + 1] = ' ';
                    index++;
                }
                else if (current == '"')
                {
                    state = 0;
                }

                continue;
            }

            if (state == 5)
            {
                if (!escaped && current == '\'')
                {
                    state = 0;
                }
                escaped = !escaped && current == '\\';
                continue;
            }

            if (current == '/' && next == '/')
            {
                result[index] = ' ';
                result[index + 1] = ' ';
                index++;
                state = 1;
            }
            else if (current == '/' && next == '*')
            {
                result[index] = ' ';
                result[index + 1] = ' ';
                index++;
                state = 2;
            }
            else if (current == '"')
            {
                result[index] = ' ';
                escaped = false;
                state = index > 0 && source[index - 1] == '@'
                    ? 4
                    : 3;
            }
            else if (current == '\'')
            {
                escaped = false;
                state = 5;
            }
        }

        return new string(result);
    }

    private static string ReadStageThreeLine(int lineIndex)
    {
        int length =
            BootSequencePlan
                .GetM01StageThreeSuccessLineLength(lineIndex);
        byte[] bytes = new byte[length];
        for (int offset = 0; offset < length; offset++)
        {
            bytes[offset] =
                BootSequencePlan
                    .GetM01StageThreeSuccessLineByte(
                        lineIndex,
                        offset);
        }

        return Encoding.ASCII.GetString(bytes);
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int search = 0;
        while (search <= source.Length - value.Length)
        {
            int index = source.IndexOf(
                value,
                search,
                StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            search = index + value.Length;
        }

        return count;
    }

    private static string NormalizeLineEndings(string source)
    {
        return source.Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                '\r',
                '\n');
    }

    private static bool Failure(string reason)
    {
        Console.WriteLine(
            "TEST_FAIL "
            + IssueTestName
            + " "
            + reason);
        return false;
    }

    private readonly record struct MutationCase(
        string Name,
        string ExpectedFailure,
        Func<string, string> Apply);

    private readonly record struct SourceRegion(
        int ContentStart,
        int ContentEnd);

    private readonly record struct PreprocessorFrame(
        bool IsSelected,
        bool ParentEmit,
        bool SelectedValue,
        bool SeenElse,
        bool IsOwnedSurvival = false,
        bool SeenOwnedRevocation = false);
}
