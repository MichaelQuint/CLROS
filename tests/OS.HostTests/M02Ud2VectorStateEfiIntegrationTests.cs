using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
                "address <= ~0UL - byteCount",
                StringComparison.Ordinal)
            && CanFormEndExclusive(~0UL - 0x2000, 0x2000)
            && !CanFormEndExclusive((~0UL - 0x2000) + 1, 0x2000)
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

    private static bool TestPreparationMaterializationAndReadBack()
    {
        string source = ReadEfiEntry();
        string preparation = ExtractBlock(
            source,
            "private static bool PrepareM02Ud2VectorStateProbe(",
            "private static bool ValidateM02Ud2VectorStateResources()");

        int codeAllocation = preparation.IndexOf(
            "M02Ud2VectorStateEfiLoaderCode",
            StringComparison.Ordinal);
        int dataAllocation = preparation.IndexOf(
            "M02Ud2VectorStateEfiLoaderData",
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
            && CountOccurrences(
                preparation,
                "bootServices->AllocatePages(") == 2
            && codeAllocation >= 0
            && dataAllocation > codeAllocation
            && preparation.Contains(
                "M02Ud2VectorStateCodePageCount,\n" +
                "            &codeAddress);",
                StringComparison.Ordinal)
            && preparation.Contains(
                "M02Ud2VectorStateDataPageCount,\n" +
                "            &dataAddress);",
                StringComparison.Ordinal)
            && preparation.Contains(
                "M02Ud2VectorStateRangesOverlap(\n" +
                "                codeAddress,\n" +
                "                M02Ud2VectorStateCodeByteCount,\n" +
                "                dataAddress,\n" +
                "                M02Ud2VectorStateDataByteCount)",
                StringComparison.Ordinal)
            && codeZero > dataAllocation
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
            && saveCode > zeroIstPages
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
                "contextPrepared.ContextFingerprint !=\n" +
                "                streamEmitted.ContextFingerprint",
                StringComparison.Ordinal)
            && preparation.Contains(
                "streamEmitted.StreamFingerprint !=\n" +
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
                "if (dataAllocated && dataAddress != 0)",
                StringComparison.Ordinal)
            && cleanupPlan.Contains("plan |= 1;", StringComparison.Ordinal)
            && cleanupPlan.Contains(
                "if (codeAllocated &&\n" +
                "            codeAddress != 0 &&\n" +
                "            (!dataAllocated ||\n" +
                "                dataAddress == 0 ||\n" +
                "                M02Ud2VectorStateAllocationRangesDefinitelyDisjoint(\n" +
                "                    codeAddress,\n" +
                "                    dataAddress)))",
                StringComparison.Ordinal)
            && cleanupPlan.Contains("plan |= 2;", StringComparison.Ordinal)
            && cleanupPlan.Contains("return plan;", StringComparison.Ordinal)
            && cleanupPlan.Contains(
                "codeAddress > ~0UL - M02Ud2VectorStateCodeByteCount ||\n" +
                "            dataAddress > ~0UL - M02Ud2VectorStateDataByteCount",
                StringComparison.Ordinal)
            && cleanupPlan.Contains(
                "return codeEnd <= dataAddress || dataEnd <= codeAddress;",
                StringComparison.Ordinal)
            && ModelCleanupPlan(true, 0x0030_0000, true, 0x0040_0000) == 3
            && ModelCleanupPlan(true, 0x0030_0000, true, 0x0030_0000) == 1
            && ModelCleanupPlan(true, 0x0030_0000, true, 0x0030_1000) == 1
            && ModelCleanupPlan(true, 0x0030_4000, true, 0x0030_0000) == 1
            && ModelCleanupPlan(
                true,
                ~0UL - 0x2000 + 1,
                true,
                0x0040_0000) == 1
            && ModelCleanupPlan(
                true,
                0x0030_0000,
                true,
                ~0UL - 0x5000 + 1) == 1
            && ModelCleanupPlan(true, 0x0030_0000, true, 0x0030_2000) == 3
            && ModelCleanupPlan(true, 0x0030_5000, true, 0x0030_0000) == 3
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
        int enteredState = entry.IndexOf(
            "s_m02Ud2VectorStateState = M02Ud2VectorStateEnteredState;",
            StringComparison.Ordinal);
        int serialize = entry.IndexOf(
            "X64InstructionPublication.Serialize()",
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
        int prepare = efiMain.IndexOf(
            "PrepareM02Ud2VectorStateProbe(",
            StringComparison.Ordinal);
        int bootMarkers = efiMain.IndexOf(
            "WriteBootMarkers(",
            StringComparison.Ordinal);
        int fallbackCleanup = efiMain.IndexOf(
            "FailM02Ud2VectorStateBeforeEntry(",
            StringComparison.Ordinal);

        return validate >= 0
            && start > validate
            && functionPointer > start
            && transitionOffset > functionPointer
            && contextAddress > transitionOffset
            && enteredState > contextAddress
            && serialize > enteredState
            && invoke > serialize
            && spin > invoke
            && CountOccurrences(entry, "X64InstructionPublication.Serialize()") == 1
            && CountOccurrences(entry, "terminalEntry(context);") == 1
            && Regex.IsMatch(
                entry,
                @"if \(X64InstructionPublication\.Serialize\(\) !=\s*" +
                @"X64InstructionPublication\.ExpectedSuccessCookie\)\s*" +
                @"\{.*?SpinM02Ud2VectorStateProbe\(\);\s*\}\s*" +
                @"terminalEntry\(context\);",
                RegexOptions.CultureInvariant | RegexOptions.Singleline)
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
            && m02013 > bootPlan
            && m02009 > m02013
            && CountOccurrences(
                continuation,
                "EfiEntry.EnterM02Ud2VectorStateProbe(") == 1
            && resolveConsole >= 0
            && resolveSerial > resolveConsole
            && prepare > resolveSerial
            && bootMarkers > prepare
            && fallbackCleanup > bootMarkers
            && CountOccurrences(
                efiMain,
                "PrepareM02Ud2VectorStateProbe(") == 1
            && CountOccurrences(
                efiMain,
                "FailM02Ud2VectorStateBeforeEntry(") == 1
            && efiMain.Contains(
                "s_m02Ud2VectorStateCodeAddress,\n" +
                "            s_m02Ud2VectorStateCodeAddress != 0,\n" +
                "            s_m02Ud2VectorStateDataAddress,\n" +
                "            s_m02Ud2VectorStateDataAddress != 0",
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

    private static bool CanFormEndExclusive(ulong address, ulong byteCount)
    {
        return byteCount != 0 && address <= ~0UL - byteCount;
    }

    private static byte ModelCleanupPlan(
        bool codeAllocated,
        ulong codeAddress,
        bool dataAllocated,
        ulong dataAddress)
    {
        byte plan = dataAllocated && dataAddress != 0 ? (byte)1 : (byte)0;
        if (codeAllocated &&
            codeAddress != 0 &&
            (!dataAllocated ||
                dataAddress == 0 ||
                ModelAllocationRangesDefinitelyDisjoint(
                    codeAddress,
                    dataAddress)))
        {
            plan |= 2;
        }

        return plan;
    }

    private static bool ModelAllocationRangesDefinitelyDisjoint(
        ulong codeAddress,
        ulong dataAddress)
    {
        const ulong codeByteCount = 0x2000;
        const ulong dataByteCount = 0x5000;
        if (!CanFormEndExclusive(codeAddress, codeByteCount) ||
            !CanFormEndExclusive(dataAddress, dataByteCount))
        {
            return false;
        }

        ulong codeEnd = codeAddress + codeByteCount;
        ulong dataEnd = dataAddress + dataByteCount;
        return codeEnd <= dataAddress || dataEnd <= codeAddress;
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
}
