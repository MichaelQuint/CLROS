using OS.Tooling;
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
        EfiContracts.PortIoStubPageSha256;
    private const ulong ModeledPageAddress = 0x0040_0000;
    private const ulong PublicationEpoch = 7;
    private const PageProtection ReadWrite =
        PageProtection.Read | PageProtection.Write;
    private const PageProtection ReadExecute =
        PageProtection.Read | PageProtection.Execute;

    private static readonly string[] M02SuccessMarkers = QemuContracts.M02IssueSuccessMarkers.ToArray();

    private static readonly string[] M01SuccessMarkers = QemuContracts.M01StageThreeGuestMarkers.ToArray();

    private static readonly string[] M02ForbiddenMarkers = QemuContracts.M02IssueForbiddenMarkers.ToArray();

    [HostTest("m02.017 reviewed fixed page reconstructs exactly", HostLane.Fast, 0)]
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

    [HostTest("m02.017 EFI marker writer and cross-stage order are exact", HostLane.Fast, 1)]
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
            "WriteSuccessLine(console, serial, bootServices, 4);",
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
            "WriteSuccessLine(console, serial, bootServices, 8);",
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

    [HostTest("m02.017 project and build selector are fail closed", HostLane.Fast, 2)]
    private static bool TestProjectAndBuildSelector()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(root, "src", "OS", "OS.csproj");
        XDocument project = XDocument.Load(projectPath);
        XElement[] defaults = project
            .Descendants("EnableM02SafeStubProbe")
            .Where(element => element.Attribute("Condition")?.Value ==
                "'$(EnableM02SafeStubProbe)' == ''")
            .ToArray();
        XElement[] ud2ProfileOverrides = project
            .Descendants("PropertyGroup")
            .Where(group => group.Attribute("Condition")?.Value ==
                "'$(EnableM02Ud2VectorStateProbe)' == 'true'")
            .SelectMany(group => group.Elements("EnableM02SafeStubProbe"))
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

        return defaults.Length == 1
            && defaults[0].Value == "false"
            && defaults[0].Attribute("Condition")?.Value
                == "'$(EnableM02SafeStubProbe)' == ''"
            && ud2ProfileOverrides.Length == 1
            && ud2ProfileOverrides[0].Value == "true"
            && symbols.Length == 1
            && symbols[0].Elements("DefineConstants").Single().Value
                == "$(DefineConstants);M02_SAFE_STUB_PROBE"
            && errorConditions.Length == 16
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM03TypedExitBootServicesCallProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM04CheckedReferenceBarrierProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM03MemoryMapCaptureProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM03FirmwareEntropySeedProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM04SzArrayAllocationProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM04StringAllocationProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM04StringAllocationBeforeBindProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM04StringAllocationExhaustionProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM04StringAllocationInvalidLengthProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM04SzArrayAllocationBeforeBindProbe)' == 'true'",
                StringComparer.Ordinal)
            && errorConditions.Contains(
                "'$(EnableM02SafeStubProbe)' == 'true' and '$(EnableM04SzArrayAllocationExhaustionProbe)' == 'true'",
                StringComparer.Ordinal)
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
                string.Empty);
    }

    [HostTest("m02.017 QEMU profiles are immutable and match the guest", HostLane.Fast, 3)]
    private static bool TestQemuProfiles() => EfiProfileTestControls.Returning("M02SafeStub",
        QemuContracts.M02SafeStubRequiredMarkers,QemuContracts.M02SafeStubForbiddenMarkers,
        M02SuccessMarkers[4],M02ForbiddenMarkers[0]);

    [HostTest("m02.017 integration source keeps one safe no-firmware invocation", HostLane.Fast, 4)]
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
