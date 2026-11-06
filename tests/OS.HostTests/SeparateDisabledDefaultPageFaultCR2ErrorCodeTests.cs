using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using Probe = OS.Kernel.Diagnostics.SeparateDisabledDefaultPageFaultCR2ErrorCode;

internal static class SeparateDisabledDefaultPageFaultCR2ErrorCodeTests
{
    private const ulong InstructionAddress = 0x20_0123;
    private const string HostMarker =
        "CLROS_M02_014_HOST_READY vector=14 errorCodePresent=true errorCode=0 " +
        "cr2=0 access=read auditFields=5 probeEnabled=false syntheticObservation=true " +
        "hardwareStateCaptured=false faultTriggered=false guestExecution=false " +
        "postExitBootServices=false";

    [HostFixture(HostLane.Fast)]
    internal static void RunAll()
    {
        HostCheck.Run("m02.014", () =>
        {
            TestDefaultsAndValueBoundary();
            TestPrerequisitesAndSafetyFlags();
            TestM01015IntentRequired();
            TestInstructionAddresses();
            TestFaultFieldsAndPrecedence();
            TestExactErrorAndCr2Bits();
            TestAllocationAndInputStability();
            TestSourceBoundary();
            Console.WriteLine(HostMarker);
        });
    }

    private static void TestDefaultsAndValueBoundary()
    {
        SeparatePageFaultRequest emptyRequest = default;
        SeparatePageFaultResult emptyResult = default;
        Require(!emptyRequest.IsInitialized && !emptyResult.IsInitialized &&
            !emptyResult.Succeeded && !emptyResult.ReadyDisabled &&
            !emptyResult.SyntheticObservationAccepted, "defaults must fail closed");
        Require(!emptyResult.IsGuestEvidence && !emptyResult.CapturedHardwareState &&
            !emptyResult.FaultTriggered, "default must not claim hardware evidence");
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<SeparatePageFaultRequest>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<SeparatePageFaultResult>(),
            "request and result must contain no references");
        Require(Probe.EnabledByDefault == Probes.EnableNullPointerPanicProbe &&
            !Probe.EnabledByDefault && !NullPageFaultProbePlan.EnabledByDefault,
            "authoritative destructive probe must be disabled");
        Require(Probe.ExpectedVector == 14 && Probe.ExpectedVector == FaultVectorCatalog.PageFault &&
            Probe.ExpectedErrorCode == 0 && Probe.ExpectedCr2 == 0 && Probe.AuditFieldCount == 5,
            "fixed supervisor null-read policy");
        AssertPlanFailure(default, SeparatePageFaultStatus.InvalidRequest);

        SeparatePageFaultRequest request = ReadyRequest();
        SeparatePageFaultResult planned = Probe.Plan(request);
        Require(planned.IsInitialized && planned.ReadyDisabled && planned.Succeeded &&
            !planned.SyntheticObservationAccepted && planned.ComparedFieldCount == 0 &&
            planned.FirstMismatchField == SeparatePageFaultField.None &&
            planned.ExpectedValue == 0 && planned.ObservedValue == 0,
            "planning must not claim an observation");
        AssertNoHardwareEvidence(planned);
        AssertAccepted(Probe.AuditSyntheticObservation(request, Frame()));

