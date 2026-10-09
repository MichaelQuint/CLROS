using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static unsafe class ExistingIn8Out8StubBytesRWPagesFinalizeTests
{
    private const string TestName = "m02.002";
    private const string ReadyMarker =
        "CLROS_M02_002_READY pageBytes=4096 safeOffset=0 out8Offset=16 " +
        "in8Offset=32 fnv1a64=982E5B7E3EE58E56 " +
        "sha256=" + OS.Tooling.EfiContracts.PortIoStubPageSha256 + " " +
        "initial=RW+NX final=RO+X liveProtection=false liveExecution=false";
    private const string ExpectedSha256 =
        OS.Tooling.EfiContracts.PortIoStubPageSha256;
    private const ulong ModeledPageAddress = 0x0040_0000;
    private const ulong OtherModeledPageAddress = 0x0041_0000;
    private const ulong PublicationEpoch = 7;
    private const byte Sentinel = 0xa5;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase(
            "value contracts and constants are exact",
            TestValueContractsAndConstants,
            ref failures);
        RunCase(
            "port io pointer cores preserve host compatibility",
            TestPortIoPointerCores,
            ref failures);
        RunCase(
            "pointer page path materializes reads back and audits",
            TestPointerPagePath,
            ref failures);
        RunCase(
            "host span surface preserves the exact page",
            TestHostSpanSurface,
            ref failures);
        RunCase(
            "materialization failures are atomic",
            TestMaterializationFailuresAreAtomic,
            ref failures);
        RunCase(
            "state and page storage aliasing is rejected atomically",
            TestStateStorageAliasing,
            ref failures);
        RunCase(
            "full page readback rejects every byte mutation",
            TestFullPageReadbackRejectsEveryMutation,
            ref failures);
        RunCase(
            "readback metadata failures preserve state",
            TestReadbackMetadataFailures,
            ref failures);
        RunCase(
            "finalization rejects unsafe stale and replayed input",
            TestFinalizationFailures,
            ref failures);
        RunCase(
            "pointer state machine allocates no managed memory",
            TestPointerPathAllocatesNoManagedMemory,
            ref failures);
        RunCase(
            "source boundary excludes live execution and protection",
            TestSourceBoundary,
            ref failures);
        RunCase(
            "exact pointer closure compiles without references and runs under NOSTDLIB_EFI",
            TestNoStdLibEfiCompileClosure,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-tests failures=" + failures);
            throw new InvalidOperationException(
                "M02-002 fixed stub-page host tests failed: " + failures);
        }

        Console.WriteLine(ReadyMarker);
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
                Console.WriteLine(
                    "M02_002_CASE_FAIL " + Normalize(name));
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "M02_002_CASE_FAIL "
                    + Normalize(name)
                    + " "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestValueContractsAndConstants()
    {
        ExistingIn8Out8StubPageState defaultState = default;
        ExistingIn8Out8StubPageState initialState =
            ExistingIn8Out8StubPageState.Create();
        ExistingIn8Out8StubPageResult defaultResult = default;

        return HasSequentialByteEnum<ExistingIn8Out8StubPagePhase>(
                expectedCount: 4)
            && HasSequentialByteEnum<ExistingIn8Out8StubPageStatus>(
                expectedCount: 44)
            && ExistingIn8Out8StubPagePhase.Uninitialized == 0
            && ExistingIn8Out8StubPagePhase.Materialized
                == (ExistingIn8Out8StubPagePhase)1
            && ExistingIn8Out8StubPagePhase.ReadBackVerified
                == (ExistingIn8Out8StubPagePhase)2
            && ExistingIn8Out8StubPagePhase.FinalizationAudited
                == (ExistingIn8Out8StubPagePhase)3
            && Enum.GetUnderlyingType(typeof(PortIoOperationPlanStatus))
                == typeof(int)
            && Enum.GetUnderlyingType(typeof(PortIoStubPlanStatus))
                == typeof(int)
            && (int)PortIoOperationPlanStatus.Success == 0
            && (int)PortIoOperationPlanStatus.InvalidOperationKind == 1
            && (int)PortIoOperationPlanStatus.OutputBufferTooSmall == 2
            && (int)PortIoOperationPlanStatus.MissingDestination == 3
            && (int)PortIoStubPlanStatus.Success == 0
            && (int)PortIoStubPlanStatus.InvalidOperationKind == 1
            && (int)PortIoStubPlanStatus.OutputBufferTooSmall == 2
            && (int)PortIoStubPlanStatus.MissingDestination == 3
            && ExistingIn8Out8StubBytesRWPagesFinalize.PageSize == 4096
            && ExistingIn8Out8StubBytesRWPagesFinalize
                .RequiredPointerAlignment == 4096
            && ExistingIn8Out8StubBytesRWPagesFinalize.SafeOffset == 0
            && ExistingIn8Out8StubBytesRWPagesFinalize.SafeLength == 6
            && ExistingIn8Out8StubBytesRWPagesFinalize.Out8Offset == 16
            && ExistingIn8Out8StubBytesRWPagesFinalize.Out8Length == 7
            && ExistingIn8Out8StubBytesRWPagesFinalize.In8Offset == 32
            && ExistingIn8Out8StubBytesRWPagesFinalize.In8Length == 5
            && ExistingIn8Out8StubBytesRWPagesFinalize
                .InstructionByteCount == 18
            && ExistingIn8Out8StubBytesRWPagesFinalize
                .OccupiedExtent == 37
            && ExistingIn8Out8StubBytesRWPagesFinalize
                .SafeReturnValue == 0x4f53_4f4b
            && ExistingIn8Out8StubBytesRWPagesFinalize
                .ExpectedFnv1a64 == 0x982e_5b7e_3ee5_8e56
            && ExistingIn8Out8StubBytesRWPagesFinalize
                .ReadWriteProtection == ReadWrite
            && ExistingIn8Out8StubBytesRWPagesFinalize
                .ReadExecuteProtection == ReadExecute
            && !ExistingIn8Out8StubBytesRWPagesFinalize
                .PerformsLiveProtectionChange
            && !ExistingIn8Out8StubBytesRWPagesFinalize
                .PerformsLiveExecution
            && !ExistingIn8Out8StubBytesRWPagesFinalize
                .InvokesSafeStub
            && !ExistingIn8Out8StubBytesRWPagesFinalize
                .InvokesPortIo
            && (ulong)Unsafe.SizeOf<ExistingIn8Out8StubPageState>()
                == ExistingIn8Out8StubPageState.StructureSize
            && (ulong)Unsafe.SizeOf<ExistingIn8Out8StubPageResult>()
                == ExistingIn8Out8StubPageResult.StructureSize
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                ExistingIn8Out8StubPageState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                ExistingIn8Out8StubPageResult>()
            && !defaultState.IsInitialized
            && !defaultState.IsValid
            && initialState.IsInitialized
            && initialState.IsValid
            && initialState.Phase
                == ExistingIn8Out8StubPagePhase.Uninitialized
            && initialState.TransitionVersion == 0
            && initialState.PublicationEpoch == 0
            && initialState.ModeledPageAddress == 0
            && initialState.CurrentProtection == PageProtection.None
            && initialState.FinalizationCount == 0
            && !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && !defaultResult.ReadBackVerified
            && !defaultResult.FinalizationAudited
            && defaultResult.Status
                == ExistingIn8Out8StubPageStatus.Uninitialized
            && defaultResult.Phase
                == ExistingIn8Out8StubPagePhase.Uninitialized;
    }

    private static bool TestPortIoPointerCores()
    {
        byte* operationBytes = stackalloc byte[3];
        operationBytes[0] = Sentinel;
        operationBytes[1] = Sentinel;
        operationBytes[2] = Sentinel;
        PortIoOperationPlanResult outOperation =
            PortIoOperationPlan.Out8.Emit(operationBytes + 1, 1);
        PortIoOperationPlanResult missingOperation =
            PortIoOperationPlan.In8.Emit(null, 1);
        PortIoOperationPlanResult shortOperation =
            PortIoOperationPlan.In8.Emit(null, 0);
        PortIoOperationPlanResult invalidOperation =
            default(PortIoOperationPlan).Emit(null, 1);

        byte* stubBytes = stackalloc byte[9];
        for (int index = 0; index < 9; index++)
        {
            stubBytes[index] = Sentinel;
        }

        PortIoStubPlanResult outStub =
            PortIoStubPlan.Out8.Emit(stubBytes + 1, 7);
        PortIoStubPlanResult missingStub =
            PortIoStubPlan.In8.Emit(null, 5);
        PortIoStubPlanResult shortStub =
            PortIoStubPlan.In8.Emit(null, 4);
        PortIoStubPlanResult invalidStub =
            default(PortIoStubPlan).Emit(null, 7);

        Span<byte> hostOperation = stackalloc byte[1];
        Span<byte> hostOut8 = stackalloc byte[7];
        PortIoOperationPlanResult hostOperationResult =
            PortIoOperationPlan.Out8.Emit(hostOperation);
        PortIoStubPlanResult hostStubResult =
            PortIoStubPlan.Out8.Emit(hostOut8);

        return outOperation.Succeeded
            && operationBytes[0] == Sentinel
            && operationBytes[1] == 0xee
            && operationBytes[2] == Sentinel
            && missingOperation.Status
                == PortIoOperationPlanStatus.MissingDestination
            && shortOperation.Status
                == PortIoOperationPlanStatus.OutputBufferTooSmall
            && invalidOperation.Status
                == PortIoOperationPlanStatus.InvalidOperationKind
            && outStub.Succeeded
            && stubBytes[0] == Sentinel
            && stubBytes[8] == Sentinel
            && new ReadOnlySpan<byte>(stubBytes + 1, 7).SequenceEqual(
                new byte[] { 0x88, 0xd0, 0x66, 0x89, 0xca, 0xee, 0xc3 })
            && missingStub.Status
                == PortIoStubPlanStatus.MissingDestination
            && shortStub.Status
                == PortIoStubPlanStatus.OutputBufferTooSmall
            && invalidStub.Status
                == PortIoStubPlanStatus.InvalidOperationKind
            && hostOperationResult.Succeeded
            && hostOperation[0] == 0xee
            && hostStubResult.Succeeded
            && hostOut8.SequenceEqual(
                new byte[] { 0x88, 0xd0, 0x66, 0x89, 0xca, 0xee, 0xc3 });
    }

    private static bool TestPointerPagePath()
    {
        using AlignedPage page = new();
        page.Fill(Sentinel);
        ExistingIn8Out8StubPageState state =
            ExistingIn8Out8StubPageState.Create();

        ExistingIn8Out8StubPageResult materialized =
            MaterializeValid(page, &state);
        if (!HasMaterializedResult(materialized, state)
            || !HasExactPage(page.Span)
            || IndependentFnv1a64(page.Span)
                != ExistingIn8Out8StubBytesRWPagesFinalize
                    .ExpectedFnv1a64
            || Convert.ToHexString(SHA256.HashData(page.Span))
                != ExpectedSha256)
        {
            return false;
        }

        byte[] snapshot = page.Span.ToArray();
        ExistingIn8Out8StubPageResult readback =
            ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                page.Pointer,
                ModeledPageAddress,
                ExistingIn8Out8StubBytesRWPagesFinalize.PageSize,
                materialized,
                &state,
                expectedTransitionVersion: 1);
        if (!HasReadbackResult(readback, state)
            || !page.Span.SequenceEqual(snapshot))
        {
            return false;
        }

        ExistingIn8Out8StubPageResult finalized =
            ExistingIn8Out8StubBytesRWPagesFinalize.AuditFinalization(
                readback,
                ModeledPageAddress,
                ReadWrite,
                ReadExecute,
                executionAttemptedBeforeFinalization: false,
                &state,
                expectedTransitionVersion: 2);

        return HasFinalizedResult(finalized, state)
            && page.Span.SequenceEqual(snapshot);
    }

    private static bool TestHostSpanSurface()
    {
        using AlignedPage page = new();
        page.Fill(Sentinel);
        ExistingIn8Out8StubPageState state =
            ExistingIn8Out8StubPageState.Create();

        ExistingIn8Out8StubPageResult materialized =
            ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(
                page.Span,
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
                page.ReadOnlySpan,
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

        return HasExactPage(page.Span)
            && materialized.Succeeded
            && readback.ReadBackVerified
            && finalized.FinalizationAudited
            && state.IsValid
            && state.FinalizationCount == 1;
    }

    private static bool TestMaterializationFailuresAreAtomic()
    {
        using AlignedPage page = new();

        if (!ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.StaleStateVersion,
                expectedTransitionVersion: 1)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidDestinationLength,
                destinationCapacity: 4095)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidDestinationLength,
                destinationCapacity: 4097)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.MissingDestination,
                destination: null)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.MisalignedDestination,
                destination: page.Pointer + 1)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.DestinationSpanOverflow,
                destination: (byte*)0xffff_ffff_ffff_f000UL)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidModeledPageAddress,
                modeledAddress: ModeledPageAddress + 1)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.ModeledPageSpanOverflow,
                modeledAddress: 0xffff_ffff_ffff_f000UL)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.LayoutOverflow,
                safeOffset: ulong.MaxValue)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.LayoutOutsidePage,
                safeOffset: 4091,
                out8Offset: 16,
                in8Offset: 32)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.LayoutOverlap,
                out8Offset: 4)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidSafeOffset,
                safeOffset: 64,
                out8Offset: 80,
                in8Offset: 96)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidOut8Offset,
                out8Offset: 64,
                in8Offset: 96)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidIn8Offset,
                in8Offset: 64)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidOut8Plan,
                out8Plan: new PortIoStubPlan(PortIoOperationKind.None))
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidOut8Plan,
                out8Plan: PortIoStubPlan.In8)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidIn8Plan,
                in8Plan: new PortIoStubPlan(PortIoOperationKind.None))
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidIn8Plan,
                in8Plan: PortIoStubPlan.Out8)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidInitialProtection,
                initialProtection: PageProtection.Read)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.WritableExecutableRejected,
                initialProtection: ReadWriteExecute)
            || !ExpectMaterializeFailure(
                page,
                ExistingIn8Out8StubPageStatus.InvalidPublicationEpoch,
                publicationEpoch: 0))
        {
            return false;
        }

        page.Fill(Sentinel);
        ExistingIn8Out8StubPageResult missingState =
            ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(
                page.Pointer,
                ModeledPageAddress,
                4096,
                0,
                16,
                32,
                PortIoStubPlan.Out8,
                PortIoStubPlan.In8,
                ReadWrite,
                PublicationEpoch,
                state: null,
                expectedTransitionVersion: 0);

        byte* stateStorage = stackalloc byte[64];
        ExistingIn8Out8StubPageResult misalignedState =
            ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(
                page.Pointer,
                ModeledPageAddress,
                4096,
                0,
                16,
                32,
                PortIoStubPlan.Out8,
                PortIoStubPlan.In8,
                ReadWrite,
                PublicationEpoch,
                (ExistingIn8Out8StubPageState*)(stateStorage + 1),
                expectedTransitionVersion: 0);
        ExistingIn8Out8StubPageResult overflowingState =
            ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(
                page.Pointer,
                ModeledPageAddress,
                4096,
                0,
                16,
                32,
                PortIoStubPlan.Out8,
                PortIoStubPlan.In8,
                ReadWrite,
                PublicationEpoch,
                (ExistingIn8Out8StubPageState*)
                    0xffff_ffff_ffff_fff8UL,
                expectedTransitionVersion: 0);
        ExistingIn8Out8StubPageState defaultState = default;
        ExistingIn8Out8StubPageResult invalidState =
            MaterializeValid(page, &defaultState);

        return missingState.Status
                == ExistingIn8Out8StubPageStatus.MissingState
            && misalignedState.Status
                == ExistingIn8Out8StubPageStatus.MisalignedState
            && overflowingState.Status
                == ExistingIn8Out8StubPageStatus.StateSpanOverflow
            && invalidState.Status
                == ExistingIn8Out8StubPageStatus.InvalidState
            && page.IsAll(Sentinel);
    }

    private static bool TestFullPageReadbackRejectsEveryMutation()
    {
        using AlignedPage page = new();
        ExistingIn8Out8StubPageState state =
            ExistingIn8Out8StubPageState.Create();
        ExistingIn8Out8StubPageResult materialized =
            MaterializeValid(page, &state);
        if (!materialized.Succeeded)
        {
            return false;
        }

        for (int index = 0; index < 4096; index++)
        {
            byte original = page.Pointer[index];
            page.Pointer[index] ^= 0xff;
            ExistingIn8Out8StubPageState before = state;
            ExistingIn8Out8StubPageResult rejected =
                ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                    page.Pointer,
                    ModeledPageAddress,
                    4096,
                    materialized,
                    &state,
                    expectedTransitionVersion: 1);
            page.Pointer[index] = original;

            bool instructionByte = IsInstructionByte(index);
            ExistingIn8Out8StubPageStatus expectedStatus =
                instructionByte
                    ? ExistingIn8Out8StubPageStatus.ByteReadbackMismatch
                    : ExistingIn8Out8StubPageStatus.NonzeroPadding;
            if (rejected.Succeeded
                || rejected.Status != expectedStatus
                || !StatesEqual(state, before))
            {
                return false;
            }
        }

        page.Pointer[0] = 0;
        ExistingIn8Out8StubPageResult partial =
            ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                page.Pointer,
                ModeledPageAddress,
                4096,
                materialized,
                &state,
                expectedTransitionVersion: 1);
        page.Pointer[0] = 0xb8;
        ExistingIn8Out8StubPageResult valid =
            ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                page.Pointer,
                ModeledPageAddress,
                4096,
                materialized,
                &state,
                expectedTransitionVersion: 1);

        return partial.Status
                == ExistingIn8Out8StubPageStatus.PartialMaterialization
            && valid.ReadBackVerified
            && state.Phase
                == ExistingIn8Out8StubPagePhase.ReadBackVerified;
    }

    private static bool TestStateStorageAliasing()
    {
        using AlignedPage page = new();
        page.Fill(Sentinel);

        ExistingIn8Out8StubPageState* materializeState =
            (ExistingIn8Out8StubPageState*)(page.Pointer + 128);
        *materializeState = ExistingIn8Out8StubPageState.Create();
        byte[] beforeMaterialize = page.Span.ToArray();
        ExistingIn8Out8StubPageResult materializeRejected =
            ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(
                page.Pointer,
                ModeledPageAddress,
                4096,
                0,
                16,
                32,
                PortIoStubPlan.Out8,
                PortIoStubPlan.In8,
                ReadWrite,
                PublicationEpoch,
                materializeState,
                expectedTransitionVersion: 0);

        if (materializeRejected.Status
                != ExistingIn8Out8StubPageStatus.StateStorageOverlap
            || !page.ReadOnlySpan.SequenceEqual(beforeMaterialize))
        {
            return false;
        }

        ExistingIn8Out8StubPageState externalState =
            ExistingIn8Out8StubPageState.Create();
        ExistingIn8Out8StubPageResult materialized =
            MaterializeValid(page, &externalState);
        if (!materialized.Succeeded)
        {
            return false;
        }

        ExistingIn8Out8StubPageState* readbackState =
            (ExistingIn8Out8StubPageState*)(page.Pointer + 128);
        *readbackState = externalState;
        byte[] beforeReadback = page.Span.ToArray();
        ExistingIn8Out8StubPageResult readbackRejected =
            ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                page.Pointer,
                ModeledPageAddress,
                4096,
                materialized,
                readbackState,
                expectedTransitionVersion: 1);

        return readbackRejected.Status
                == ExistingIn8Out8StubPageStatus.StateStorageOverlap
            && page.ReadOnlySpan.SequenceEqual(beforeReadback);
    }

    private static bool TestReadbackMetadataFailures()
    {
        using AlignedPage page = new();
        ExistingIn8Out8StubPageState state =
            ExistingIn8Out8StubPageState.Create();
        ExistingIn8Out8StubPageResult materialized =
            MaterializeValid(page, &state);
        ExistingIn8Out8StubPageState expectedState = state;

        if (!ExpectReadbackFailure(
                page,
                ref state,
                materialized,
                ExistingIn8Out8StubPageStatus.StaleStateVersion,
                expectedTransitionVersion: 0)
            || !ExpectReadbackFailure(
                page,
                ref state,
                default,
                ExistingIn8Out8StubPageStatus.InvalidExpectedResult)
            || !ExpectReadbackFailure(
                page,
                ref state,
                materialized,
                ExistingIn8Out8StubPageStatus.InvalidSourceLength,
                sourceCapacity: 4095)
            || !ExpectReadbackFailure(
                page,
                ref state,
                materialized,
                ExistingIn8Out8StubPageStatus.InvalidSourceLength,
                sourceCapacity: 4097)
            || !ExpectReadbackFailure(
                page,
                ref state,
                materialized,
                ExistingIn8Out8StubPageStatus.MissingSource,
                source: null)
            || !ExpectReadbackFailure(
                page,
                ref state,
                materialized,
                ExistingIn8Out8StubPageStatus.MisalignedSource,
                source: page.Pointer + 1)
            || !ExpectReadbackFailure(
                page,
                ref state,
                materialized,
                ExistingIn8Out8StubPageStatus.SourceSpanOverflow,
                source: (byte*)0xffff_ffff_ffff_f000UL)
            || !ExpectReadbackFailure(
                page,
                ref state,
                materialized,
                ExistingIn8Out8StubPageStatus.SourceAddressMismatch,
                modeledAddress: OtherModeledPageAddress))
        {
            return false;
        }

        ExistingIn8Out8StubPageResult verified =
            ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                page.Pointer,
                ModeledPageAddress,
                4096,
                materialized,
                &state,
                expectedTransitionVersion: 1);
        ExistingIn8Out8StubPageResult repeated =
            ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                page.Pointer,
                ModeledPageAddress,
                4096,
                verified,
                &state,
                expectedTransitionVersion: 2);

        return expectedState.IsValid
            && verified.ReadBackVerified
            && repeated.Status
                == ExistingIn8Out8StubPageStatus
                    .ExpectedResultNotMaterialized
            && state.TransitionVersion == 2;
    }

    private static bool TestFinalizationFailures()
    {
        using AlignedPage page = new();
        ExistingIn8Out8StubPageState materializedState =
            ExistingIn8Out8StubPageState.Create();
        ExistingIn8Out8StubPageResult materialized =
            MaterializeValid(page, &materializedState);
        ExistingIn8Out8StubPageResult tooEarly =
            ExistingIn8Out8StubBytesRWPagesFinalize.AuditFinalization(
                materialized,
                ModeledPageAddress,
                ReadWrite,
                ReadExecute,
                executionAttemptedBeforeFinalization: false,
                &materializedState,
                expectedTransitionVersion: 1);
        if (tooEarly.Status
                != ExistingIn8Out8StubPageStatus
                    .ExpectedReadbackNotVerified
            || materializedState.Phase
                != ExistingIn8Out8StubPagePhase.Materialized)
        {
            return false;
        }

        ExistingIn8Out8StubPageState state =
            ExistingIn8Out8StubPageState.Create();
        ExistingIn8Out8StubPageResult emitted =
            MaterializeValid(page, &state);
        ExistingIn8Out8StubPageResult verified =
            ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                page.Pointer,
                ModeledPageAddress,
                4096,
                emitted,
                &state,
                expectedTransitionVersion: 1);
        ExistingIn8Out8StubPageState before = state;

        if (!ExpectFinalizationFailure(
                ref state,
                verified,
                ExistingIn8Out8StubPageStatus.StaleFinalization,
                expectedTransitionVersion: 1)
            || !ExpectFinalizationFailure(
                ref state,
                verified,
                ExistingIn8Out8StubPageStatus.StaleFinalization,
                targetPageAddress: OtherModeledPageAddress)
            || !ExpectFinalizationFailure(
                ref state,
                default,
                ExistingIn8Out8StubPageStatus.InvalidExpectedResult)
            || !ExpectFinalizationFailure(
                ref state,
                verified,
                ExistingIn8Out8StubPageStatus.InvalidInitialProtection,
                initialProtection: PageProtection.Read)
            || !ExpectFinalizationFailure(
                ref state,
                verified,
                ExistingIn8Out8StubPageStatus.WritableExecutableRejected,
                initialProtection: ReadWriteExecute)
            || !ExpectFinalizationFailure(
                ref state,
                verified,
                ExistingIn8Out8StubPageStatus.ExecuteBeforeFinalization,
                executionAttempted: true)
            || !ExpectFinalizationFailure(
                ref state,
                verified,
                ExistingIn8Out8StubPageStatus.InvalidFinalProtection,
                finalProtection: PageProtection.Read)
            || !ExpectFinalizationFailure(
                ref state,
                verified,
                ExistingIn8Out8StubPageStatus.WritableExecutableRejected,
                finalProtection: ReadWriteExecute)
            || !StatesEqual(state, before))
        {
            return false;
        }

        ExistingIn8Out8StubPageResult finalized =
            ExistingIn8Out8StubBytesRWPagesFinalize.AuditFinalization(
                verified,
                ModeledPageAddress,
                ReadWrite,
                ReadExecute,
                executionAttemptedBeforeFinalization: false,
                &state,
                expectedTransitionVersion: 2);
        ExistingIn8Out8StubPageState finalState = state;
        ExistingIn8Out8StubPageResult replay =
            ExistingIn8Out8StubBytesRWPagesFinalize.AuditFinalization(
                verified,
                ModeledPageAddress,
                ReadWrite,
                ReadExecute,
                executionAttemptedBeforeFinalization: false,
                &state,
                expectedTransitionVersion: 2);

        return finalized.FinalizationAudited
            && replay.Status
                == ExistingIn8Out8StubPageStatus.AlreadyFinalized
            && StatesEqual(state, finalState)
            && state.FinalizationCount == 1;
    }

    private static bool TestPointerPathAllocatesNoManagedMemory()
    {
        using AlignedPage page = new();
        ExistingIn8Out8StubPageState state =
            ExistingIn8Out8StubPageState.Create();
        RunCompletePointerPath(page, &state);

        long before = GC.GetAllocatedBytesForCurrentThread();
        ExistingIn8Out8StubPageResult final = default;
        for (int iteration = 0; iteration < 32; iteration++)
        {
            state = ExistingIn8Out8StubPageState.Create();
            final = RunCompletePointerPath(page, &state);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return final.FinalizationAudited
            && state.IsValid
            && after == before;
    }

    private static bool TestSourceBoundary()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string operationSource = File.ReadAllText(
            Path.Combine(
                root,
                @"src\OS.Kernel\Diagnostics\PortIoOperationPlan.cs"));
        string stubSource = File.ReadAllText(
            Path.Combine(
                root,
                @"src\OS.Kernel\Diagnostics\PortIoStubPlan.cs"));
        string pageSource = File.ReadAllText(
            Path.Combine(
                root,
                @"src\OS.Kernel\Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs"));
        string combined = operationSource + stubSource + pageSource;
        string operationNoHost = RemoveNoStdLibHostBlocks(operationSource);
        string stubNoHost = RemoveNoStdLibHostBlocks(stubSource);
        string pageNoHost = RemoveNoStdLibHostBlocks(pageSource);

        string[] forbidden =
        [
            "delegate*",
            "GetDelegateForFunctionPointer",
            "DynamicInvoke(",
            "VirtualProtect",
            "mprotect",
            "X64PageMap.",
            "X64TlbInvalidation",
            "invlpg",
            "OSHost_ProtectPages",
            "EfiEntry",
            "Marshal.",
            "OpCodes.Calli"
        ];

        return operationSource.Contains(
                "#if !NOSTDLIB_EFI",
                StringComparison.Ordinal)
            && stubSource.Contains(
                "#if !NOSTDLIB_EFI",
                StringComparison.Ordinal)
            && pageSource.Contains(
                "#if !NOSTDLIB_EFI",
                StringComparison.Ordinal)
            && !operationNoHost.Contains("Span<", StringComparison.Ordinal)
            && !stubNoHost.Contains("Span<", StringComparison.Ordinal)
            && !pageNoHost.Contains("Span<", StringComparison.Ordinal)
            && !pageSource.Contains(ExpectedSha256, StringComparison.Ordinal)
            && !pageSource.Contains("new byte[", StringComparison.Ordinal)
            && !pageSource.Contains("0xee", StringComparison.OrdinalIgnoreCase)
            && !pageSource.Contains("0xec", StringComparison.OrdinalIgnoreCase)
            && forbidden.All(token =>
                !combined.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();
        try
        {
            NoStdLibBuildResult result = RunNoStdLibBuild(workspace);
            string assembly = Path.Combine(
                workspace,
                "out",
                "CLROS.M02.FixedStubPage.NoStdLib.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || !HasZeroAssemblyReferences(assembly))
            {
                Console.WriteLine(
                    "M02_002_NOSTDLIB_COMPILE_DIAGNOSTIC "
                    + NormalizeCompilerOutput(result.Output));
                return false;
            }

            NoStdLibBuildResult pointerRun =
                RunNoStdLibPointerRunner(workspace);
            const string passMarker =
                "M02_002_NOSTDLIB_POINTER_RUN_PASS";
            int markerIndex = pointerRun.Output.IndexOf(
                passMarker,
                StringComparison.Ordinal);
            if (pointerRun.TimedOut
                || pointerRun.ExitCode != 0
                || markerIndex < 0
                || markerIndex != pointerRun.Output.LastIndexOf(
                    passMarker,
                    StringComparison.Ordinal))
            {
                Console.WriteLine(
                    "M02_002_NOSTDLIB_RUN_DIAGNOSTIC "
                    + NormalizeCompilerOutput(pointerRun.Output));
                return false;
            }

            return true;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static ExistingIn8Out8StubPageResult MaterializeValid(
        AlignedPage page,
        ExistingIn8Out8StubPageState* state)
    {
        return ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(
            page.Pointer,
            ModeledPageAddress,
            4096,
            0,
            16,
            32,
            PortIoStubPlan.Out8,
            PortIoStubPlan.In8,
            ReadWrite,
            PublicationEpoch,
            state,
            expectedTransitionVersion: 0);
    }

    private static ExistingIn8Out8StubPageResult RunCompletePointerPath(
        AlignedPage page,
        ExistingIn8Out8StubPageState* state)
    {
        ExistingIn8Out8StubPageResult materialized =
            MaterializeValid(page, state);
        ExistingIn8Out8StubPageResult readback =
            ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                page.Pointer,
                ModeledPageAddress,
                4096,
                materialized,
                state,
                expectedTransitionVersion: 1);
        return ExistingIn8Out8StubBytesRWPagesFinalize.AuditFinalization(
            readback,
            ModeledPageAddress,
            ReadWrite,
            ReadExecute,
            executionAttemptedBeforeFinalization: false,
            state,
            expectedTransitionVersion: 2);
    }

    private static bool HasMaterializedResult(
        ExistingIn8Out8StubPageResult result,
        ExistingIn8Out8StubPageState state)
    {
        return result.Succeeded
            && result.IsInitialized
            && result.Status == ExistingIn8Out8StubPageStatus.Success
            && result.Phase
                == ExistingIn8Out8StubPagePhase.Materialized
            && !result.ReadBackVerified
            && !result.FinalizationAudited
            && result.ModeledPageAddress == ModeledPageAddress
            && result.PageByteCount == 4096
            && result.SafeOffset == 0
            && result.Out8Offset == 16
            && result.In8Offset == 32
            && result.BytesWrittenOrVerified == 4096
            && result.InstructionByteCount == 18
            && result.OccupiedExtent == 37
            && result.Fnv1a64 == 0x982e_5b7e_3ee5_8e56
            && result.PublicationEpoch == PublicationEpoch
            && result.TransitionVersion == 1
            && result.InitialProtection == ReadWrite
            && result.FinalProtection == ReadExecute
            && result.FinalizationCount == 0
            && !result.LiveProtection
            && !result.LiveExecution
            && state.IsValid
            && state.Phase
                == ExistingIn8Out8StubPagePhase.Materialized
            && state.TransitionVersion == 1
            && state.PublicationEpoch == PublicationEpoch
            && state.ModeledPageAddress == ModeledPageAddress
            && state.CurrentProtection == ReadWrite;
    }

    private static bool HasReadbackResult(
        ExistingIn8Out8StubPageResult result,
        ExistingIn8Out8StubPageState state)
    {
        return result.Succeeded
            && result.ReadBackVerified
            && !result.FinalizationAudited
            && result.Phase
                == ExistingIn8Out8StubPagePhase.ReadBackVerified
            && result.TransitionVersion == 2
            && result.BytesWrittenOrVerified == 4096
            && !result.LiveProtection
            && !result.LiveExecution
            && state.IsValid
            && state.Phase
                == ExistingIn8Out8StubPagePhase.ReadBackVerified
            && state.TransitionVersion == 2
            && state.CurrentProtection == ReadWrite;
    }

    private static bool HasFinalizedResult(
        ExistingIn8Out8StubPageResult result,
        ExistingIn8Out8StubPageState state)
    {
        return result.Succeeded
            && result.ReadBackVerified
            && result.FinalizationAudited
            && result.Phase
                == ExistingIn8Out8StubPagePhase.FinalizationAudited
            && result.TransitionVersion == 3
            && result.FinalizationCount == 1
            && result.InitialProtection == ReadWrite
            && result.FinalProtection == ReadExecute
            && !result.LiveProtection
            && !result.LiveExecution
            && state.IsValid
            && state.Phase
                == ExistingIn8Out8StubPagePhase.FinalizationAudited
            && state.TransitionVersion == 3
            && state.CurrentProtection == ReadExecute
            && state.FinalizationCount == 1;
    }

    private static bool HasExactPage(ReadOnlySpan<byte> page)
    {
        if (page.Length != 4096
            || !page.Slice(0, 6).SequenceEqual(
                new byte[] { 0xb8, 0x4b, 0x4f, 0x53, 0x4f, 0xc3 })
            || !page.Slice(16, 7).SequenceEqual(
                new byte[] { 0x88, 0xd0, 0x66, 0x89, 0xca, 0xee, 0xc3 })
            || !page.Slice(32, 5).SequenceEqual(
                new byte[] { 0x66, 0x89, 0xca, 0xec, 0xc3 }))
        {
            return false;
        }

        for (int index = 0; index < page.Length; index++)
        {
            if (!IsInstructionByte(index) && page[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInstructionByte(int index)
    {
        return index is >= 0 and < 6
            || index is >= 16 and < 23
            || index is >= 32 and < 37;
    }

    private static ulong IndependentFnv1a64(ReadOnlySpan<byte> bytes)
    {
        ulong hash = 0xcbf2_9ce4_8422_2325;
        for (int index = 0; index < bytes.Length; index++)
        {
            hash = unchecked(
                (hash ^ bytes[index]) * 0x0000_0100_0000_01b3);
        }

        return hash;
    }

    private static bool ExpectMaterializeFailure(
        AlignedPage page,
        ExistingIn8Out8StubPageStatus expectedStatus,
        byte* destination = null,
        ulong modeledAddress = ModeledPageAddress,
        ulong destinationCapacity = 4096,
        ulong safeOffset = 0,
        ulong out8Offset = 16,
        ulong in8Offset = 32,
        PortIoStubPlan? out8Plan = null,
        PortIoStubPlan? in8Plan = null,
        PageProtection initialProtection =
            (PageProtection)((byte)PageProtection.Read
                | (byte)PageProtection.Write),
        ulong publicationEpoch = PublicationEpoch,
        ulong expectedTransitionVersion = 0)
    {
        page.Fill(Sentinel);
        ExistingIn8Out8StubPageState state =
            ExistingIn8Out8StubPageState.Create();
        ExistingIn8Out8StubPageState before = state;
        byte* actualDestination = destination;
        if (destination == null
            && expectedStatus
                != ExistingIn8Out8StubPageStatus.MissingDestination)
        {
            actualDestination = page.Pointer;
        }

        ExistingIn8Out8StubPageResult result =
            ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(
                actualDestination,
                modeledAddress,
                destinationCapacity,
                safeOffset,
                out8Offset,
                in8Offset,
                out8Plan ?? PortIoStubPlan.Out8,
                in8Plan ?? PortIoStubPlan.In8,
                initialProtection,
                publicationEpoch,
                &state,
                expectedTransitionVersion);

        bool passed = !result.Succeeded
            && result.Status == expectedStatus
            && result.Phase
                == ExistingIn8Out8StubPagePhase.Uninitialized
            && result.BytesWrittenOrVerified == 0
            && StatesEqual(state, before)
            && page.IsAll(Sentinel);
        if (!passed)
        {
            Console.WriteLine(
                "M02_002_MATERIALIZE_FAILURE_DIAGNOSTIC expected="
                + expectedStatus
                + " actual="
                + result.Status
                + " succeeded="
                + result.Succeeded
                + " phase="
                + result.Phase
                + " bytes="
                + result.BytesWrittenOrVerified
                + " stateUnchanged="
                + StatesEqual(state, before)
                + " destinationUnchanged="
                + page.IsAll(Sentinel));
        }

        return passed;
    }

    private static bool ExpectReadbackFailure(
        AlignedPage page,
        ref ExistingIn8Out8StubPageState state,
        ExistingIn8Out8StubPageResult materialized,
        ExistingIn8Out8StubPageStatus expectedStatus,
        byte* source = null,
        ulong modeledAddress = ModeledPageAddress,
        ulong sourceCapacity = 4096,
        ulong expectedTransitionVersion = 1)
    {
        ExistingIn8Out8StubPageState before = state;
        byte* actualSource = source;
        if (source == null
            && expectedStatus
                != ExistingIn8Out8StubPageStatus.MissingSource)
        {
            actualSource = page.Pointer;
        }

        ExistingIn8Out8StubPageResult result;
        fixed (ExistingIn8Out8StubPageState* statePointer = &state)
        {
            result =
                ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                    actualSource,
                    modeledAddress,
                    sourceCapacity,
                    materialized,
                    statePointer,
                    expectedTransitionVersion);
        }

        return !result.Succeeded
            && result.Status == expectedStatus
            && StatesEqual(state, before)
            && HasExactPage(page.Span);
    }

    private static bool ExpectFinalizationFailure(
        ref ExistingIn8Out8StubPageState state,
        ExistingIn8Out8StubPageResult verified,
        ExistingIn8Out8StubPageStatus expectedStatus,
        ulong targetPageAddress = ModeledPageAddress,
        PageProtection initialProtection =
            (PageProtection)((byte)PageProtection.Read
                | (byte)PageProtection.Write),
        PageProtection finalProtection =
            (PageProtection)((byte)PageProtection.Read
                | (byte)PageProtection.Execute),
        bool executionAttempted = false,
        ulong expectedTransitionVersion = 2)
    {
        ExistingIn8Out8StubPageState before = state;
        ExistingIn8Out8StubPageResult result;
        fixed (ExistingIn8Out8StubPageState* statePointer = &state)
        {
            result =
                ExistingIn8Out8StubBytesRWPagesFinalize.AuditFinalization(
                    verified,
                    targetPageAddress,
                    initialProtection,
                    finalProtection,
                    executionAttempted,
                    statePointer,
                    expectedTransitionVersion);
        }

        return !result.Succeeded
            && result.Status == expectedStatus
            && StatesEqual(state, before);
    }

    private static bool StatesEqual(
        ExistingIn8Out8StubPageState left,
        ExistingIn8Out8StubPageState right)
    {
        return left.IsInitialized == right.IsInitialized
            && left.IsValid == right.IsValid
            && left.Phase == right.Phase
            && left.TransitionVersion == right.TransitionVersion
            && left.PublicationEpoch == right.PublicationEpoch
            && left.ModeledPageAddress == right.ModeledPageAddress
            && left.CurrentProtection == right.CurrentProtection
            && left.FinalizationCount == right.FinalizationCount;
    }

    private static bool HasSequentialByteEnum<T>(
        int expectedCount)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
        {
            return false;
        }

        T[] values = Enum.GetValues<T>();
        if (values.Length != expectedCount)
        {
            return false;
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (Convert.ToByte(values[index]) != index)
            {
                return false;
            }
        }

        return true;
    }

    private static string RemoveNoStdLibHostBlocks(string source)
    {
        return Regex.Replace(
            source,
            @"(?ms)^[ \t]*#if[ \t]+!NOSTDLIB_EFI[ \t]*\r?\n.*?^[ \t]*#endif[ \t]*\r?\n?",
            string.Empty);
    }

    private static string CreateNoStdLibWorkspace()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m02-002-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.Copy(
                Path.Combine(root, "global.json"),
                Path.Combine(workspace, "global.json"));
            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibShim.cs"),
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
                    public struct IntPtr { }
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
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                using OS.Kernel.Diagnostics;
                using OS.Kernel.Memory;

                internal static unsafe class M02002PointerProbe
                {
                    internal static ExistingIn8Out8StubPageStatus Exercise(
                        byte* storage)
                    {
                        ulong raw = (ulong)storage;
                        byte* page = (byte*)((raw + 4095UL) & ~4095UL);
                        ExistingIn8Out8StubPageState state =
                            ExistingIn8Out8StubPageState.Create();
                        ExistingIn8Out8StubPageResult materialized =
                            ExistingIn8Out8StubBytesRWPagesFinalize.Materialize(
                                page,
                                0x00400000UL,
                                4096UL,
                                0UL,
                                16UL,
                                32UL,
                                PortIoStubPlan.Out8,
                                PortIoStubPlan.In8,
                                (PageProtection)((byte)PageProtection.Read
                                    | (byte)PageProtection.Write),
                                1UL,
                                &state,
                                0UL);
                        ExistingIn8Out8StubPageResult readback =
                            ExistingIn8Out8StubBytesRWPagesFinalize.ValidateReadback(
                                page,
                                0x00400000UL,
                                4096UL,
                                materialized,
                                &state,
                                1UL);
                        ExistingIn8Out8StubPageResult finalized =
                            ExistingIn8Out8StubBytesRWPagesFinalize.AuditFinalization(
                                readback,
                                0x00400000UL,
                                (PageProtection)((byte)PageProtection.Read
                                    | (byte)PageProtection.Write),
                                (PageProtection)((byte)PageProtection.Read
                                    | (byte)PageProtection.Execute),
                                false,
                                &state,
                                2UL);
                        return finalized.Status;
                    }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string project =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M02.FixedStubPage.NoStdLib</AssemblyName>
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
                    <Compile Include="__PROTECTION__" Link="Memory\PageProtection.cs" />
                    <Compile Include="__OPERATION__" Link="Diagnostics\PortIoOperationPlan.cs" />
                    <Compile Include="__STUB__" Link="Diagnostics\PortIoStubPlan.cs" />
                    <Compile Include="__PAGE__" Link="Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs" />
                    <Compile Include="Probe.cs" />
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
                """
                .Replace(
                    "__PROTECTION__",
                    EscapeXml(Path.Combine(
                        root,
                        @"src\OS.Kernel\Memory\PageProtection.cs")),
                    StringComparison.Ordinal)
                .Replace(
                    "__OPERATION__",
                    EscapeXml(Path.Combine(
                        root,
                        @"src\OS.Kernel\Diagnostics\PortIoOperationPlan.cs")),
                    StringComparison.Ordinal)
                .Replace(
                    "__STUB__",
                    EscapeXml(Path.Combine(
                        root,
                        @"src\OS.Kernel\Diagnostics\PortIoStubPlan.cs")),
                    StringComparison.Ordinal)
                .Replace(
                    "__PAGE__",
                    EscapeXml(Path.Combine(
                        root,
                        @"src\OS.Kernel\Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs")),
                    StringComparison.Ordinal);
            File.WriteAllText(
                Path.Combine(workspace, "M02.002.NoStdLib.csproj"),
                project,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            File.WriteAllText(
                Path.Combine(workspace, "PointerRunner.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                using OS.Kernel.Diagnostics;
                using OS.Kernel.Memory;
                using System;
                using System.Runtime.InteropServices;

                internal static unsafe class M02002PointerRunner
                {
                    private static int Main()
                    {
                        byte* page = (byte*)NativeMemory.AlignedAlloc(
                            4096,
                            4096);
                        if (page == null)
                        {
                            return 10;
                        }

                        try
                        {
                            ExistingIn8Out8StubPageState state =
                                ExistingIn8Out8StubPageState.Create();
                            PageProtection readWrite =
                                (PageProtection)(
                                    (byte)PageProtection.Read
                                    | (byte)PageProtection.Write);
                            PageProtection readExecute =
                                (PageProtection)(
                                    (byte)PageProtection.Read
                                    | (byte)PageProtection.Execute);
                            ExistingIn8Out8StubPageResult materialized =
                                ExistingIn8Out8StubBytesRWPagesFinalize
                                    .Materialize(
                                        page,
                                        0x00400000UL,
                                        4096UL,
                                        0UL,
                                        16UL,
                                        32UL,
                                        PortIoStubPlan.Out8,
                                        PortIoStubPlan.In8,
                                        readWrite,
                                        1UL,
                                        &state,
                                        0UL);
                            ExistingIn8Out8StubPageResult readback =
                                ExistingIn8Out8StubBytesRWPagesFinalize
                                    .ValidateReadback(
                                        page,
                                        0x00400000UL,
                                        4096UL,
                                        materialized,
                                        &state,
                                        1UL);
                            ExistingIn8Out8StubPageResult finalized =
                                ExistingIn8Out8StubBytesRWPagesFinalize
                                    .AuditFinalization(
                                        readback,
                                        0x00400000UL,
                                        readWrite,
                                        readExecute,
                                        false,
                                        &state,
                                        2UL);

                            if (!materialized.Succeeded
                                || !readback.ReadBackVerified
                                || !finalized.FinalizationAudited
                                || !state.IsValid
                                || state.TransitionVersion != 3UL
                                || state.FinalizationCount != 1
                                || finalized.Fnv1a64
                                    != ExistingIn8Out8StubBytesRWPagesFinalize
                                        .ExpectedFnv1a64
                                || finalized.LiveProtection
                                || finalized.LiveExecution)
                            {
                                return 20;
                            }

                            Console.WriteLine(
                                "M02_002_NOSTDLIB_POINTER_RUN_PASS");
                            return 0;
                        }
                        finally
                        {
                            NativeMemory.AlignedFree(page);
                        }
                    }
                }
                """,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string runnerProject =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M02.FixedStubPage.NostdlibRunner</AssemblyName>
                    <TargetFramework>net10.0</TargetFramework>
                    <LangVersion>14.0</LangVersion>
                    <OutputType>Exe</OutputType>
                    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                    <ImplicitUsings>disable</ImplicitUsings>
                    <Nullable>disable</Nullable>
                    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                    <DefineConstants>NOSTDLIB_EFI</DefineConstants>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <EnableNETAnalyzers>false</EnableNETAnalyzers>
                    <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                    <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                    <BaseIntermediateOutputPath>$(MSBuildProjectDirectory)\runner-obj\</BaseIntermediateOutputPath>
                    <OutputPath>$(MSBuildProjectDirectory)\runner-out\</OutputPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="__PROTECTION__" Link="Memory\PageProtection.cs" />
                    <Compile Include="__OPERATION__" Link="Diagnostics\PortIoOperationPlan.cs" />
                    <Compile Include="__STUB__" Link="Diagnostics\PortIoStubPlan.cs" />
                    <Compile Include="__PAGE__" Link="Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs" />
                    <Compile Include="PointerRunner.cs" />
                  </ItemGroup>
                </Project>
                """
                .Replace(
                    "__PROTECTION__",
                    EscapeXml(Path.Combine(
                        root,
                        @"src\OS.Kernel\Memory\PageProtection.cs")),
                    StringComparison.Ordinal)
                .Replace(
                    "__OPERATION__",
                    EscapeXml(Path.Combine(
                        root,
                        @"src\OS.Kernel\Diagnostics\PortIoOperationPlan.cs")),
                    StringComparison.Ordinal)
                .Replace(
                    "__STUB__",
                    EscapeXml(Path.Combine(
                        root,
                        @"src\OS.Kernel\Diagnostics\PortIoStubPlan.cs")),
                    StringComparison.Ordinal)
                .Replace(
                    "__PAGE__",
                    EscapeXml(Path.Combine(
                        root,
                        @"src\OS.Kernel\Diagnostics\ExistingIn8Out8StubBytesRWPagesFinalize.cs")),
                    StringComparison.Ordinal);
            File.WriteAllText(
                Path.Combine(
                    workspace,
                    "M02.002.NostdlibRunner.csproj"),
                runnerProject,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return workspace;
        }
        catch
        {
            DeleteNoStdLibWorkspace(workspace);
            throw;
        }
    }

    private static NoStdLibBuildResult RunNoStdLibBuild(
        string workspace)
    {
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workspace
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add("M02.002.NoStdLib.csproj");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-m:1");
        start.ArgumentList.Add("--no-dependencies");
        start.ArgumentList.Add("--no-incremental");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-v:minimal");
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        start.ArgumentList.Add("-p:NuGetAudit=false");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";

        using Process process = new() { StartInfo = start };
        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90_000);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                process.Kill();
            }
        }

        bool stopped = exited || process.WaitForExit(5_000);
        bool streamsCompleted = Task.WaitAll(
            [stdout, stderr],
            millisecondsTimeout: 5_000);
        string output = streamsCompleted
            ? stdout.GetAwaiter().GetResult()
                + Environment.NewLine
                + stderr.GetAwaiter().GetResult()
            : "Compiler streams did not close inside the bounded timeout.";
        return new NoStdLibBuildResult(
            stopped ? process.ExitCode : -1,
            timedOut: !exited || !stopped || !streamsCompleted,
            output);
    }

    private static NoStdLibBuildResult RunNoStdLibPointerRunner(
        string workspace)
    {
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workspace
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add("M02.002.NostdlibRunner.csproj");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("--no-launch-profile");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        start.ArgumentList.Add("-p:NuGetAudit=false");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";

        using Process process = new() { StartInfo = start };
        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90_000);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                process.Kill();
            }
        }

        bool stopped = exited || process.WaitForExit(5_000);
        bool streamsCompleted = Task.WaitAll(
            [stdout, stderr],
            millisecondsTimeout: 5_000);
        string output = streamsCompleted
            ? stdout.GetAwaiter().GetResult()
                + Environment.NewLine
                + stderr.GetAwaiter().GetResult()
            : "Runner streams did not close inside the bounded timeout.";
        return new NoStdLibBuildResult(
            stopped ? process.ExitCode : -1,
            timedOut: !exited || !stopped || !streamsCompleted,
            output);
    }

    private static bool HasZeroAssemblyReferences(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader reader = new(stream);
        return reader.HasMetadata
            && reader.GetMetadataReader().AssemblyReferences.Count == 0;
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath());
        string full = Path.GetFullPath(workspace);
        string expectedPrefix =
            Path.TrimEndingDirectorySeparator(tempRoot)
            + Path.DirectorySeparatorChar;
        string leaf = Path.GetFileName(full);
        if (!full.StartsWith(
                expectedPrefix,
                StringComparison.OrdinalIgnoreCase)
            || !leaf.StartsWith(
                "clros-m02-002-",
                StringComparison.Ordinal)
            || leaf.Length <= "clros-m02-002-".Length)
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M02-002 workspace.");
        }

        if (Directory.Exists(full))
        {
            Directory.Delete(full, recursive: true);
        }
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string NormalizeCompilerOutput(string output)
    {
        return output
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private static string Normalize(string value)
    {
        return value
            .Replace(' ', '_')
            .Replace('/', '_')
            .ToLowerInvariant();
    }

    private static PageProtection ReadWrite =>
        (PageProtection)(
            (byte)PageProtection.Read
            | (byte)PageProtection.Write);

    private static PageProtection ReadExecute =>
        (PageProtection)(
            (byte)PageProtection.Read
            | (byte)PageProtection.Execute);

    private static PageProtection ReadWriteExecute =>
        (PageProtection)(
            (byte)PageProtection.Read
            | (byte)PageProtection.Write
            | (byte)PageProtection.Execute);

    private sealed class AlignedPage : IDisposable
    {
        private byte* _pointer;

        internal AlignedPage()
        {
            _pointer = (byte*)NativeMemory.AlignedAlloc(
                (nuint)ExistingIn8Out8StubBytesRWPagesFinalize.PageSize,
                (nuint)ExistingIn8Out8StubBytesRWPagesFinalize
                    .RequiredPointerAlignment);
            if (_pointer == null)
            {
                throw new OutOfMemoryException(
                    "Could not allocate the M02-002 aligned test page.");
            }
        }

        internal byte* Pointer => _pointer;

        internal Span<byte> Span =>
            new(
                _pointer,
                (int)ExistingIn8Out8StubBytesRWPagesFinalize.PageSize);

        internal ReadOnlySpan<byte> ReadOnlySpan => Span;

        internal void Fill(byte value)
        {
            Span.Fill(value);
        }

        internal bool IsAll(byte value)
        {
            foreach (byte item in Span)
            {
                if (item != value)
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (_pointer != null)
            {
                NativeMemory.AlignedFree(_pointer);
                _pointer = null;
            }
        }
    }

    private readonly struct NoStdLibBuildResult
    {
        internal NoStdLibBuildResult(
            int exitCode,
            bool timedOut,
            string output)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
        }

        internal int ExitCode { get; }

        internal bool TimedOut { get; }

        internal string Output { get; }
    }
}
