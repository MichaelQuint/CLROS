using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Diagnostics;

internal static unsafe class IDTRContinuedExecutionTests
{
    private const string TestName = "m02.012";
    private const ulong ContextAddress = 0x0020_0000;
    private const ulong StubAddress = 0x0030_0000;
    private const ulong IdtAddress = 0x0040_0000;
    private const ushort FirmwareLimit = 0x07ff;
    private const ulong FirmwareBase = 0xffff_8000_1234_5000;
    private const byte Sentinel = 0xa5;
    private const string ExpectedSha256 =
        "41B9015D93354D734EC939531790C573CA1239EBC0413FB512B39A80DFC5E91E";
    private const string ExpectedStubHex =
        "9C5848894140FA0F0149100F0159000F0149200F0159100F014930" +
        "A9000200007401FB9C5A4889514848B8323152544449534FC3";
    private const string HostReadyMarker =
        "CLROS_M02_012_HOST_READY contextBytes=80 stubBytes=52 " +
        "idtrBytes=10 tableBytes=4096 limit=4095 lidtPlan=2 sidtPlan=3 " +
        "cliPlan=1 stiPlan=1 returnCookie=4F53494454523132 " +
        "readBack=true guestExecution=false idtrLoaded=false " +
        "idtrRestored=false continuedExecution=false faultTriggered=false " +
        "postExitBootServices=false";

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase("value and layout contracts are exact POD", TestValueContracts, ref failures);
        RunCase("context preparation is exact and tail safe", TestExactContext, ref failures);
        RunCase("stub bytes and frozen identities are exact", TestExactStub, ref failures);
        RunCase("every context and stub mutation is rejected", TestEveryMutation, ref failures);
        RunCase("span overloads preserve pointer contracts", TestSpanParity, ref failures);
        RunCase("range failures reject before mutation", TestAtomicRangeFailures, ref failures);
        RunCase("actual modeled and IDT overlaps reject", TestOverlapFailures, ref failures);
        RunCase("stub readback validates storage directly", TestValidateReadBackFailures, ref failures);
        RunCase("synthetic audit accepts both IF states", TestSyntheticAuditSuccess, ref failures);
        RunCase("synthetic audit rejects false evidence", TestSyntheticAuditFailures, ref failures);
        RunCase("pointer cores allocate no managed memory", TestZeroAllocation, ref failures);
        RunCase("exact source compiles without the BCL", TestNoStdLibCompile, ref failures);
        RunCase("source boundary stays host safe", TestSourceBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + TestName + " host-tests failures=" + failures);
            throw new InvalidOperationException(
                "M02-012 IDTR continued-execution host tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
        Console.WriteLine("TEST_PASS " + TestName);
    }

