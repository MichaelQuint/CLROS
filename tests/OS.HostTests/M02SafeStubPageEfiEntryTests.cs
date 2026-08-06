using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static unsafe class M02SafeStubPageEfiEntryTests
{
    private const string TestName = "m02.017";
    private const string ExpectedSha256 =
        "B45339EB93D346D9FE6B32700F227CFB367AB1D10970DD6655A173AFE54705E2";
    private const ulong ModeledPageAddress = 0x0040_0000;
    private const ulong PublicationEpoch = 7;
    private const PageProtection ReadWrite =
        PageProtection.Read | PageProtection.Write;
    private const PageProtection ReadExecute =
        PageProtection.Read | PageProtection.Execute;

    private static readonly string[] M02SuccessMarkers =
    [
        "TEST_START m02.017",
        "M02_STUB_PAGE_MATERIALIZED pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56",
        "M02_STUB_PAGE_READBACK_VERIFIED fnv1a64=982E5B7E3EE58E56",
        "M02_SAFE_PROBE_EXECUTED offset=0 return=0x4F534F4B",
        "CLROS_M02_017_READY pageBytes=4096 safeOffset=0 out8Offset=16 in8Offset=32 fnv1a64=982E5B7E3EE58E56 temporaryOwnedRootRx=true portIoExecuted=false",
        "TEST_PASS m02.017"
    ];

    private static readonly string[] M01SuccessMarkers =
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
        "TEST_PASS m01.stage1",
        "TEST_START m01.stage2",
        "M01_ALLOCATOR_ARENA_ALLOCATED",
        "M01_STAGE2_MAP_RECAPTURED",
        "M01_RESERVATIONS_APPLIED",
        "M01_LIVE_ALLOCATED_4K",
        "M01_LIVE_PAGE_WITHIN_ARENA",
        "M01_LIVE_FREED_4K",
        "M01_DOUBLE_FREE_REJECTED",
        "TEST_PASS m01.stage2",
        "TEST_START m01.stage3",
        "M01_CR3_STUB_PAGE_ALLOCATED",
        "M01_PROTECTION_PROBE_PAGE_ALLOCATED",
        "M01_PAGE_TABLE_STATE_ARENA_ALLOCATED",
        "M01_STAGE3_MAP_RECAPTURED",
        "M01_CR3_STUB_MATERIALIZED",
        "M01_PAGE_TABLES_BUILT",
        "M01_PAGING_MODE_VALIDATED",
        "M01_PRE_CR3",
        "M01_FIRMWARE_CR3_RESTORED",
        "M01_RFLAGS_RESTORED",
        "M01_PGE_CYCLE_VERIFIED",
        "M01_OWNED_CR3_CANARY_VERIFIED",
        "M01_INSTRUCTION_PUBLICATION_EPOCHS_VERIFIED",
        "M01_POST_CR3",
        "M01_RW_TO_RX_VERIFIED",
        "M01_TLB_INVALIDATION_COMPLETE",
        "M01_RX_PROBE_EXECUTED",
        "M01_PAGE_ZERO_ABSENT",
        "TEST_PASS m01.stage3",
        "CLROS_M01_LIVE_BOOT_MEMORY_READY",
        "OS_SMOKE_PASS"
    ];

    private static readonly string[] M02ForbiddenMarkers =
    [
        "M02_STUB_PAGE_MATERIALIZATION_FAILED",
        "M02_STUB_PAGE_READBACK_FAILED",
        "M02_STUB_PAGE_HASH_MISMATCH",
        "M02_SAFE_PROBE_FAILED",
        "M02_OUT8_EXECUTED",
        "M02_IN8_EXECUTED",
        "M02_PORT_IO_ATTEMPTED",
        "M02_PRIVILEGED_STUB_TARGETED",
        "TEST_FAIL m02.017"
    ];

    [ModuleInitializer]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "reviewed fixed page reconstructs exactly",
            TestReviewedFixedPage,
            ref failures);
        RunCase(
            "EFI marker writer and cross-stage order are exact",
            TestEfiMarkerWriterAndOrder,
            ref failures);
        RunCase(
            "project and build selector are fail closed",
            TestProjectAndBuildSelector,
            ref failures);
        RunCase(
            "QEMU profiles are immutable and match the guest",
            TestQemuProfiles,
            ref failures);
        RunCase(
            "integration source keeps one safe no-firmware invocation",
            TestIntegrationSourceBoundary,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-contract failures=" + failures);
            throw new InvalidOperationException(
                "M02-017 EFI integration host tests failed: " + failures);
        }

        Console.WriteLine(M02SuccessMarkers[4]);
        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(
        string name,
        Func<bool> test,
        ref int failures)
    {
        try
        {
            if (test())
            {
                return;
            }

            failures++;
            Console.WriteLine(
                "M02_017_CASE_FAIL " + Normalize(name));
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine(
                "M02_017_CASE_FAIL "
                + Normalize(name)
                + " "
                + ex.GetType().Name
                + ": "
                + ex.Message);
        }
    }

    private static bool TestReviewedFixedPage()
    {
        void* allocation = NativeMemory.AlignedAlloc(
            (nuint)ExistingIn8Out8StubBytesRWPagesFinalize.PageSize,
            (nuint)ExistingIn8Out8StubBytesRWPagesFinalize.PageSize);
        if (allocation == null)
        {
            return false;
        }

        try
        {
            Span<byte> page = new(
                allocation,
                checked((int)ExistingIn8Out8StubBytesRWPagesFinalize.PageSize));
            page.Fill(0xa5);
            ExistingIn8Out8StubPageState state =
                ExistingIn8Out8StubPageState.Create();
            ExistingIn8Out8StubPageResult materialized =
                ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(
                    page,
                    ModeledPageAddress,
                    ExistingIn8Out8StubBytesRWPagesFinalize.SafeOffset,
                    ExistingIn8Out8StubBytesRWPagesFinalize.Out8Offset,
                    ExistingIn8Out8StubBytesRWPagesFinalize.In8Offset,
                    PortIoStubPlan.Out8,
                    PortIoStubPlan.In8,
                    ReadWrite,
                    PublicationEpoch,
                    ref state,
                    expectedTransitionVersion: 0);
            ExistingIn8Out8StubPageResult readback =
                ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                    page,
                    ModeledPageAddress,
                    materialized,
                    ref state,
                    expectedTransitionVersion: 1);
            ExistingIn8Out8StubPageResult finalized =
                ExistingIn8Out8StubBytesRWPagesFinalize.AuditFinalization(
                    readback,
                    ModeledPageAddress,
                    ReadWrite,
                    ReadExecute,
                    executionAttemptedBeforeFinalization: false,
                    ref state,
                    expectedTransitionVersion: 2);

            return materialized.Succeeded
                && readback.ReadBackVerified
                && finalized.FinalizationAudited
                && state.FinalizationCount == 1
                && HasExactPage(page)
                && ComputeFnv1a64(page) == 0x982e_5b7e_3ee5_8e56
                && Convert.ToHexString(SHA256.HashData(page))
                    == ExpectedSha256;
        }
        finally
        {
            NativeMemory.AlignedFree(allocation);
        }
    }

    private static bool TestEfiMarkerWriterAndOrder()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));
        string[] reconstructed = ExtractM02WriterMarkers(source);
        if (!reconstructed.SequenceEqual(
                M02SuccessMarkers,
                StringComparer.Ordinal))
        {
            return false;
        }

        int stageThreeRecaptured = source.IndexOf(
            "WriteSuccessLine(console, serial, 4);",
            StringComparison.Ordinal);
        int gateMaterialized = source.IndexOf(
            "if (!s_gateMaterialization.IsMaterialized)",
            stageThreeRecaptured,
            StringComparison.Ordinal);
        int prepareCall = source.IndexOf(
            "if (!PrepareM02SafeStubPage(",
            gateMaterialized,
            StringComparison.Ordinal);
        int gateReadback = source.IndexOf(
            "gateMaterialization =",
            prepareCall,
            StringComparison.Ordinal);
        int preCr3 = source.IndexOf(
            "WriteSuccessLine(console, serial, 8);",
            gateReadback,
            StringComparison.Ordinal);
        int transfer = source.IndexOf(
            "transfer(",
            preCr3,
            StringComparison.Ordinal);
        int observedAuditGuard = source.IndexOf(
            "if (!s_gateAudit.IsObservedAudit)",
            transfer,
            StringComparison.Ordinal);
        int postReturnLoop = source.IndexOf(
            "for (int lineIndex = 9;",
            observedAuditGuard,
            StringComparison.Ordinal);
        int writerCall = source.IndexOf(
            "WriteSuccessLine(",
            postReturnLoop,
            StringComparison.Ordinal);

        int prepareMethod = source.IndexOf(
            "private static bool PrepareM02SafeStubPage(",
            writerCall,
            StringComparison.Ordinal);
        int originalProbeReadback = source.IndexOf(
            "ValidateOriginalProtectionProbeBeforeM02(",
            prepareMethod,
            StringComparison.Ordinal);
        int start = source.IndexOf(
            "WriteM02SuccessLine(console, serial, 0);",
            originalProbeReadback,
            StringComparison.Ordinal);
        int materialize = source.IndexOf(
            "M02SafeStubPageIntegration.Materialize(",
            start,
            StringComparison.Ordinal);
        int materializedMarker = source.IndexOf(
            "WriteM02SuccessLine(console, serial, 1);",
            materialize,
            StringComparison.Ordinal);
        int readback = source.IndexOf(
            "M02SafeStubPageIntegration.ValidateReadback(",
            materializedMarker,
            StringComparison.Ordinal);
        int readbackMarker = source.IndexOf(
            "WriteM02SuccessLine(console, serial, 2);",
            readback,
            StringComparison.Ordinal);
        int finalization = source.IndexOf(
            "M02SafeStubPageIntegration.AuditFinalization(",
            readbackMarker,
            StringComparison.Ordinal);

        int writer = source.IndexOf(
            "private static void WriteSuccessLine(",
            finalization,
            StringComparison.Ordinal);
        int executionGuard = source.IndexOf(
            "if (lineIndex == 9)",
            writer,
            StringComparison.Ordinal);
        int executionAudit = source.IndexOf(
            "M02SafeStubPageIntegration.AuditExecution(",
            executionGuard,
            StringComparison.Ordinal);
        int lastThreeGuard = source.IndexOf(
            "if (lineIndex == 18)",
            executionAudit,
            StringComparison.Ordinal);
        int safeMarker = source.IndexOf(
            "WriteM02SuccessLine(console, serial, 3);",
            lastThreeGuard,
            StringComparison.Ordinal);
        int readyMarker = source.IndexOf(
            "WriteM02SuccessLine(console, serial, 4);",
            safeMarker,
            StringComparison.Ordinal);
        int passMarker = source.IndexOf(
            "WriteM02SuccessLine(console, serial, 5);",
            readyMarker,
            StringComparison.Ordinal);

        return stageThreeRecaptured >= 0
            && stageThreeRecaptured < gateMaterialized
            && gateMaterialized < prepareCall
            && prepareCall < gateReadback
            && gateReadback < preCr3
            && preCr3 < transfer
            && transfer < observedAuditGuard
            && observedAuditGuard < postReturnLoop
            && postReturnLoop < writerCall
            && writerCall < prepareMethod
            && prepareMethod < originalProbeReadback
            && originalProbeReadback < start
            && start < materialize
            && materialize < materializedMarker
            && materializedMarker < readback
            && readback < readbackMarker
            && readbackMarker < finalization
            && finalization < writer
            && writer < executionGuard
            && executionGuard < executionAudit
            && executionAudit < lastThreeGuard
            && lastThreeGuard < safeMarker
            && safeMarker < readyMarker
            && readyMarker < passMarker;
    }

    private static bool TestProjectAndBuildSelector()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        XDocument project = XDocument.Load(projectPath);
        XElement[] defaults = project
            .Descendants("EnableM02SafeStubProbe")
            .Where(element => element.Name.LocalName == "EnableM02SafeStubProbe")
            .ToArray();
        XElement[] symbols = project
            .Descendants("PropertyGroup")
            .Where(group =>
                string.Equals(
                    group.Attribute("Condition")?.Value,
                    "'$(EnableM02SafeStubProbe)' == 'true'",
                    StringComparison.Ordinal))
            .ToArray();
        string[] errorConditions = project
            .Descendants("Error")
            .Select(error => error.Attribute("Condition")?.Value ?? string.Empty)
            .Where(value => value.StartsWith(
                "'$(EnableM02SafeStubProbe)' == 'true'",
                StringComparison.Ordinal))
            .ToArray();
        string script = File.ReadAllText(
            Path.Combine(root, "scripts", "build-efi-shell.ps1"));

        return defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value
                == "'$(EnableM02SafeStubProbe)' == ''"
            && symbols.Length == 1
            && symbols[0].Elements("DefineConstants").Single().Value
                == "$(DefineConstants);M02_SAFE_STUB_PROBE"
            && errorConditions.Length == 3
            && errorConditions.Any(value => value.Contains(
                "EnableM01LoadedImageStackProbe",
                StringComparison.Ordinal))
            && errorConditions.Any(value => value.Contains(
                "EnableM01LiveAllocatorProbe",
                StringComparison.Ordinal))
            && errorConditions.Any(value => value.Contains(
                "EnableM01OwnedPagingProbe",
                StringComparison.Ordinal))
            && BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Diagnostics\PortIoOperationPlan.cs",
                @"Diagnostics\PortIoOperationPlan.cs")
            && BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Diagnostics\PortIoStubPlan.cs",
                @"Diagnostics\PortIoStubPlan.cs")
            && BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs",
                @"Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs")
            && BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"Boot\M02SafeStubPageIntegration.cs",
                string.Empty)
            && script.Contains(
                "[switch] $EnableM02SafeStubProbe",
                StringComparison.Ordinal)
            && script.Contains(
                "EFI_PUBLISH_SOURCE_AUTHORITY status=ok inputs=68",
                StringComparison.Ordinal)
            && script.Contains(
                "reviewed=\" + $m01SourceClosure.ReviewedSourceCount",
                StringComparison.Ordinal)
            && script.Contains(
                "$arguments += \"-p:EnableM02SafeStubProbe=true\"",
                StringComparison.Ordinal)
            && script.Contains(
                "Write-Host \"EFI_PROBE M02_SAFE_STUB\"",
                StringComparison.Ordinal);
    }

    private static bool TestQemuProfiles()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string script = File.ReadAllText(
            Path.Combine(root, "scripts", "run-qemu-smoke.ps1"));
        string[] expectedCombined = BuildCombinedSuccessMarkers();
        string[] expectedForbidden =
        [
            .. M01ForbiddenMarkers(),
            .. M02ForbiddenMarkers
        ];
        string[] scriptSuccess = ExtractPowerShellArray(
            script,
            "$script:M02SafeStubRequiredMarkers = @(");
        string[] scriptForbidden = ExtractPowerShellArray(
            script,
            "$script:M02SafeStubForbiddenMarkers = @(");
        string[] shimSuccess = ExtractCSharpArray(
            script,
            "private static readonly string[] M02GuestMarkers =");
        string[] shimForbidden = ExtractCSharpArray(
            script,
            "private static readonly string[] M02ForbiddenMarkers =");
        string[] m01ShimSuccess = ExtractCSharpArray(
            script,
            "private static readonly string[] GuestMarkers =");
        string[] m01ShimForbidden = ExtractCSharpArray(
            script,
            "private static readonly string[] ForbiddenMarkers =");

        return expectedCombined.Length == 48
            && expectedForbidden.Length == 34
            && scriptSuccess.SequenceEqual(
                expectedCombined,
                StringComparer.Ordinal)
            && scriptForbidden.SequenceEqual(
                expectedForbidden,
                StringComparer.Ordinal)
            && shimSuccess.SequenceEqual(
                expectedCombined,
                StringComparer.Ordinal)
            && shimForbidden.SequenceEqual(
                expectedForbidden,
                StringComparer.Ordinal)
            && m01ShimSuccess.SequenceEqual(
                M01SuccessMarkers,
                StringComparer.Ordinal)
            && m01ShimForbidden.SequenceEqual(
                M01ForbiddenMarkers(),
                StringComparer.Ordinal)
            && scriptSuccess.Distinct(
                StringComparer.Ordinal).Count() == 48
            && scriptForbidden.Distinct(
                StringComparer.Ordinal).Count() == 34
            && script.Contains(
                "$requiredMarkers = @(",
                StringComparison.Ordinal)
            && script.Contains(
                "$script:M02SafeStubRequiredMarkers",
                StringComparison.Ordinal)
            && script.Contains(
                "$script:M02SafeStubForbiddenMarkers",
                StringComparison.Ordinal)
            && script.Contains(
                "$buildArguments += \"-EnableM02SafeStubProbe\"",
                StringComparison.Ordinal)
            && script.Contains(
                "m02_safe_stub_probe_requires_build_efi",
                StringComparison.Ordinal)
            && script.Contains(
                "m02_safe_stub_probe_forbids_skip_image_build",
                StringComparison.Ordinal)
            && script.Contains(
                "CLROS_M02_SAFE_STUB_PROBE",
                StringComparison.Ordinal);
    }

    private static bool TestIntegrationSourceBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string helper = File.ReadAllText(
            Path.Combine(
                root,
                "src",
                "OS",
                "Boot",
                "M02SafeStubPageIntegration.cs"));
        string efiEntry = File.ReadAllText(
            Path.Combine(root, "src", "OS", "Boot", "EfiEntry.cs"));

        return helper.Contains(
                "public const ulong StructureSize = 96;",
                StringComparison.Ordinal)
            && helper.Contains(
                "public const ulong SafeOffset = 0;",
                StringComparison.Ordinal)
            && helper.Contains(
                "public const ulong Out8Offset = 16;",
                StringComparison.Ordinal)
            && helper.Contains(
                "public const ulong In8Offset = 32;",
                StringComparison.Ordinal)
            && helper.Contains(
                "public const uint SafeReturnValue = 0x4f53_4f4b;",
                StringComparison.Ordinal)
            && helper.Contains(
                "public const ulong ExpectedFnv1a64 = 0x982e_5b7e_3ee5_8e56;",
                StringComparison.Ordinal)
            && helper.Contains(
                "public const bool PortIoExecuted = false;",
                StringComparison.Ordinal)
            && helper.Contains(
                "public const bool PrivilegedStubTargeted = false;",
                StringComparison.Ordinal)
            && helper.Contains(
                "runtimeState->ProbeEntry != modeledProbePageAddress + SafeOffset",
                StringComparison.Ordinal)
            && helper.Contains(
                "runtimeState->InvlpgTarget",
                StringComparison.Ordinal)
            && helper.Contains(
                "!= runtimeState->ProbeEntry",
                StringComparison.Ordinal)
            && helper.Contains(
                "!= ExactProbeInvocationCount",
                StringComparison.Ordinal)
            && helper.Contains(
                "runtimeState->ProhibitedFirmwareCallbackCount != 0",
                StringComparison.Ordinal)
            && helper.Contains(
                "runtimeState->ProbeReturn != SafeReturnValue",
                StringComparison.Ordinal)
            && !helper.Contains("throw ", StringComparison.Ordinal)
            && !helper.Contains("System.Reflection", StringComparison.Ordinal)
            && !helper.Contains("dynamic ", StringComparison.Ordinal)
            && !helper.Contains("delegate*", StringComparison.Ordinal)
            && efiEntry.Contains(
                "(delegate* unmanaged<",
                StringComparison.Ordinal)
            && efiEntry.Contains(
                "M02SafeStubPageIntegration.AuditExecution(",
                StringComparison.Ordinal)
            && CountOccurrences(
                efiEntry,
                "M02SafeStubPageIntegration.AuditExecution(") == 1
            && efiEntry.Contains(
                "s_gateAudit.PreparedState.ExpectedSelfAddress;",
                StringComparison.Ordinal)
            && efiEntry.Contains(
                "(void*)runtimeStateAddress,",
                StringComparison.Ordinal)
            && efiEntry.Contains(
                "gateAudit = &s_gateAudit",
                StringComparison.Ordinal)
            && efiEntry.Contains(
                "_ = FailAfterEntry(",
                StringComparison.Ordinal)
            && !efiEntry.Contains(
                "M02_OUT8_EXECUTED",
                StringComparison.Ordinal)
            && !efiEntry.Contains(
                "M02_IN8_EXECUTED",
                StringComparison.Ordinal)
            && !efiEntry.Contains(
                "M02_PORT_IO_ATTEMPTED",
                StringComparison.Ordinal);
    }

    private static bool HasExactPage(ReadOnlySpan<byte> page)
    {
        if (page.Length != 4096)
        {
            return false;
        }

        ReadOnlySpan<byte> safe = [0xb8, 0x4b, 0x4f, 0x53, 0x4f, 0xc3];
        ReadOnlySpan<byte> out8 = [0x88, 0xd0, 0x66, 0x89, 0xca, 0xee, 0xc3];
        ReadOnlySpan<byte> in8 = [0x66, 0x89, 0xca, 0xec, 0xc3];
        for (int index = 0; index < page.Length; index++)
        {
            byte expected = index < safe.Length
                ? safe[index]
                : index >= 16 && index < 16 + out8.Length
                    ? out8[index - 16]
                    : index >= 32 && index < 32 + in8.Length
                        ? in8[index - 32]
                        : (byte)0;
            if (page[index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static ulong ComputeFnv1a64(ReadOnlySpan<byte> bytes)
    {
        ulong hash = 0xcbf2_9ce4_8422_2325;
        foreach (byte value in bytes)
        {
            hash = (hash ^ value) * 0x0000_0100_0000_01b3UL;
        }

        return hash;
    }

    private static string[] ExtractM02WriterMarkers(string source)
    {
        int methodStart = source.IndexOf(
            "private static void WriteM02SuccessLine(",
            StringComparison.Ordinal);
        int methodEnd = source.IndexOf(
            "#endif",
            methodStart,
            StringComparison.Ordinal);
        if (methodStart < 0 || methodEnd <= methodStart)
        {
            return [];
        }

        string method = source[methodStart..methodEnd];
        string[] markers = new string[M02SuccessMarkers.Length];
        for (int lineIndex = 0; lineIndex < markers.Length; lineIndex++)
        {
            string caseToken = "case " + lineIndex + ":";
            int caseStart = method.IndexOf(
                caseToken,
                StringComparison.Ordinal);
            int caseEnd = method.IndexOf(
                "break;",
                caseStart,
                StringComparison.Ordinal);
            if (caseStart < 0 || caseEnd <= caseStart)
            {
                return [];
            }

            MatchCollection characters = Regex.Matches(
                method[caseStart..caseEnd],
                @"\(byte\)'(?<value>.)'",
                RegexOptions.CultureInvariant);
            markers[lineIndex] = string.Concat(
                characters
                    .Cast<Match>()
                    .Select(match => match.Groups["value"].Value));
        }

        return markers;
    }

    private static string[] ExtractPowerShellArray(
        string source,
        string token)
    {
        string normalized = source.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
        int start = normalized.IndexOf(token, StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : normalized.IndexOf(
                "\n)",
                start + token.Length,
                StringComparison.Ordinal);
        return ExtractQuotedValues(normalized, start, end);
    }

    private static string[] ExtractCSharpArray(
        string source,
        string token)
    {
        int tokenStart = source.IndexOf(token, StringComparison.Ordinal);
        int start = tokenStart < 0
            ? -1
            : source.IndexOf(
                "{",
                tokenStart + token.Length,
                StringComparison.Ordinal);
        int end = start < 0
            ? -1
            : source.IndexOf(
                "};",
                start + 1,
                StringComparison.Ordinal);
        return ExtractQuotedValues(source, start, end);
    }

    private static string[] ExtractQuotedValues(
        string source,
        int start,
        int end)
    {
        if (start < 0 || end <= start)
        {
            return [];
        }

        return Regex.Matches(
                source[start..end],
                "\"(?<value>[^\"]*)\"",
                RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(match => match.Groups["value"].Value)
            .ToArray();
    }

    private static string[] BuildCombinedSuccessMarkers()
    {
        List<string> markers = [];
        markers.AddRange(M01SuccessMarkers.Take(25));
        markers.AddRange(M02SuccessMarkers.Take(3));
        markers.AddRange(M01SuccessMarkers.Skip(25).Take(14));
        markers.AddRange(M02SuccessMarkers.Skip(3));
        markers.AddRange(M01SuccessMarkers.Skip(39));
        return markers.ToArray();
    }

    private static string[] M01ForbiddenMarkers()
    {
        return
        [
            "LIVE_ALLOCATOR_NOT_INSTALLED",
            "BOOT_PAGE_ALLOCATOR_NOT_READY",
            "M01_ARBITRARY_CONVENTIONAL_PAGE_USED",
            "M01_PAGE_OUTSIDE_ALLOCATOR_ARENA",
            "M01_PAGE_TABLE_OUTSIDE_ARENA",
            "M01_PROTECTION_PROBE_ALLOCATION_FAILED",
            "M01_PROTECTION_PROBE_ALIAS",
            "M01_CR3_STUB_READBACK_FAILED",
            "M01_PAGING_MODE_VALIDATION_FAILED",
            "M01_PGE_CLEAR_FAILED",
            "M01_PGE_RESTORE_FAILED",
            "M01_EXECUTABLE_TABLE_DATA",
            "M01_INSTRUCTION_PUBLICATION_COOKIE_FAILED",
            "M01_FIRMWARE_CALLBACK_DURING_OWNED_CR3",
            "M01_FIRMWARE_CR3_RESTORE_FAILED",
            "M01_CR4_RESTORE_FAILED",
            "M01_RFLAGS_RESTORE_FAILED",
            "M01_OWNED_CR3_CANARY_FAILED",
            "M01_RX_PROBE_FAILED",
            "TEST_FAIL",
            "OS_SMOKE_FAIL",
            "M01_INTEGRATION_FAIL",
            "M01_STAGE1_FAIL",
            "M01_STAGE2_FAIL",
            "M01_STAGE3_FAIL"
        ];
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int searchIndex = 0;
        while (searchIndex <= source.Length - value.Length)
        {
            int match = source.IndexOf(
                value,
                searchIndex,
                StringComparison.Ordinal);
            if (match < 0)
            {
                break;
            }

            count++;
            searchIndex = match + value.Length;
        }

        return count;
    }

    private static string Normalize(string value)
    {
        return value.Replace(' ', '_');
    }
}