        foreach (string field in new[] { "_initialized", "_executionFlags", "_readinessFlags" })
        {
            SeparatePageFaultRequest malformed = request;
            int offset = checked((int)Marshal.OffsetOf<SeparatePageFaultRequest>(field));
            MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref malformed, 1))[offset] = 0xff;
            Require(!malformed.IsInitialized, "malformed request field " + field);
            AssertPlanFailure(malformed, SeparatePageFaultStatus.InvalidRequest);
        }
    }

    private static void TestPrerequisitesAndSafetyFlags()
    {
        NullPageFaultProbePlanResult intent = NullReadPlan(AbsencePolicy());
        for (int execution = 0; execution < 8; execution++)
        {
            for (int readiness = 0; readiness < 16; readiness++)
            {
                bool enabled = (execution & 1) != 0;
                bool requested = (execution & 2) != 0;
                bool attempted = (execution & 4) != 0;
                bool serial = (readiness & 1) != 0;
                bool gdtTss = (readiness & 2) != 0;
                bool idt = (readiness & 4) != 0;
                bool handler = (readiness & 8) != 0;
                SeparatePageFaultRequest request = SeparatePageFaultRequest.CreateForValidation(
                    intent, InstructionAddress, enabled, requested, attempted,
                    serial, gdtTss, idt, handler);
                SeparatePageFaultStatus expected = enabled
                    ? SeparatePageFaultStatus.EnabledByDefaultRejected
                    : requested ? SeparatePageFaultStatus.ExecutionRequestedRejected
                    : attempted ? SeparatePageFaultStatus.ExecutionAttemptedRejected
                    : !serial ? SeparatePageFaultStatus.OwnedSerialUnavailable
                    : !gdtTss ? SeparatePageFaultStatus.OwnedGdtTssUnavailable
                    : !idt ? SeparatePageFaultStatus.OwnedIdtUnavailable
                    : !handler ? SeparatePageFaultStatus.PageFaultHandlerUnavailable
                    : SeparatePageFaultStatus.ReadyDisabled;
                if (expected == SeparatePageFaultStatus.ReadyDisabled)
                {
                    Require(Probe.Plan(request).ReadyDisabled, "all prerequisites supplied");
                    AssertAccepted(Probe.AuditSyntheticObservation(request, Frame()));
                }
                else
                {
                    AssertPlanFailure(request, expected);
                }
            }
        }
    }

    private static void TestM01015IntentRequired()
    {
        PageZeroMappingPolicyResult absence = AbsencePolicy();
        PageZeroMappingPolicyResult mereIntent = PageZeroMappingPolicy.ValidateMappingIntent(
            PageZeroMappingIntent.CreateAtRawMappingBoundary(0, 4096, present: false));
        Require(mereIntent.Succeeded && !mereIntent.ProvesPageZeroAbsentInSuppliedImage,
            "mapping intent alone is not an image audit");
        NullPageFaultProbePlanResult[] invalidPlans =
        [
            default,
            NullReadPlan(default),
            NullReadPlan(mereIntent),
            NullPageFaultProbePlan.Plan(NullPageFaultProbeRequest.CreateDisabled(absence, false, true)),
            NullPageFaultProbePlan.Plan(NullPageFaultProbeRequest.CreateDisabled(absence, true, false)),
            NullPageFaultProbePlan.Plan(NullPageFaultProbeRequest.CreateForValidation(
                absence, true, true, true, false, false)),
            NullPageFaultProbePlan.Plan(NullPageFaultProbeRequest.CreateForValidation(
                absence, false, true, true, true, false)),
            NullPageFaultProbePlan.Plan(NullPageFaultProbeRequest.CreateForValidation(
                absence, false, true, true, false, true))
        ];
        foreach (NullPageFaultProbePlanResult plan in invalidPlans)
        {
            Require(!plan.Succeeded, "invalid M01-015 fixture unexpectedly accepted");
            AssertPlanFailure(ReadyRequest(plan), SeparatePageFaultStatus.NullReadPlanUnavailable);
        }

        // Corruption cannot turn a successful M01-015 result into a different
        // access kind, error expectation, CR2, privilege level or enabled state.
        NullPageFaultProbePlanResult valid = NullReadPlan(absence);
        foreach (string field in new[] { "_accessKind", "_hasHardwareErrorCode",
            "_expectedPrivilegeLevel", "_isEnabled", "_expectedVector",
            "_expectedErrorCode", "_checkedErrorCodeMask", "_expectedCr2Raw" })
        {
            NullPageFaultProbePlanResult malformed = valid;
            int offset = checked((int)Marshal.OffsetOf<NullPageFaultProbePlanResult>(field));
            MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref malformed, 1))[offset] ^= 0x80;
            AssertPlanFailure(ReadyRequest(malformed), SeparatePageFaultStatus.NullReadPlanUnavailable);
        }
    }

    private static void TestInstructionAddresses()
    {
        NullPageFaultProbePlanResult intent = NullReadPlan(AbsencePolicy());
        foreach (ulong address in new ulong[] { 0, 1, 0x7ff, 0xfff })
            AssertPlanFailure(ReadyRequest(intent, address),
                SeparatePageFaultStatus.InstructionAddressInPageZero);
        foreach (ulong address in new ulong[] { 0x0000_8000_0000_0000,
            0x0000_ffff_ffff_ffff, 0xffff_7fff_ffff_ffff })
            AssertPlanFailure(ReadyRequest(intent, address),
                SeparatePageFaultStatus.NonCanonicalInstructionAddress);
        foreach (ulong address in new ulong[] { 0x1000, 0x1001,
            0x0000_7fff_ffff_ffff, 0xffff_8000_0000_0000, ulong.MaxValue })
        {
            SeparatePageFaultRequest request = ReadyRequest(intent, address);
            Require(Probe.Plan(request).ReadyDisabled, "canonical address must remain metadata only");
            AssertAccepted(Probe.AuditSyntheticObservation(request, Frame(rip: address)));
        }
        // No byte count is accepted or inferred, even at ulong.MaxValue.
        // An eventual executable instruction span requires a different contract.
    }

    private static void TestFaultFieldsAndPrecedence()
    {
        SeparatePageFaultRequest request = ReadyRequest();
        for (int vector = -1; vector <= 256; vector++)
        {
            SeparatePageFaultResult result = Probe.AuditSyntheticObservation(request, Frame(vector: vector));
            if (vector == 14)
                AssertAccepted(result);
            else
                AssertMismatch(result, SeparatePageFaultStatus.VectorMismatch,
                    SeparatePageFaultField.Vector, 14, unchecked((ulong)(long)vector));
        }
        AssertMismatch(Probe.AuditSyntheticObservation(request, Frame(hasErrorCode: false)),
            SeparatePageFaultStatus.ErrorCodePresenceMismatch, SeparatePageFaultField.HasErrorCode, 1, 0);
        AssertMismatch(Probe.AuditSyntheticObservation(request, Frame(rip: InstructionAddress + 1)),
            SeparatePageFaultStatus.InstructionAddressMismatch, SeparatePageFaultField.Rip,
            InstructionAddress, InstructionAddress + 1);
        AssertMismatch(Probe.AuditSyntheticObservation(request,
            Frame(vector: 6, hasErrorCode: false, errorCode: 2, cr2: 1, rip: 0)),
            SeparatePageFaultStatus.VectorMismatch, SeparatePageFaultField.Vector, 14, 6);
        AssertMismatch(Probe.AuditSyntheticObservation(request,
            Frame(hasErrorCode: false, errorCode: 2, cr2: 1, rip: 0)),
            SeparatePageFaultStatus.ErrorCodePresenceMismatch, SeparatePageFaultField.HasErrorCode, 1, 0);
        AssertMismatch(Probe.AuditSyntheticObservation(request, Frame(errorCode: 2, cr2: 1, rip: 0)),
            SeparatePageFaultStatus.ErrorCodeMismatch, SeparatePageFaultField.ErrorCode, 0, 2);
        AssertMismatch(Probe.AuditSyntheticObservation(request, Frame(cr2: 1, rip: 0)),
            SeparatePageFaultStatus.Cr2Mismatch, SeparatePageFaultField.Cr2, 0, 1);

        // This leaf does not validate saved RF, RSP, CR3, GPRs or frame layout.
        // A synthetic five-field match cannot be promoted to register capture.
        CpuFaultFrame unrelatedRegisters = new(14, true, 0, InstructionAddress,
            ulong.MaxValue, ulong.MaxValue, 0x10046, 0, ulong.MaxValue,
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
        AssertAccepted(Probe.AuditSyntheticObservation(request, unrelatedRegisters));
    }

    private static void TestExactErrorAndCr2Bits()
    {
        SeparatePageFaultRequest request = ReadyRequest();
        for (int bit = 0; bit < 64; bit++)
        {
            ulong value = 1UL << bit;
            AssertMismatch(Probe.AuditSyntheticObservation(request, Frame(errorCode: value)),
                SeparatePageFaultStatus.ErrorCodeMismatch, SeparatePageFaultField.ErrorCode, 0, value);
            AssertMismatch(Probe.AuditSyntheticObservation(request, Frame(cr2: value)),
                SeparatePageFaultStatus.Cr2Mismatch, SeparatePageFaultField.Cr2, 0, value);
        }
        foreach (ulong error in new ulong[] { 3, 7, 0x1f, 0xffff_ffff, ulong.MaxValue })
            AssertMismatch(Probe.AuditSyntheticObservation(request, Frame(errorCode: error)),
                SeparatePageFaultStatus.ErrorCodeMismatch, SeparatePageFaultField.ErrorCode, 0, error);
        // Error 2 is the legacy null-WRITE policy and must never pass this read.
        AssertMismatch(Probe.AuditSyntheticObservation(request, Frame(errorCode: 2)),
            SeparatePageFaultStatus.ErrorCodeMismatch, SeparatePageFaultField.ErrorCode, 0, 2);
    }

    private static void TestAllocationAndInputStability()
    {
        SeparatePageFaultRequest request = ReadyRequest();
        CpuFaultFrame frame = Frame();
        byte[] before = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref request, 1)).ToArray();
        for (int i = 0; i < 100; i++)
        {
            _ = Probe.Plan(request).Succeeded;
            _ = Probe.AuditSyntheticObservation(request, frame).Succeeded;
        }
        long start = GC.GetAllocatedBytesForCurrentThread();
        int successes = 0;
        for (int i = 0; i < 10_000; i++)
        {
            if (Probe.Plan(request).ReadyDisabled)
                successes++;
            if (Probe.AuditSyntheticObservation(request, frame).SyntheticObservationAccepted)
                successes++;
            if (!Probe.AuditSyntheticObservation(default, frame).Succeeded)
                successes++;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Require(successes == 30_000 && allocated == 0, "warmed calls must allocate zero bytes");
        Require(before.AsSpan().SequenceEqual(
            MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref request, 1))),
            "intent bytes must remain unchanged");
    }

    private static void TestSourceBoundary()
    {
        DirectoryInfo? root = new(Environment.CurrentDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CLROS.slnx")))
            root = root.Parent;
        Require(root is not null, "repository root missing");
        string source = File.ReadAllText(Path.Combine(root!.FullName,
            "src/OS.Kernel/Diagnostics/SeparateDisabledDefaultPageFaultCR2ErrorCode.cs"));
        source = Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline);
        foreach (string forbidden in new[] { "unsafe", "stackalloc", "delegate", "throw",
            "System.", "Span<", "string ", "object ", "FaultProbeStubPlan", "EfiEntry",
            "BootSequence", "RuntimeImport", "DllImport", "UnmanagedCallersOnly", "new byte[" })
            Require(!source.Contains(forbidden, StringComparison.Ordinal),
                "host-leaf source boundary: " + forbidden);
        Require(source.Contains("Probes.EnableNullPointerPanicProbe", StringComparison.Ordinal),
            "do not duplicate the destructive toggle authority");
        Require(source.Contains("request.NullReadPlan.Succeeded", StringComparison.Ordinal),
            "M01-015 image/intent authority must be consumed");
    }

    private static SeparatePageFaultRequest ReadyRequest() => ReadyRequest(NullReadPlan(AbsencePolicy()));

    private static SeparatePageFaultRequest ReadyRequest(
        NullPageFaultProbePlanResult plan, ulong rip = InstructionAddress) =>
        SeparatePageFaultRequest.CreateDisabled(plan, rip, true, true, true, true);

    private static NullPageFaultProbePlanResult NullReadPlan(PageZeroMappingPolicyResult policy) =>
        NullPageFaultProbePlan.Plan(NullPageFaultProbeRequest.CreateDisabled(policy, true, true));

    private static unsafe PageZeroMappingPolicyResult AbsencePolicy()
    {
        Require(PhysicalAddress.TryCreateAlignedFromHardwareRegisterBoundary(0x10_0000, 4096,
            out PhysicalAddress root) == AddressValueStatus.Success, "root fixture");
        Require(X64PageTableImagePage.TryCreate(root, 0, out X64PageTableImagePage page) ==
            X64PageMapStatus.Success, "page fixture");
        Require(X64PatMappingToken.TryCreate(0x0006_0501_0007_0406, 0, 3, 4,
            out X64PatMappingToken pat) == X64PageTableEntryStatus.Success, "PAT fixture");
        X64PageTableEncodingPreconditions context = X64PageTableEncodingPreconditions.CreateObserved(
            48, true, true, false, true, pat);
        ulong* entries = stackalloc ulong[512];
        new Span<ulong>(entries, 512).Clear();
        PageZeroMappingPolicyResult result = PageZeroMappingPolicy.AuditTableImage(
            root, context, &page, 1, 1, entries, 512, 512);
        Require(result.ProvesPageZeroAbsentInSuppliedImage, "absent PML4 entry fixture");
        return result;
    }

    private static CpuFaultFrame Frame(int vector = 14, bool hasErrorCode = true,
        ulong errorCode = 0, ulong cr2 = 0, ulong rip = InstructionAddress) =>
        new(vector, hasErrorCode, errorCode, rip, 0, 0, 0, cr2, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static void AssertPlanFailure(SeparatePageFaultRequest request, SeparatePageFaultStatus expected)
    {
        foreach (SeparatePageFaultResult result in new[]
            { Probe.Plan(request), Probe.AuditSyntheticObservation(request, Frame()) })
        {
            Require(result.IsInitialized && result.Status == expected && !result.Succeeded &&
                !result.ReadyDisabled && !result.SyntheticObservationAccepted &&
                result.FirstMismatchField == SeparatePageFaultField.None && result.ComparedFieldCount == 0,
                "request rejection " + expected + ", actual=" + result.Status);
            AssertNoHardwareEvidence(result);
        }
    }

    private static void AssertMismatch(SeparatePageFaultResult result, SeparatePageFaultStatus status,
        SeparatePageFaultField field, ulong expected, ulong observed)
    {
        Require(result.IsInitialized && !result.Succeeded && !result.ReadyDisabled &&
            !result.SyntheticObservationAccepted && result.Status == status &&
            result.FirstMismatchField == field && result.ComparedFieldCount == (byte)field &&
            result.ExpectedValue == expected && result.ObservedValue == observed,
            "mismatch " + field + ", actual=" + result.Status);
        AssertNoHardwareEvidence(result);
    }

    private static void AssertAccepted(SeparatePageFaultResult result)
    {
        Require(result.IsInitialized && result.Succeeded && result.SyntheticObservationAccepted &&
            !result.ReadyDisabled && result.ComparedFieldCount == 5 &&
            result.FirstMismatchField == SeparatePageFaultField.None &&
            result.ExpectedValue == 0 && result.ObservedValue == 0,
            "synthetic observation should match only the five scoped fields");
        AssertNoHardwareEvidence(result);
    }

    private static void AssertNoHardwareEvidence(SeparatePageFaultResult result) =>
        Require(!result.IsGuestEvidence && !result.CapturedHardwareState && !result.FaultTriggered,
            "host result must not claim a captured or delivered fault");

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