    private static void RunCase(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run(TestName + " " + name, () =>
        {
            try
            {
                if (test())
                {
                    return true;
                }
                Console.WriteLine("M02_012_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "M02_012_CASE_FAIL " + Normalize(name) + " " +
                    exception.GetType().Name + ": " + exception.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestValueContracts()
    {
        IDTRContinuedExecutionPlanResult emptyPlan = default;
        IDTRContinuedExecutionAuditResult emptyAudit = default;

        return Enum.GetUnderlyingType(typeof(IDTRContinuedExecutionOperation)) == typeof(byte)
            && Enum.GetUnderlyingType(typeof(IDTRContinuedExecutionStatus)) == typeof(byte)
            && EnumValuesAreSequential<IDTRContinuedExecutionOperation>(5)
            && EnumValuesAreSequential<IDTRContinuedExecutionStatus>(41)
            && Unsafe.SizeOf<IDTRContinuedExecutionContext>() == 80
            && !RuntimeHelpers.IsReferenceOrContainsReferences<IDTRContinuedExecutionContext>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<IDTRContinuedExecutionPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<IDTRContinuedExecutionAuditResult>()
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.RequestedIdtr)).ToInt32() == 0
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.Reserved0)).ToInt32() == 10
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.PreviousIdtr)).ToInt32() == 16
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.Reserved1)).ToInt32() == 26
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.LoadedIdtr)).ToInt32() == 32
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.Reserved2)).ToInt32() == 42
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.RestoredIdtr)).ToInt32() == 48
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.Reserved3)).ToInt32() == 58
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.RflagsBefore)).ToInt32() == 64
            && Marshal.OffsetOf<IDTRContinuedExecutionContext>(
                nameof(IDTRContinuedExecutionContext.RflagsAfter)).ToInt32() == 72
            && IDTRContinuedExecution.IdtrByteCount == 10
            && IDTRContinuedExecution.IdtTableByteCount == 4096
            && IDTRContinuedExecution.IdtrLimit == 4095
            && IDTRContinuedExecution.ContextByteCount == 80
            && IDTRContinuedExecution.StubByteCount == 52
            && IDTRContinuedExecution.ContextAlignment == 16
            && IDTRContinuedExecution.StubAlignment == 16
            && IDTRContinuedExecution.IdtAlignment == 4096
            && IDTRContinuedExecution.ExecutionCookie == 0x4f53_4944_5452_3132UL
            && IDTRContinuedExecution.StubFnv1A64 == 0xaff5_9d64_80de_7f3cUL
            && IDTRContinuedExecution.AuditedInstructionCount == 17
            && IDTRContinuedExecution.LidtInstructionCount == 2
            && IDTRContinuedExecution.SidtInstructionCount == 3
            && IDTRContinuedExecution.CliInstructionCount == 1
            && IDTRContinuedExecution.StiInstructionCount == 1
            && IDTRContinuedExecution.ReturnInstructionCount == 1
            && IDTRContinuedExecution.UsesWindowsX64Abi
            && IDTRContinuedExecution.ContextPassedInRcx
            && IDTRContinuedExecution.UsesOnlyVolatileRegisters
            && IDTRContinuedExecution.UsesBalancedStackOperations
            && !IDTRContinuedExecution.HasCalls
            && IDTRContinuedExecution.HasReturns
            && !IDTRContinuedExecution.HasExternalRelocations
            && IDTRContinuedExecution.RestoresPreviousIdtrBeforeReturn
            && IDTRContinuedExecution.RestoresInterruptFlagBeforeReturn
            && !IDTRContinuedExecution.HostTestsInvokeBytes
            && !emptyPlan.IsInitialized
            && emptyPlan.Operation == IDTRContinuedExecutionOperation.None
            && emptyPlan.Status == IDTRContinuedExecutionStatus.Uninitialized
            && emptyPlan.FirstMismatchOffset == -1
            && !emptyPlan.Succeeded
            && !emptyAudit.IsInitialized
            && emptyAudit.Operation == IDTRContinuedExecutionOperation.None
            && emptyAudit.Status == IDTRContinuedExecutionStatus.Uninitialized
            && emptyAudit.FirstMismatchOffset == -1
            && !emptyAudit.Succeeded;
    }

    private static bool TestExactContext()
    {
        byte* allocation = AllocateAligned(96, 16);
        try
        {
            Fill(allocation, 96, Sentinel);
            IDTRContinuedExecutionPlanResult result =
                IDTRContinuedExecution.PrepareContext(
                    allocation,
                    ContextAddress,
                    96,
                    IdtAddress);

            Span<byte> expectedIdtr = stackalloc byte[10];
            IdtrDescriptorResult descriptor = IdtrDescriptor.Create(
                IdtAddress,
                tableByteLength: 4096);
            if (!descriptor.Succeeded ||
                !descriptor.Descriptor.TryWrite(expectedIdtr))
            {
                return false;
            }

            return result.ContextPrepared
                && result.Operation == IDTRContinuedExecutionOperation.PrepareContext
                && result.Status == IDTRContinuedExecutionStatus.Success
                && result.ModeledContextAddress == ContextAddress
                && result.ModeledStubAddress == 0
                && result.OwnedIdtAddress == IdtAddress
                && result.ContextCapacity == 96
                && result.ContextBytesWritten == 80
                && result.ContextBytesCompared == 80
                && result.FirstMismatchOffset == -1
                && new ReadOnlySpan<byte>(allocation, 10).SequenceEqual(expectedIdtr)
                && IsAll(allocation + 10, 70, 0)
                && IsAll(allocation + 80, 16, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(allocation);
        }
    }

    private static bool TestExactStub()
    {
        byte* context = AllocateAligned(96, 16);
        byte* stub = AllocateAligned(64, 16);
        try
        {
            Fill(context, 96, Sentinel);
            Fill(stub, 64, Sentinel);
            if (!IDTRContinuedExecution.PrepareContext(
                    context, ContextAddress, 96, IdtAddress).ContextPrepared)
            {
                return false;
            }

            IDTRContinuedExecutionPlanResult result =
                IDTRContinuedExecution.EmitStub(
                    stub,
                    StubAddress,
                    64,
                    context,
                    ContextAddress,
                    96,
                    IdtAddress);
            byte[] expected = Convert.FromHexString(ExpectedStubHex);
            ReadOnlySpan<byte> observed = new(stub, 52);
            ulong fnv = IndependentFnv1A64(observed);
            string sha256 = Convert.ToHexString(SHA256.HashData(observed));

            return result.StubEmitted
                && result.Operation == IDTRContinuedExecutionOperation.EmitStub
                && result.Status == IDTRContinuedExecutionStatus.Success
                && result.ContextBytesCompared == 80
                && result.StubBytesWritten == 52
                && result.StubBytesCompared == 52
                && result.StubFingerprint == 0xaff5_9d64_80de_7f3cUL
                && observed.SequenceEqual(expected)
                && fnv == 0xaff5_9d64_80de_7f3cUL
                && sha256 == ExpectedSha256
                && IsAll(stub + 52, 12, Sentinel)
                && stub[0] == 0x9c
                && stub[6] == 0xfa
                && CountSequence(observed, [0x0f, 0x01, 0x59]) == 2
                && CountSequence(observed, [0x0f, 0x01, 0x49]) == 3
                && stub[34] == 0xfb
                && stub[51] == 0xc3
                && ReadU64(stub + 43) == 0x4f53_4944_5452_3132UL;
        }
        finally
        {
            NativeMemory.AlignedFree(stub);
            NativeMemory.AlignedFree(context);
        }
    }

    private static bool TestEveryMutation()
    {
        byte* context = AllocateAligned(96, 16);
        byte* stub = AllocateAligned(64, 16);
        byte* scratch = AllocateAligned(64, 16);
        try
        {
            if (!PrepareAndEmit(context, 96, stub, 64))
            {
                return false;
            }

            for (int offset = 0; offset < 80; offset++)
            {
                Fill(scratch, 64, Sentinel);
                context[offset] ^= 0xff;
                IDTRContinuedExecutionPlanResult rejected =
                    IDTRContinuedExecution.EmitStub(
                        scratch, StubAddress, 64,
                        context, ContextAddress, 96, IdtAddress);
                context[offset] ^= 0xff;
                if (rejected.Status !=
                        IDTRContinuedExecutionStatus.ContextByteMismatch ||
                    rejected.FirstMismatchOffset != offset ||
                    rejected.ContextBytesCompared != offset + 1 ||
                    rejected.DestinationMutated ||
                    !IsAll(scratch, 64, Sentinel))
                {
                    return false;
                }
            }

            for (int offset = 0; offset < 52; offset++)
            {
                stub[offset] ^= 0xff;
                IDTRContinuedExecutionPlanResult rejected =
                    IDTRContinuedExecution.ValidateStubReadBack(
                        stub, StubAddress, 64,
                        context, ContextAddress, 96, IdtAddress);
                stub[offset] ^= 0xff;
                if (rejected.Status !=
                        IDTRContinuedExecutionStatus.StubByteMismatch ||
                    rejected.FirstMismatchOffset != offset ||
                    rejected.StubBytesCompared != offset + 1 ||
                    rejected.DestinationMutated)
                {
                    return false;
                }
            }

            return IDTRContinuedExecution.ValidateStubReadBack(
                stub, StubAddress, 64,
                context, ContextAddress, 96, IdtAddress).ReadBackValidated;
        }
        finally
        {
            NativeMemory.AlignedFree(scratch);
            NativeMemory.AlignedFree(stub);
            NativeMemory.AlignedFree(context);
        }
    }

    private static bool TestSpanParity()
    {
        byte* pointerContext = AllocateAligned(96, 16);
        byte* spanContext = AllocateAligned(96, 16);
        byte* pointerStub = AllocateAligned(64, 16);
        byte* spanStub = AllocateAligned(64, 16);
        try
        {
            Fill(pointerContext, 96, Sentinel);
            Fill(spanContext, 96, Sentinel);
            Fill(pointerStub, 64, Sentinel);
            Fill(spanStub, 64, Sentinel);
            Span<byte> contextSpan = new(spanContext, 96);
            Span<byte> stubSpan = new(spanStub, 64);

            IDTRContinuedExecutionPlanResult pointerPrepared =
                IDTRContinuedExecution.PrepareContext(
                    pointerContext, ContextAddress, 96, IdtAddress);
            IDTRContinuedExecutionPlanResult spanPrepared =
                IDTRContinuedExecution.PrepareContext(
                    contextSpan, ContextAddress, IdtAddress);
            IDTRContinuedExecutionPlanResult pointerEmitted =
                IDTRContinuedExecution.EmitStub(
                    pointerStub, StubAddress, 64,
                    pointerContext, ContextAddress, 96, IdtAddress);
            IDTRContinuedExecutionPlanResult spanEmitted =
                IDTRContinuedExecution.EmitStub(
                    stubSpan, StubAddress,
                    contextSpan, ContextAddress, IdtAddress);
            IDTRContinuedExecutionPlanResult pointerValidated =
                IDTRContinuedExecution.ValidateStubReadBack(
                    pointerStub, StubAddress, 64,
                    pointerContext, ContextAddress, 96, IdtAddress);
            IDTRContinuedExecutionPlanResult spanValidated =
                IDTRContinuedExecution.ValidateStubReadBack(
                    stubSpan, StubAddress,
                    contextSpan, ContextAddress, IdtAddress);

            pointerStub[17] ^= 0xff;
            spanStub[17] ^= 0xff;
            IDTRContinuedExecutionPlanResult pointerRejected =
                IDTRContinuedExecution.ValidateStubReadBack(
                    pointerStub, StubAddress, 64,
                    pointerContext, ContextAddress, 96, IdtAddress);
            IDTRContinuedExecutionPlanResult spanRejected =
                IDTRContinuedExecution.ValidateStubReadBack(
                    stubSpan, StubAddress,
                    contextSpan, ContextAddress, IdtAddress);
            pointerStub[17] ^= 0xff;
            spanStub[17] ^= 0xff;

            WriteSuccessfulExecution(pointerContext, entryIfSet: true);
            WriteSuccessfulExecution(spanContext, entryIfSet: true);
            IDTRContinuedExecutionAuditResult pointerAudited =
                IDTRContinuedExecution.AuditExecution(
                    pointerContext, ContextAddress, 96, IdtAddress,
                    IDTRContinuedExecution.ExecutionCookie);
            IDTRContinuedExecutionAuditResult spanAudited =
                IDTRContinuedExecution.AuditExecution(
                    contextSpan, ContextAddress, IdtAddress,
                    IDTRContinuedExecution.ExecutionCookie);

            return pointerPrepared.ContextPrepared
                && spanPrepared.ContextPrepared
                && SamePlan(pointerPrepared, spanPrepared)
                && SameBytes(pointerContext, spanContext, 96)
                && pointerEmitted.StubEmitted
                && spanEmitted.StubEmitted
                && SamePlan(pointerEmitted, spanEmitted)
                && SameBytes(pointerStub, spanStub, 64)
                && pointerValidated.ReadBackValidated
                && spanValidated.ReadBackValidated
                && SamePlan(pointerValidated, spanValidated)
                && pointerRejected.Status ==
                    IDTRContinuedExecutionStatus.StubByteMismatch
                && SamePlan(pointerRejected, spanRejected)
                && pointerAudited.Succeeded
                && spanAudited.Succeeded
                && SameAudit(pointerAudited, spanAudited);
        }
        finally
        {
            NativeMemory.AlignedFree(spanStub);
            NativeMemory.AlignedFree(pointerStub);
            NativeMemory.AlignedFree(spanContext);
            NativeMemory.AlignedFree(pointerContext);
        }
    }

    private static bool TestAtomicRangeFailures()
    {
        byte* context = AllocateAligned(96, 16);
        byte* stub = AllocateAligned(64, 16);
        try
        {
            Fill(context, 96, Sentinel);
            Fill(stub, 64, Sentinel);
            if (!ExpectPrepareStatus(null, ContextAddress, 80, IdtAddress,
                    IDTRContinuedExecutionStatus.MissingContext)
                || !ExpectPrepareStatus(context, ContextAddress, 79, IdtAddress,
                    IDTRContinuedExecutionStatus.ContextCapacityTooSmall)
                || !ExpectPrepareStatus(context + 1, ContextAddress, 80, IdtAddress,
                    IDTRContinuedExecutionStatus.MisalignedContextPointer)
                || !ExpectPrepareStatus((byte*)0x0000_8000_0000_0000UL,
                    ContextAddress, 80, IdtAddress,
                    IDTRContinuedExecutionStatus.NonCanonicalContextPointer)
                || !ExpectPrepareStatus((byte*)0xffff_ffff_ffff_fff0UL,
                    ContextAddress, 80, IdtAddress,
                    IDTRContinuedExecutionStatus.ContextPointerSpanOverflow)
                || !ExpectPrepareStatus((byte*)0x0000_7fff_ffff_ffc0UL,
                    ContextAddress, 80, IdtAddress,
                    IDTRContinuedExecutionStatus.NonCanonicalContextPointerSpan)
                || !ExpectPrepareStatus(context, 0, 80, IdtAddress,
                    IDTRContinuedExecutionStatus.NullContextAddress)
                || !ExpectPrepareStatus(context, ContextAddress + 1, 80, IdtAddress,
                    IDTRContinuedExecutionStatus.MisalignedContextAddress)
                || !ExpectPrepareStatus(context, 0x0000_8000_0000_0000UL,
                    80, IdtAddress,
                    IDTRContinuedExecutionStatus.NonCanonicalContextAddress)
                || !ExpectPrepareStatus(context, 0xffff_ffff_ffff_fff0UL,
                    80, IdtAddress,
                    IDTRContinuedExecutionStatus.ContextAddressSpanOverflow)
                || !ExpectPrepareStatus(context, 0x0000_7fff_ffff_ffc0UL,
                    80, IdtAddress,
                    IDTRContinuedExecutionStatus.NonCanonicalContextAddressSpan)
                || !ExpectPrepareStatus(context, ContextAddress, 80, 0,
                    IDTRContinuedExecutionStatus.NullIdtAddress)
                || !ExpectPrepareStatus(context, ContextAddress, 80, IdtAddress + 1,
                    IDTRContinuedExecutionStatus.MisalignedIdtAddress)
                || !ExpectPrepareStatus(context, ContextAddress, 80,
                    0x0000_8000_0000_0000UL,
                    IDTRContinuedExecutionStatus.NonCanonicalIdtAddress)
                || !ExpectPrepareStatus(context, ContextAddress, 80,
                    0xffff_ffff_ffff_f000UL,
                    IDTRContinuedExecutionStatus.IdtAddressSpanOverflow)
                || !IsAll(context, 96, Sentinel))
            {
                return false;
            }

            if (!IDTRContinuedExecution.PrepareContext(
                    context, ContextAddress, 96, IdtAddress).ContextPrepared)
            {
                return false;
            }

            return ExpectEmitStatus(null, StubAddress, 52, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.MissingStub, stub)
                && ExpectEmitStatus(stub, StubAddress, 51, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.StubCapacityTooSmall, stub)
                && ExpectEmitStatus(stub + 1, StubAddress, 52, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.MisalignedStubPointer, stub)
                && ExpectEmitStatus((byte*)0x0000_8000_0000_0000UL,
                    StubAddress, 52, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.NonCanonicalStubPointer, stub)
                && ExpectEmitStatus((byte*)0xffff_ffff_ffff_fff0UL,
                    StubAddress, 52, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.StubPointerSpanOverflow, stub)
                && ExpectEmitStatus((byte*)0x0000_7fff_ffff_fff0UL,
                    StubAddress, 52, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.NonCanonicalStubPointerSpan, stub)
                && ExpectEmitStatus(stub, 0, 64, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.NullStubAddress, stub)
                && ExpectEmitStatus(stub, StubAddress + 1, 64, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.MisalignedStubAddress, stub)
                && ExpectEmitStatus(stub, 0x0000_8000_0000_0000UL,
                    64, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.NonCanonicalStubAddress, stub)
                && ExpectEmitStatus(stub, 0xffff_ffff_ffff_fff0UL,
                    64, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.StubAddressSpanOverflow, stub)
                && ExpectEmitStatus(stub, 0x0000_7fff_ffff_fff0UL,
                    64, context, ContextAddress, 96,
                    IDTRContinuedExecutionStatus.NonCanonicalStubAddressSpan, stub);
        }
        finally
        {
            NativeMemory.AlignedFree(stub);
            NativeMemory.AlignedFree(context);
        }
    }

    private static bool TestOverlapFailures()
    {
        byte* storage = AllocateAligned(192, 16);
        byte* separate = AllocateAligned(64, 16);
        try
        {
            byte* context = storage;
            byte* overlappingStub = storage + 16;
            if (!IDTRContinuedExecution.PrepareContext(
                    context, ContextAddress, 80, IdtAddress).ContextPrepared)
            {
                return false;
            }

            Fill(separate, 64, Sentinel);
            IDTRContinuedExecutionPlanResult actual =
                IDTRContinuedExecution.EmitStub(
                    overlappingStub, StubAddress, 52,
                    context, ContextAddress, 80, IdtAddress);
            IDTRContinuedExecutionPlanResult modeled =
                IDTRContinuedExecution.EmitStub(
                    separate, ContextAddress + 16, 52,
                    context, ContextAddress, 80, IdtAddress);
            IDTRContinuedExecutionPlanResult idtContext =
                IDTRContinuedExecution.PrepareContext(
                    context, IdtAddress, 80, IdtAddress);
            IDTRContinuedExecutionPlanResult idtStub =
                IDTRContinuedExecution.EmitStub(
                    separate, IdtAddress, 52,
                    context, ContextAddress, 80, IdtAddress);
            IDTRContinuedExecutionPlanResult adjacent =
                IDTRContinuedExecution.EmitStub(
                    separate, ContextAddress + 80, 52,
                    context, ContextAddress, 80, IdtAddress);

            return actual.Status ==
                    IDTRContinuedExecutionStatus.StubOverlapsContextPointer
                && modeled.Status ==
                    IDTRContinuedExecutionStatus.StubOverlapsContextAddress
                && idtContext.Status ==
                    IDTRContinuedExecutionStatus.IdtOverlapsContextAddress
                && idtStub.Status ==
                    IDTRContinuedExecutionStatus.IdtOverlapsStubAddress
                && adjacent.StubEmitted;
        }
        finally
        {
            NativeMemory.AlignedFree(separate);
            NativeMemory.AlignedFree(storage);
        }
    }

    private static bool TestValidateReadBackFailures()
    {
        byte* context = AllocateAligned(96, 16);
        byte* stub = AllocateAligned(64, 16);
        try
        {
            if (!PrepareAndEmit(context, 96, stub, 64))
            {
                return false;
            }

            return ExpectValidateStatus(
                    null,
                    StubAddress,
                    52,
                    context,
                    ContextAddress,
                    96,
                    IDTRContinuedExecutionStatus.MissingStub)
                && ExpectValidateStatus(
                    stub,
                    StubAddress,
                    51,
                    context,
                    ContextAddress,
                    96,
                    IDTRContinuedExecutionStatus.StubCapacityTooSmall)
                && ExpectValidateStatus(
                    stub,
                    ContextAddress + 16,
                    52,
                    context,
                    ContextAddress,
                    96,
                    IDTRContinuedExecutionStatus.StubOverlapsContextAddress)
                && ExpectValidateStatus(
                    stub,
                    IdtAddress,
                    52,
                    context,
                    ContextAddress,
                    96,
                    IDTRContinuedExecutionStatus.IdtOverlapsStubAddress)
                && ExpectValidateStatus(
                    stub,
                    0x0000_7fff_ffff_fff0UL,
                    64,
                    context,
                    ContextAddress,
                    96,
                    IDTRContinuedExecutionStatus.NonCanonicalStubAddressSpan);
        }
        finally
        {
            NativeMemory.AlignedFree(stub);
            NativeMemory.AlignedFree(context);
        }
    }

    private static bool TestSyntheticAuditSuccess()
    {
        byte* context = AllocateAligned(96, 16);
        try
        {
            if (!IDTRContinuedExecution.PrepareContext(
                    context, ContextAddress, 96, IdtAddress).ContextPrepared)
            {
                return false;
            }

            WriteSuccessfulExecution(context, entryIfSet: false);
            IDTRContinuedExecutionAuditResult disabled =
                IDTRContinuedExecution.AuditExecution(
                    context, ContextAddress, 96, IdtAddress,
                    IDTRContinuedExecution.ExecutionCookie);
            WriteSuccessfulExecution(context, entryIfSet: true);
            WriteU64(context + IDTRContinuedExecution.RflagsBeforeOffset, 0x2d7);
            WriteU64(context + IDTRContinuedExecution.RflagsAfterOffset, 0x202);
            IDTRContinuedExecutionAuditResult enabled =
                IDTRContinuedExecution.AuditExecution(
                    context, ContextAddress, 96, IdtAddress,
                    IDTRContinuedExecution.ExecutionCookie);

            return disabled.Succeeded
                && disabled.Status == IDTRContinuedExecutionStatus.Success
                && disabled.TransitionObserved
                && disabled.OwnedIdtrLoaded
                && disabled.FirmwareIdtrRestored
                && disabled.InterruptFlagRestored
                && disabled.ContinuedExecution
                && disabled.PreviousLimit == FirmwareLimit
                && disabled.PreviousBase == FirmwareBase
                && disabled.LoadedLimit == 4095
                && disabled.LoadedBase == IdtAddress
                && disabled.RestoredLimit == FirmwareLimit
                && disabled.RestoredBase == FirmwareBase
                && enabled.Succeeded
                && enabled.RflagsBefore == 0x2d7
                && enabled.RflagsAfter == 0x202;
        }
        finally
        {
            NativeMemory.AlignedFree(context);
        }
    }

    private static bool TestSyntheticAuditFailures()
    {
        byte* context = AllocateAligned(96, 16);
        try
        {
            if (!IDTRContinuedExecution.PrepareContext(
                    context, ContextAddress, 96, IdtAddress).ContextPrepared)
            {
                return false;
            }

            WriteSuccessfulExecution(context, entryIfSet: true);
            if (!ExpectAuditStatus(context, IDTRContinuedExecution.ExecutionCookie ^ 1,
                    IDTRContinuedExecutionStatus.ExecutionCookieMismatch))
            {
                return false;
            }

            for (int offset = 0; offset < 10; offset++)
            {
                context[IDTRContinuedExecution.RequestedIdtrOffset + offset] ^= 0xff;
                bool rejected = ExpectAuditStatus(
                    context,
                    IDTRContinuedExecution.ExecutionCookie,
                    IDTRContinuedExecutionStatus.ContextByteMismatch);
                context[IDTRContinuedExecution.RequestedIdtrOffset + offset] ^= 0xff;
                if (!rejected)
                {
                    return false;
                }
            }

            for (int group = 0; group < 4; group++)
            {
                int reservedStart = (group * 16) + 10;
                for (int offset = 0; offset < 6; offset++)
                {
                    context[reservedStart + offset] = 1;
                    bool rejected = ExpectAuditStatus(
                        context,
                        IDTRContinuedExecution.ExecutionCookie,
                        IDTRContinuedExecutionStatus.ContextByteMismatch);
                    context[reservedStart + offset] = 0;
                    if (!rejected)
                    {
                        return false;
                    }
                }
            }

            Copy(context + IDTRContinuedExecution.RequestedIdtrOffset,
                context + IDTRContinuedExecution.PreviousIdtrOffset, 10);
            Copy(context + IDTRContinuedExecution.PreviousIdtrOffset,
                context + IDTRContinuedExecution.RestoredIdtrOffset, 10);
            if (!ExpectAuditStatus(context, IDTRContinuedExecution.ExecutionCookie,
                    IDTRContinuedExecutionStatus.NoIdtrTransitionObserved))
            {
                return false;
            }

            WriteSuccessfulExecution(context, entryIfSet: true);
            for (int offset = 0; offset < 10; offset++)
            {
                context[IDTRContinuedExecution.LoadedIdtrOffset + offset] ^= 0xff;
                bool rejected = ExpectAuditStatus(
                    context,
                    IDTRContinuedExecution.ExecutionCookie,
                    IDTRContinuedExecutionStatus.LoadedIdtrMismatch);
                context[IDTRContinuedExecution.LoadedIdtrOffset + offset] ^= 0xff;
                if (!rejected)
                {
                    return false;
                }
            }

            for (int offset = 0; offset < 10; offset++)
            {
                context[IDTRContinuedExecution.RestoredIdtrOffset + offset] ^= 0xff;
                bool rejected = ExpectAuditStatus(
                    context,
                    IDTRContinuedExecution.ExecutionCookie,
                    IDTRContinuedExecutionStatus.RestoredIdtrMismatch);
                context[IDTRContinuedExecution.RestoredIdtrOffset + offset] ^= 0xff;
                if (!rejected)
                {
                    return false;
                }
            }

            WriteSuccessfulExecution(context, entryIfSet: true);
            WriteU64(context + IDTRContinuedExecution.RflagsAfterOffset, 0x2);
            if (!ExpectAuditStatus(context, IDTRContinuedExecution.ExecutionCookie,
                    IDTRContinuedExecutionStatus.InterruptFlagMismatch))
            {
                return false;
            }

            WriteSuccessfulExecution(context, entryIfSet: false);
            WriteU64(context + IDTRContinuedExecution.RflagsAfterOffset, 0x202);
            if (!ExpectAuditStatus(
                    context,
                    IDTRContinuedExecution.ExecutionCookie,
                    IDTRContinuedExecutionStatus.InterruptFlagMismatch))
            {
                return false;
            }

            if (!IDTRContinuedExecution.PrepareContext(
                    context, ContextAddress, 96, IdtAddress).ContextPrepared
                || !ExpectAuditStatus(
                    context,
                    IDTRContinuedExecution.ExecutionCookie,
                    IDTRContinuedExecutionStatus.LoadedIdtrMismatch))
            {
                return false;
            }

            return IDTRContinuedExecution.AuditExecution(
                    (byte*)0x0000_8000_0000_0000UL,
                    ContextAddress,
                    80,
                    IdtAddress,
                    IDTRContinuedExecution.ExecutionCookie).Status ==
                    IDTRContinuedExecutionStatus.NonCanonicalContextPointer;
        }
        finally
        {
            NativeMemory.AlignedFree(context);
        }
    }

    private static bool TestZeroAllocation()
    {
        byte* context = AllocateAligned(96, 16);
        byte* stub = AllocateAligned(64, 16);
        try
        {
            IDTRContinuedExecutionPlanResult warmPrepared =
                IDTRContinuedExecution.PrepareContext(
                    context, ContextAddress, 96, IdtAddress);
            IDTRContinuedExecutionPlanResult warmEmitted =
                IDTRContinuedExecution.EmitStub(
                    stub, StubAddress, 64,
                    context, ContextAddress, 96, IdtAddress);
            IDTRContinuedExecutionPlanResult warmValidated =
                IDTRContinuedExecution.ValidateStubReadBack(
                    stub, StubAddress, 64,
                    context, ContextAddress, 96, IdtAddress);
            WriteSuccessfulExecution(context, entryIfSet: true);
            IDTRContinuedExecutionAuditResult warmAudited =
                IDTRContinuedExecution.AuditExecution(
                    context, ContextAddress, 96, IdtAddress,
                    IDTRContinuedExecution.ExecutionCookie);
            if (!warmPrepared.ContextPrepared
                || !warmEmitted.StubEmitted
                || !warmValidated.ReadBackValidated
                || !warmAudited.Succeeded)
            {
                return false;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            IDTRContinuedExecutionPlanResult lastPrepared = default;
            IDTRContinuedExecutionPlanResult lastEmitted = default;
            IDTRContinuedExecutionPlanResult lastValidated = default;
            IDTRContinuedExecutionAuditResult lastAudited = default;
            for (int index = 0; index < 512; index++)
            {
                lastPrepared = IDTRContinuedExecution.PrepareContext(
                    context, ContextAddress, 96, IdtAddress);
                lastEmitted = IDTRContinuedExecution.EmitStub(
                    stub, StubAddress, 64,
                    context, ContextAddress, 96, IdtAddress);
                lastValidated = IDTRContinuedExecution.ValidateStubReadBack(
                    stub, StubAddress, 64,
                    context, ContextAddress, 96, IdtAddress);
                WriteSuccessfulExecution(context, entryIfSet: true);
                lastAudited = IDTRContinuedExecution.AuditExecution(
                    context, ContextAddress, 96, IdtAddress,
                    IDTRContinuedExecution.ExecutionCookie);
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            return lastPrepared.ContextPrepared
                && lastEmitted.StubEmitted
                && lastValidated.ReadBackValidated
                && lastAudited.Succeeded
                && after == before;
        }
        finally
        {
            NativeMemory.AlignedFree(stub);
            NativeMemory.AlignedFree(context);
        }
    }

    private static bool TestNoStdLibCompile()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m02-012-nostdlib-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibShim.cs"),
                NoStdLibShimSource,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            string productionSource = Path.Combine(
                repositoryRoot,
                @"src\OS.Kernel\Diagnostics\IDTRContinuedExecution.cs");
            File.WriteAllText(
                Path.Combine(workspace, "M02.012.NoStdLib.csproj"),
                NoStdLibProjectSource.Replace(
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
            start.ArgumentList.Add("M02.012.NoStdLib.csproj");
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
                    "Failed to start focused M02-012 no-stdlib build.");
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
                Console.WriteLine("M02_012_NOSTDLIB_FAIL " +
                    output.Replace('\r', ' ').Replace('\n', ' ').Trim());
                return false;
            }

            return File.Exists(Path.Combine(
                workspace, "out", "CLROS.M02.IDTRContinuedExecution.dll"));
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static bool TestSourceBoundary()
    {
        string source = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            @"src\OS.Kernel\Diagnostics\IDTRContinuedExecution.cs"));
        string pointerCore = RemoveNoStdLibHostBlocks(source);
        string[] forbidden =
        [
            "throw ",
            "new byte[",
            "stackalloc",
            "delegate*",
            "Marshal.",
            "NativeMemory",
            "VirtualProtect",
            "mprotect",
            "DllImport",
            "EfiEntry",
            "BootSequence",
            "Probes",
            "Console.",
            "CLROS_M02_012_HOST_READY",
            "CLROS_M02_012_READY",
            "TEST_PASS m02.012"
        ];

        for (int index = 0; index < forbidden.Length; index++)
        {
            if (source.Contains(forbidden[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return source.Contains("#if !NOSTDLIB_EFI", StringComparison.Ordinal)
            && source.Contains("Span<byte>", StringComparison.Ordinal)
            && source.Contains("ReadOnlySpan<byte>", StringComparison.Ordinal)
            && !pointerCore.Contains("Span<", StringComparison.Ordinal)
            && source.Contains("HostTestsInvokeBytes = false", StringComparison.Ordinal)
            && source.Contains("HasExternalRelocations = false", StringComparison.Ordinal)
            && source.Contains("StubFnv1A64 = 0xaff5_9d64_80de_7f3cUL", StringComparison.Ordinal)
            && source.Contains("ExecutionCookie = 0x4f53_4944_5452_3132UL", StringComparison.Ordinal);
    }

    private static bool PrepareAndEmit(
        byte* context,
        ulong contextCapacity,
        byte* stub,
        ulong stubCapacity)
    {
        return IDTRContinuedExecution.PrepareContext(
                context, ContextAddress, contextCapacity, IdtAddress).ContextPrepared
            && IDTRContinuedExecution.EmitStub(
                stub, StubAddress, stubCapacity,
                context, ContextAddress, contextCapacity, IdtAddress).StubEmitted;
    }

    private static bool ExpectPrepareStatus(
        byte* context,
        ulong modeledContextAddress,
        ulong capacity,
        ulong idtAddress,
        IDTRContinuedExecutionStatus expected)
    {
        IDTRContinuedExecutionPlanResult result =
            IDTRContinuedExecution.PrepareContext(
                context, modeledContextAddress, capacity, idtAddress);
        return result.IsInitialized
            && result.Operation == IDTRContinuedExecutionOperation.PrepareContext
            && result.Status == expected
            && !result.DestinationMutated
            && !result.Succeeded;
    }

    private static bool ExpectEmitStatus(
        byte* stub,
        ulong modeledStubAddress,
        ulong stubCapacity,
        byte* context,
        ulong modeledContextAddress,
        ulong contextCapacity,
        IDTRContinuedExecutionStatus expected,
        byte* validStubStorage)
    {
        Fill(validStubStorage, 64, Sentinel);
        IDTRContinuedExecutionPlanResult result =
            IDTRContinuedExecution.EmitStub(
                stub, modeledStubAddress, stubCapacity,
                context, modeledContextAddress, contextCapacity, IdtAddress);
        return result.IsInitialized
            && result.Operation == IDTRContinuedExecutionOperation.EmitStub
            && result.Status == expected
            && !result.DestinationMutated
            && !result.Succeeded
            && IsAll(validStubStorage, 64, Sentinel);
    }

    private static bool ExpectValidateStatus(
        byte* source,
        ulong modeledStubAddress,
        ulong sourceCapacity,
        byte* context,
        ulong modeledContextAddress,
        ulong contextCapacity,
        IDTRContinuedExecutionStatus expected)
    {
        IDTRContinuedExecutionPlanResult result =
            IDTRContinuedExecution.ValidateStubReadBack(
                source,
                modeledStubAddress,
                sourceCapacity,
                context,
                modeledContextAddress,
                contextCapacity,
                IdtAddress);
        return result.IsInitialized
            && result.Operation ==
                IDTRContinuedExecutionOperation.ValidateStubReadBack
            && result.Status == expected
            && !result.DestinationMutated
            && !result.Succeeded;
    }

    private static bool ExpectAuditStatus(
        byte* context,
        ulong cookie,
        IDTRContinuedExecutionStatus expected)
    {
        IDTRContinuedExecutionAuditResult result =
            IDTRContinuedExecution.AuditExecution(
                context, ContextAddress, 96, IdtAddress, cookie);
        return result.IsInitialized
            && result.Operation == IDTRContinuedExecutionOperation.AuditExecution
            && result.Status == expected
            && !result.Succeeded;
    }

    private static bool SamePlan(
        IDTRContinuedExecutionPlanResult first,
        IDTRContinuedExecutionPlanResult second)
    {
        return first.IsInitialized == second.IsInitialized
            && first.Operation == second.Operation
            && first.Status == second.Status
            && first.ModeledContextAddress == second.ModeledContextAddress
            && first.ModeledStubAddress == second.ModeledStubAddress
            && first.OwnedIdtAddress == second.OwnedIdtAddress
            && first.ContextCapacity == second.ContextCapacity
            && first.StubCapacity == second.StubCapacity
            && first.ContextBytesWritten == second.ContextBytesWritten
            && first.ContextBytesCompared == second.ContextBytesCompared
            && first.StubBytesWritten == second.StubBytesWritten
            && first.StubBytesCompared == second.StubBytesCompared
            && first.FirstMismatchOffset == second.FirstMismatchOffset
            && first.ContextFingerprint == second.ContextFingerprint
            && first.StubFingerprint == second.StubFingerprint
            && first.ExpectedStubFingerprint == second.ExpectedStubFingerprint
            && first.DestinationMutated == second.DestinationMutated
            && first.ReadBackPerformed == second.ReadBackPerformed
            && first.ContextPrepared == second.ContextPrepared
            && first.StubEmitted == second.StubEmitted
            && first.ReadBackValidated == second.ReadBackValidated
            && first.Succeeded == second.Succeeded;
    }

    private static bool SameAudit(
        IDTRContinuedExecutionAuditResult first,
        IDTRContinuedExecutionAuditResult second)
    {
        return first.IsInitialized == second.IsInitialized
            && first.Operation == second.Operation
            && first.Status == second.Status
            && first.ModeledContextAddress == second.ModeledContextAddress
            && first.OwnedIdtAddress == second.OwnedIdtAddress
            && first.ContextCapacity == second.ContextCapacity
            && first.ExpectedCookie == second.ExpectedCookie
            && first.ObservedCookie == second.ObservedCookie
            && first.ContextBytesCompared == second.ContextBytesCompared
            && first.FirstMismatchOffset == second.FirstMismatchOffset
            && first.PreviousLimit == second.PreviousLimit
            && first.PreviousBase == second.PreviousBase
            && first.LoadedLimit == second.LoadedLimit
            && first.LoadedBase == second.LoadedBase
            && first.RestoredLimit == second.RestoredLimit
            && first.RestoredBase == second.RestoredBase
            && first.RflagsBefore == second.RflagsBefore
            && first.RflagsAfter == second.RflagsAfter
            && first.TransitionObserved == second.TransitionObserved
            && first.OwnedIdtrLoaded == second.OwnedIdtrLoaded
            && first.FirmwareIdtrRestored == second.FirmwareIdtrRestored
            && first.InterruptFlagRestored == second.InterruptFlagRestored
            && first.ContinuedExecution == second.ContinuedExecution
            && first.Succeeded == second.Succeeded;
    }

    private static void WriteSuccessfulExecution(byte* context, bool entryIfSet)
    {
        WriteIdtr(context + IDTRContinuedExecution.PreviousIdtrOffset,
            FirmwareLimit, FirmwareBase);
        WriteIdtr(context + IDTRContinuedExecution.LoadedIdtrOffset,
            IDTRContinuedExecution.IdtrLimit, IdtAddress);
        WriteIdtr(context + IDTRContinuedExecution.RestoredIdtrOffset,
            FirmwareLimit, FirmwareBase);
        WriteU64(context + IDTRContinuedExecution.RflagsBeforeOffset,
            entryIfSet ? 0x202UL : 0x2UL);
        WriteU64(context + IDTRContinuedExecution.RflagsAfterOffset,
            entryIfSet ? 0x202UL : 0x2UL);
    }

    private static void WriteIdtr(
        byte* destination,
        ushort limit,
        ulong baseAddress)
    {
        destination[0] = (byte)(limit & 0xff);
        destination[1] = (byte)(limit >> 8);
        WriteU64(destination + 2, baseAddress);
    }

    private static void WriteU64(byte* destination, ulong value)
    {
        for (int index = 0; index < 8; index++)
        {
            destination[index] = (byte)(value >> (index * 8));
        }
    }

    private static ulong ReadU64(byte* source)
    {
        ulong value = 0;
        for (int index = 0; index < 8; index++)
        {
            value |= (ulong)source[index] << (index * 8);
        }

        return value;
    }

    private static void Copy(byte* source, byte* destination, int length)
    {
        for (int index = 0; index < length; index++)
        {
            destination[index] = source[index];
        }
    }

    private static int CountSequence(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> pattern)
    {
        int count = 0;
        for (int offset = 0; offset <= bytes.Length - pattern.Length; offset++)
        {
            if (bytes.Slice(offset, pattern.Length).SequenceEqual(pattern))
            {
                count++;
            }
        }

        return count;
    }

    private static ulong IndependentFnv1A64(ReadOnlySpan<byte> bytes)
    {
        ulong fingerprint = 14_695_981_039_346_656_037UL;
        for (int index = 0; index < bytes.Length; index++)
        {
            fingerprint ^= bytes[index];
            fingerprint = unchecked(fingerprint * 1_099_511_628_211UL);
        }

        return fingerprint;
    }

    private static byte* AllocateAligned(int byteCount, nuint alignment)
    {
        void* allocation = NativeMemory.AlignedAlloc((nuint)byteCount, alignment);
        if (allocation == null)
        {
            throw new OutOfMemoryException("Unable to allocate M02-012 test storage.");
        }

        return (byte*)allocation;
    }

    private static void Fill(byte* destination, int length, byte value)
    {
        for (int offset = 0; offset < length; offset++)
        {
            destination[offset] = value;
        }
    }

    private static bool IsAll(byte* source, int length, byte value)
    {
        for (int offset = 0; offset < length; offset++)
        {
            if (source[offset] != value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameBytes(byte* first, byte* second, int length)
    {
        for (int offset = 0; offset < length; offset++)
        {
            if (first[offset] != second[offset])
            {
                return false;
            }
        }

        return true;
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

    private static string RemoveNoStdLibHostBlocks(string source)
    {
        const string open = "#if !NOSTDLIB_EFI";
        const string close = "#endif";
        while (true)
        {
            int start = source.IndexOf(open, StringComparison.Ordinal);
            if (start < 0)
            {
                return source;
            }

            int end = source.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0)
            {
                return source;
            }

            source = source.Remove(start, end + close.Length - start);
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

    private const string NoStdLibShimSource =
        """
        namespace System
        {
            public class Object { }
            public abstract class ValueType : Object { }
            public abstract class Enum : ValueType { }
            public abstract class Delegate : Object { }
            public abstract class MulticastDelegate : Delegate { }
            public class Type : Object { }
            public class Attribute : Object { }
            public struct Void { }
            public struct Boolean { }
            public struct Byte { }
            public struct Int32 { }
            public struct UInt16 { }
            public struct UInt64 { }
            public struct UIntPtr { }
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
            public sealed class UnsafeValueTypeAttribute : System.Attribute { }
            public sealed class FixedBufferAttribute : System.Attribute
            {
                public FixedBufferAttribute(System.Type elementType, int length) { }
            }
        }
        """;

    private const string NoStdLibProjectSource =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <AssemblyName>CLROS.M02.IDTRContinuedExecution</AssemblyName>
            <TargetFramework>net10.0</TargetFramework>
            <LangVersion>14.0</LangVersion>
            <OutputType>Library</OutputType>
            <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
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
            <Compile Include="NoStdLibShim.cs" />
            <Compile Include="__PRODUCTION_SOURCE__"
                     Link="Diagnostics\IDTRContinuedExecution.cs" />
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
