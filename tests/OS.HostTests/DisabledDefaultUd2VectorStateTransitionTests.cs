using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OS.Kernel.Diagnostics;

internal static unsafe class DisabledDefaultUd2VectorStateTransitionTests
{
    private const string TestName = "m02.013.transition";
    private const ulong StreamAddress = 0x0020_1000UL;
    private const ulong ContextAddress = 0x0030_1000UL;
    private const ulong IdtAddress = 0x0030_0000UL;
    private const ulong Ist1StackTop = 0x0030_3000UL;
    private const ulong Ist2StackTop = 0x0030_4000UL;
    private const ulong Ist3StackTop = 0x0030_5000UL;
    private const int PageByteCount = 4096;
    private const int AllocationByteCount = 8192;
    private const byte Sentinel = 0xa5;
    private const string ExpectedSha256 =
        "DB402C072CBD5E9C82F36D1802AAB526C347077BDD147031D83E83B563E195B7";
    private const string SuccessTranscript =
        "CLROS_M02_013_READY vector=6 errorCodePresent=false errorCode=0 " +
        "ud2RipMatched=true registerFields=23 registerStateCaptured=true " +
        "registerStateMatched=true ownedGdtLoaded=true ownedTssLoaded=true " +
        "ownedIdtrLoaded=true ownedCom1=true faultTriggered=true " +
        "terminalHalt=true markerTransport=ownedCom1 " +
        "postExitBootServices=false\r\n" +
        "TEST_PASS m02.013\r\n";
    private const string FailureTranscript =
        "M02_UD2_VECTOR_STATE_VERIFY_FAILED\r\n" +
        "TEST_FAIL m02.013 vector_or_register_mismatch\r\n";
    private const string HostReadyMarker =
        "CLROS_M02_013_TRANSITION_HOST_READY contextBytes=496 " +
        "streamBytes=4096 savedGprs=15 normalizedFrameBytes=64 " +
        "registerFields=23 streamReadBack=true everyByteMutation=true " +
        "ownedCom1Plan=true terminalPlan=true guestExecution=false " +
        "faultTriggered=false postExitBootServices=false";

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        Console.WriteLine("TEST_START " + TestName);
        int failures = 0;

        RunCase("value layouts topology and identities are exact",
            TestValueContracts, ref failures);
        RunCase("context preparation is independently exact and tail safe",
            TestExactContext, ref failures);
        RunCase("stream transcripts fingerprints and tails are exact",
            TestExactStream, ref failures);
        RunCase("instruction targets captures and terminal paths decode",
            TestExactDecoding, ref failures);
        RunCase("every prepared context mutation rejects atomically",
            TestEveryContextMutation, ref failures);
        RunCase("every emitted stream mutation rejects at its byte",
            TestEveryStreamMutation, ref failures);
        RunCase("span overloads preserve pointer results and bytes",
            TestSpanParity, ref failures);
        RunCase("range overlap and IST failures reject before mutation",
            TestAtomicValidationFailures, ref failures);
        RunCase("pointer cores allocate no managed memory",
            TestZeroAllocation, ref failures);
        RunCase("exact authority closure compiles without the BCL",
            TestNoStdLibCompile, ref failures);
        RunCase("source boundary remains terminal and host safe",
            TestSourceBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + TestName + " host-tests failures=" +
                failures);
            throw new InvalidOperationException(
                "M02-013 transition host tests failed: " + failures);
        }

        Console.WriteLine(HostReadyMarker);
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
                Console.WriteLine("M02_013_TRANSITION_CASE_FAIL " +
                    Normalize(name));
                return false;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "M02_013_TRANSITION_CASE_FAIL " + Normalize(name) + " " +
                    exception.GetType().Name + ": " + exception.Message);
                throw;
            }
        }, ref failures);
    }

    private static bool TestValueContracts()
    {
        DisabledDefaultUd2VectorStateTransitionResult empty = default;

        return Enum.GetUnderlyingType(
                typeof(DisabledDefaultUd2VectorStateTransitionOperation)) ==
                typeof(byte)
            && Enum.GetUnderlyingType(
                typeof(DisabledDefaultUd2VectorStateTransitionStatus)) ==
                typeof(byte)
            && EnumValuesAreSequential<
                DisabledDefaultUd2VectorStateTransitionOperation>(4)
            && EnumValuesAreSequential<
                DisabledDefaultUd2VectorStateTransitionStatus>(42)
            && Unsafe.SizeOf<
                DisabledDefaultUd2VectorStateTransitionContext>() == 496
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                DisabledDefaultUd2VectorStateTransitionContext>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                DisabledDefaultUd2VectorStateTransitionResult>()
            && HasExactContextOffsets()
            && DisabledDefaultUd2VectorStateTransition.BaseContextByteCount ==
                168
            && DisabledDefaultUd2VectorStateTransition.ContextByteCount == 496
            && DisabledDefaultUd2VectorStateTransition.StreamByteCount == 4096
            && DisabledDefaultUd2VectorStateTransition.TssIst1Offset == 100
            && DisabledDefaultUd2VectorStateTransition.TssIst2Offset == 108
            && DisabledDefaultUd2VectorStateTransition.TssIst3Offset == 116
            && DisabledDefaultUd2VectorStateTransition.CommonHandlerOffset == 0
            && DisabledDefaultUd2VectorStateTransition.SuccessSetupOffset == 920
            && DisabledDefaultUd2VectorStateTransition.FailureSetupOffset == 937
            && DisabledDefaultUd2VectorStateTransition.SerialWriterOffset == 949
            && DisabledDefaultUd2VectorStateTransition.SerialPollOffset == 953
            && DisabledDefaultUd2VectorStateTransition.TerminalOffset == 972
            && DisabledDefaultUd2VectorStateTransition.HaltOffset == 973
            && DisabledDefaultUd2VectorStateTransition.SuccessTranscriptOffset ==
                976
            && DisabledDefaultUd2VectorStateTransition.FailureTranscriptOffset ==
                1317
            && DisabledDefaultUd2VectorStateTransition
                .ContextAddressLiteralOffset == 1400
            && DisabledDefaultUd2VectorStateTransition.HandlerBlockEndOffset ==
                1408
            && DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset ==
                0x0800
            && DisabledDefaultUd2VectorStateTransition.Ud2Offset == 0x09ce
            && DisabledDefaultUd2VectorStateTransition.TransitionEndOffset ==
                0x09d0
            && HasExactCompositeTopology()
            && HasExactNormalizedFrameLayout()
            && DisabledDefaultUd2VectorStateTransition.InvalidOpcodeVector == 6
            && DisabledDefaultUd2VectorStateTransition.InvalidOpcodeVector ==
                DisabledDefaultUd2VectorState.InvalidOpcodeVector
            && DisabledDefaultUd2VectorStateTransition.InvalidOpcodeVector ==
                FaultVectorCatalog.InvalidOpcode
            && DisabledDefaultUd2VectorStateTransition.RegisterFieldCount == 23
            && DisabledDefaultUd2VectorStateTransition.RegisterFieldCount ==
                DisabledDefaultUd2VectorState.CpuFaultFrameFieldCount
            && DisabledDefaultUd2VectorStateTransition.OwnedIdtrLimit == 4095
            && DisabledDefaultUd2VectorStateTransition.ContextAlignment == 16
            && DisabledDefaultUd2VectorStateTransition.StreamAlignment == 4096
            && DisabledDefaultUd2VectorStateTransition.IstStackTopAlignment == 16
            && DisabledDefaultUd2VectorStateTransition.SeededRflags == 0x46
            && DisabledDefaultUd2VectorStateTransition
                .FaultFrameResumeFlagMask == 0x0001_0000
            && DisabledDefaultUd2VectorStateTransition.ExpectedRflags ==
                0x0001_0046
            && DisabledDefaultUd2VectorStateTransition
                .CaptureCompletionCookie == 0x4d30_3231_3343_4150UL
            && DisabledDefaultUd2VectorStateTransition.RegisterCookieBase ==
                0x4d02_0130_0000_0000UL
            && DisabledDefaultUd2VectorStateTransition.Com1DataPort == 0x03f8
            && DisabledDefaultUd2VectorStateTransition.Com1LineStatusPort ==
                0x03fd
            && DisabledDefaultUd2VectorStateTransition
                .Com1TransmitterHoldingRegisterEmptyMask == 0x20
            && DisabledDefaultUd2VectorStateTransition
                .CanonicalModeledContextAddress == ContextAddress
            && DisabledDefaultUd2VectorStateTransition.StreamFnv1A64 ==
                0xe507_0e16_f235_33a3UL
            && DisabledDefaultUd2VectorStateTransition
                .StreamSha256Word0BigEndian == 0xdb40_2c07_2cbd_5e9cUL
            && DisabledDefaultUd2VectorStateTransition
                .StreamSha256Word1BigEndian == 0x82f3_6d18_02aa_b526UL
            && DisabledDefaultUd2VectorStateTransition
                .StreamSha256Word2BigEndian == 0xc347_077b_dd14_7031UL
            && DisabledDefaultUd2VectorStateTransition
                .StreamSha256Word3BigEndian == 0xd83e_83b5_63e1_95b7UL
            && DisabledDefaultUd2VectorStateTransition
                .AuditedConditionalFailureBranchCount == 37
            && DisabledDefaultUd2VectorStateTransition.AuditedSavedGprCount == 15
            && DisabledDefaultUd2VectorStateTransition
                .AuditedUd2InstructionCount == 1
            && DisabledDefaultUd2VectorStateTransition
                .AuditedCliInstructionCount == 2
            && DisabledDefaultUd2VectorStateTransition
                .AuditedHltInstructionCount == 1
            && DisabledDefaultUd2VectorStateTransition
                .AuditedInInstructionCount == 1
            && DisabledDefaultUd2VectorStateTransition
                .AuditedOutInstructionCount == 1
            && DisabledDefaultUd2VectorStateTransition.UsesWindowsX64Abi
            && DisabledDefaultUd2VectorStateTransition.ContextPassedInRcx
            && !DisabledDefaultUd2VectorStateTransition.HasCalls
            && !DisabledDefaultUd2VectorStateTransition.HasReturns
            && !DisabledDefaultUd2VectorStateTransition.HasExternalRelocations
            && !DisabledDefaultUd2VectorStateTransition.UsesFirmwareCallbacks
            && !DisabledDefaultUd2VectorStateTransition
                .RestoresFirmwareDescriptorState
            && !DisabledDefaultUd2VectorStateTransition.ContinuesExecution
            && DisabledDefaultUd2VectorStateTransition.TerminalHalt
            && !DisabledDefaultUd2VectorStateTransition.HostTestsInvokeBytes
            && !DisabledDefaultUd2VectorStateTransition.GuardPagesProved
            && !empty.IsInitialized
            && empty.Operation ==
                DisabledDefaultUd2VectorStateTransitionOperation.None
            && empty.Status ==
                DisabledDefaultUd2VectorStateTransitionStatus.Uninitialized
            && empty.FirstMismatchOffset == -1
            && !empty.DestinationMutated
            && !empty.ReadBackPerformed
            && !empty.ContextPrepared
            && !empty.StreamEmitted
            && !empty.ReadBackValidated
            && !empty.Succeeded;
    }

    private static bool TestExactContext()
    {
        byte* context = AllocateAligned(AllocationByteCount, PageByteCount);
        try
        {
            Fill(context, AllocationByteCount, Sentinel);
            DisabledDefaultUd2VectorStateTransitionResult result =
                Prepare(context, PageByteCount);
            if (!result.ContextPrepared || !result.Succeeded ||
                result.Operation !=
                    DisabledDefaultUd2VectorStateTransitionOperation
                        .PrepareContext ||
                result.Status !=
                    DisabledDefaultUd2VectorStateTransitionStatus.Success ||
                result.ModeledContextAddress != ContextAddress ||
                result.ModeledStreamAddress != 0 ||
                result.OwnedIdtAddress != IdtAddress ||
                result.ContextCapacity != PageByteCount ||
                result.StreamCapacity != 0 ||
                result.ContextBytesWritten != 496 ||
                result.ContextBytesCompared != 496 ||
                result.StreamBytesWritten != 0 ||
                result.StreamBytesCompared != 0 ||
                result.FirstMismatchOffset != -1 ||
                result.ContextFingerprint == 0 ||
                result.StreamFingerprint != 0 ||
                !result.DestinationMutated || !result.ReadBackPerformed ||
                !IsAll(context + 496, AllocationByteCount - 496, Sentinel))
            {
                return false;
            }

            byte* expected = stackalloc byte[496];
            BuildExpectedContext(expected);
            if (!new ReadOnlySpan<byte>(context, 496).SequenceEqual(
                    new ReadOnlySpan<byte>(expected, 496)) ||
                result.ContextFingerprint !=
                    IndependentFnv1A64(new ReadOnlySpan<byte>(context, 496)))
            {
                return false;
            }

            return ReadU16(context) == 39
                && ReadU64(context + 2) == ContextAddress + 24
                && ReadU64(context + 24 + 24 + 2) != 0
                && DecodeTssBase(context + 48) == ContextAddress + 64
                && context[53] == 0x89
                && ReadU64(context + 100) == Ist1StackTop
                && ReadU64(context + 108) == Ist2StackTop
                && ReadU64(context + 116) == Ist3StackTop
                && ReadU16(context + 166) == 104
                && ReadU16(context + 176) == 4095
                && ReadU64(context + 178) == IdtAddress
                && ReadU64(context + 208) == Ist1StackTop
                && ReadU64(context + 216) == Ist2StackTop
                && ReadU64(context + 224) == Ist3StackTop
                && ReadU64(context + 232) != 0
                && IsAll(context + 240, 256, 0);
        }
        finally
        {
            NativeMemory.AlignedFree(context);
        }
    }

    private static bool TestExactStream()
    {
        byte* context = AllocateAligned(AllocationByteCount, PageByteCount);
        byte* stream = AllocateAligned(AllocationByteCount, PageByteCount);
        try
        {
            Fill(context, AllocationByteCount, Sentinel);
            Fill(stream, AllocationByteCount, Sentinel);
            if (!Prepare(context, PageByteCount).ContextPrepared)
            {
                return false;
            }

            DisabledDefaultUd2VectorStateTransitionResult emitted =
                Emit(stream, PageByteCount, context, PageByteCount);
            DisabledDefaultUd2VectorStateTransitionResult validated =
                Validate(stream, PageByteCount, context, PageByteCount);
            ReadOnlySpan<byte> bytes = new(stream, PageByteCount);
            string sha256 = Convert.ToHexString(SHA256.HashData(bytes));
            if (!emitted.StreamEmitted || !emitted.Succeeded ||
                emitted.Operation !=
                    DisabledDefaultUd2VectorStateTransitionOperation.EmitStream ||
                emitted.Status !=
                    DisabledDefaultUd2VectorStateTransitionStatus.Success ||
                emitted.ContextBytesCompared != 496 ||
                emitted.StreamBytesWritten != 4096 ||
                emitted.StreamBytesCompared != 4096 ||
                emitted.FirstMismatchOffset != -1 ||
                emitted.StreamFingerprint !=
                    DisabledDefaultUd2VectorStateTransition.StreamFnv1A64 ||
                emitted.ExpectedStreamFingerprint != emitted.StreamFingerprint ||
                !emitted.DestinationMutated || !emitted.ReadBackPerformed ||
                !validated.ReadBackValidated || !validated.Succeeded ||
                validated.Operation != DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack ||
                validated.Status !=
                    DisabledDefaultUd2VectorStateTransitionStatus.Success ||
                validated.StreamBytesWritten != 0 ||
                validated.StreamBytesCompared != 4096 ||
                validated.DestinationMutated || !validated.ReadBackPerformed ||
                IndependentFnv1A64(bytes) !=
                    DisabledDefaultUd2VectorStateTransition.StreamFnv1A64 ||
                sha256 != ExpectedSha256 ||
                !IsAll(stream + PageByteCount,
                    AllocationByteCount - PageByteCount, Sentinel))
            {
                return false;
            }

            byte[] success = Encoding.ASCII.GetBytes(SuccessTranscript);
            byte[] failure = Encoding.ASCII.GetBytes(FailureTranscript);
            return success.Length == 341
                && failure.Length == 83
                && bytes.Slice(
                    DisabledDefaultUd2VectorStateTransition
                        .SuccessTranscriptOffset,
                    success.Length).SequenceEqual(success)
                && bytes.Slice(
                    DisabledDefaultUd2VectorStateTransition
                        .FailureTranscriptOffset,
                    failure.Length).SequenceEqual(failure)
                && !Encoding.ASCII.GetString(bytes).Contains(
                    "TEST_START m02.013", StringComparison.Ordinal)
                && IsAll(stream +
                    DisabledDefaultUd2VectorStateTransition
                        .HandlerBlockEndOffset,
                    DisabledDefaultUd2VectorStateTransition
                        .TransitionEntryOffset -
                    DisabledDefaultUd2VectorStateTransition
                        .HandlerBlockEndOffset,
                    0x90)
                && IsAll(stream +
                    DisabledDefaultUd2VectorStateTransition.TransitionEndOffset,
                    PageByteCount -
                    DisabledDefaultUd2VectorStateTransition.TransitionEndOffset,
                    0xf4);
        }
        finally
        {
            NativeMemory.AlignedFree(context);
            NativeMemory.AlignedFree(stream);
        }
    }

    private static bool TestExactDecoding()
    {
        byte* context = AllocateAligned(PageByteCount, PageByteCount);
        byte* stream = AllocateAligned(PageByteCount, PageByteCount);
        try
        {
            if (!Prepare(context, PageByteCount).ContextPrepared ||
                !Emit(stream, PageByteCount, context, PageByteCount)
                    .StreamEmitted)
            {
                return false;
            }

            ReadOnlySpan<byte> bytes = new(stream, PageByteCount);
            byte[] pushes =
            [
                0x50, 0x53, 0x51, 0x52, 0x56, 0x57, 0x55,
                0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53,
                0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57
            ];
            if (!bytes[..pushes.Length].SequenceEqual(pushes))
            {
                return false;
            }

            int cursor = pushes.Length;
            if (!ExpectRipRelativeLoad(
                    bytes,
                    ref cursor,
                    0x4c,
                    0x8b,
                    0x3d,
                    DisabledDefaultUd2VectorStateTransition
                        .ContextAddressLiteralOffset))
            {
                return false;
            }

            int[] frameStackOffsets =
            [120, 128, 136, 144, 168, 64, 160];
            int[] frameContextOffsets =
            [304, 312, 320, 328, 336, 344, 352];
            for (int index = 0; index < frameStackOffsets.Length; index++)
            {
                if (!ExpectCapture(
                        bytes,
                        ref cursor,
                        frameStackOffsets[index],
                        frameContextOffsets[index]))
                {
                    return false;
                }
            }

            if (!ExpectBytes(bytes, ref cursor, [0x0f, 0x20, 0xd0]) ||
                !ExpectContextStore(bytes, ref cursor, 360) ||
                !ExpectBytes(bytes, ref cursor, [0x0f, 0x20, 0xd8]) ||
                !ExpectContextStore(bytes, ref cursor, 368))
            {
                return false;
            }

            int[] gprStackOffsets =
            [112, 104, 96, 88, 80, 72, 56, 48, 40, 32, 24, 16, 8, 0];
            int[] gprContextOffsets =
            [376, 384, 392, 400, 408, 416, 424, 432, 440, 448,
                456, 464, 472, 480];
            for (int index = 0; index < gprStackOffsets.Length; index++)
            {
                if (!ExpectCapture(
                        bytes,
                        ref cursor,
                        gprStackOffsets[index],
                        gprContextOffsets[index]))
                {
                    return false;
                }
            }

            if (!ExpectMovAbs(
                    bytes,
                    ref cursor,
                    0x48,
                    0xb8,
                    DisabledDefaultUd2VectorStateTransition
                        .CaptureCompletionCookie) ||
                !ExpectContextStore(bytes, ref cursor, 488))
            {
                return false;
            }

            int branchCount = 0;
            if (!AuditFailureBranches(
                    bytes,
                    cursor,
                    DisabledDefaultUd2VectorStateTransition.SuccessSetupOffset,
                    ref branchCount) ||
                !AuditFailureBranches(
                    bytes,
                    DisabledDefaultUd2VectorStateTransition
                        .TransitionEntryOffset,
                    DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                    ref branchCount) ||
                branchCount != DisabledDefaultUd2VectorStateTransition
                    .AuditedConditionalFailureBranchCount ||
                !AuditFaultFrameRflagsContract(bytes) ||
                !AuditSuccessAndFailurePaths(bytes) ||
                !AuditTransition(bytes) ||
                !AuditTerminalOpcodes(bytes))
            {
                return false;
            }

            return ReadU64(stream +
                    DisabledDefaultUd2VectorStateTransition
                        .ContextAddressLiteralOffset) == ContextAddress
                && bytes[
                    DisabledDefaultUd2VectorStateTransition.Ud2Offset] == 0x0f
                && bytes[
                    DisabledDefaultUd2VectorStateTransition.Ud2Offset + 1] ==
                    0x0b;
        }
        finally
        {
            NativeMemory.AlignedFree(context);
            NativeMemory.AlignedFree(stream);
        }
    }

    private static bool TestEveryContextMutation()
    {
        byte* context = AllocateAligned(PageByteCount, PageByteCount);
        byte* stream = AllocateAligned(PageByteCount, PageByteCount);
        byte* scratch = AllocateAligned(PageByteCount, PageByteCount);
        try
        {
            if (!Prepare(context, PageByteCount).ContextPrepared ||
                !Emit(stream, PageByteCount, context, PageByteCount)
                    .StreamEmitted)
            {
                return false;
            }

            for (int offset = 0; offset <
                DisabledDefaultUd2VectorStateTransition.ContextByteCount;
                offset++)
            {
                Fill(scratch, PageByteCount, Sentinel);
                context[offset] ^= 0xff;
                DisabledDefaultUd2VectorStateTransitionResult rejected =
                    Emit(scratch, PageByteCount, context, PageByteCount);
                context[offset] ^= 0xff;
                if (rejected.Operation !=
                        DisabledDefaultUd2VectorStateTransitionOperation
                            .EmitStream ||
                    rejected.Status !=
                        DisabledDefaultUd2VectorStateTransitionStatus
                            .ContextByteMismatch ||
                    rejected.ContextBytesCompared != 496 ||
                    rejected.StreamBytesWritten != 0 ||
                    rejected.StreamBytesCompared != 0 ||
                    rejected.FirstMismatchOffset !=
                        DisabledDefaultUd2VectorStateTransition
                            .PreparedContextFingerprintOffset ||
                    rejected.DestinationMutated ||
                    rejected.ReadBackPerformed ||
                    rejected.Succeeded ||
                    !IsAll(scratch, PageByteCount, Sentinel))
                {
                    return false;
                }
            }

            return Validate(stream, PageByteCount, context, PageByteCount)
                .ReadBackValidated;
        }
        finally
        {
            NativeMemory.AlignedFree(context);
            NativeMemory.AlignedFree(stream);
            NativeMemory.AlignedFree(scratch);
        }
    }

    private static bool TestEveryStreamMutation()
    {
        byte* context = AllocateAligned(PageByteCount, PageByteCount);
        byte* stream = AllocateAligned(PageByteCount, PageByteCount);
        try
        {
            if (!Prepare(context, PageByteCount).ContextPrepared ||
                !Emit(stream, PageByteCount, context, PageByteCount)
                    .StreamEmitted)
            {
                return false;
            }

            for (int offset = 0; offset < PageByteCount; offset++)
            {
                stream[offset] ^= 0x80;
                DisabledDefaultUd2VectorStateTransitionResult rejected =
                    Validate(stream, PageByteCount, context, PageByteCount);
                stream[offset] ^= 0x80;
                if (rejected.Operation !=
                        DisabledDefaultUd2VectorStateTransitionOperation
                            .ValidateStreamReadBack ||
                    rejected.Status !=
                        DisabledDefaultUd2VectorStateTransitionStatus
                            .StreamByteMismatch ||
                    rejected.ContextBytesCompared != 496 ||
                    rejected.StreamBytesWritten != 0 ||
                    rejected.StreamBytesCompared != offset + 1 ||
                    rejected.FirstMismatchOffset != offset ||
                    rejected.DestinationMutated ||
                    !rejected.ReadBackPerformed ||
                    rejected.Succeeded)
                {
                    return false;
                }
            }

            return Validate(stream, PageByteCount, context, PageByteCount)
                .ReadBackValidated;
        }
        finally
        {
            NativeMemory.AlignedFree(context);
            NativeMemory.AlignedFree(stream);
        }
    }

    private static bool TestSpanParity()
    {
        byte* pointerContext = AllocateAligned(PageByteCount, PageByteCount);
        byte* pointerStream = AllocateAligned(PageByteCount, PageByteCount);
        byte* spanContext = AllocateAligned(PageByteCount, PageByteCount);
        byte* spanStream = AllocateAligned(PageByteCount, PageByteCount);
        try
        {
            Fill(pointerContext, PageByteCount, Sentinel);
            Fill(pointerStream, PageByteCount, Sentinel);
            Fill(spanContext, PageByteCount, Sentinel);
            Fill(spanStream, PageByteCount, Sentinel);

            DisabledDefaultUd2VectorStateTransitionResult pointerPrepared =
                Prepare(pointerContext, 496);
            DisabledDefaultUd2VectorStateTransitionResult pointerEmitted =
                Emit(pointerStream, PageByteCount, pointerContext, 496);
            DisabledDefaultUd2VectorStateTransitionResult pointerValidated =
                Validate(pointerStream, PageByteCount, pointerContext, 496);

            Span<byte> contextSpan = new(spanContext, 496);
            Span<byte> streamSpan = new(spanStream, PageByteCount);
            DisabledDefaultUd2VectorStateTransitionResult spanPrepared =
                DisabledDefaultUd2VectorStateTransition.PrepareContext(
                    contextSpan,
                    ContextAddress,
                    IdtAddress,
                    Ist1StackTop,
                    Ist2StackTop,
                    Ist3StackTop);
            DisabledDefaultUd2VectorStateTransitionResult spanEmitted =
                DisabledDefaultUd2VectorStateTransition.EmitStream(
                    streamSpan,
                    StreamAddress,
                    contextSpan,
                    ContextAddress,
                    IdtAddress,
                    Ist1StackTop,
                    Ist2StackTop,
                    Ist3StackTop);
            DisabledDefaultUd2VectorStateTransitionResult spanValidated =
                DisabledDefaultUd2VectorStateTransition
                    .ValidateStreamReadBack(
                        streamSpan,
                        StreamAddress,
                        contextSpan,
                        ContextAddress,
                        IdtAddress,
                        Ist1StackTop,
                        Ist2StackTop,
                        Ist3StackTop);

            return pointerPrepared.ContextPrepared
                && pointerEmitted.StreamEmitted
                && pointerValidated.ReadBackValidated
                && spanPrepared.ContextPrepared
                && spanEmitted.StreamEmitted
                && spanValidated.ReadBackValidated
                && SameResult(pointerPrepared, spanPrepared)
                && SameResult(pointerEmitted, spanEmitted)
                && SameResult(pointerValidated, spanValidated)
                && new ReadOnlySpan<byte>(pointerContext, 496).SequenceEqual(
                    new ReadOnlySpan<byte>(spanContext, 496))
                && new ReadOnlySpan<byte>(pointerStream, PageByteCount)
                    .SequenceEqual(
                        new ReadOnlySpan<byte>(spanStream, PageByteCount));
        }
        finally
        {
            NativeMemory.AlignedFree(pointerContext);
            NativeMemory.AlignedFree(pointerStream);
            NativeMemory.AlignedFree(spanContext);
            NativeMemory.AlignedFree(spanStream);
        }
    }

    private static bool TestAtomicValidationFailures()
    {
        byte* context = AllocateAligned(AllocationByteCount, PageByteCount);
        byte* stream = AllocateAligned(AllocationByteCount, PageByteCount);
        try
        {
            if (!ExpectPrepareStatus(
                    null, ContextAddress, 496, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .MissingContext) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 495, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .ContextCapacityTooSmall) ||
                !ExpectPrepareStatus(
                    context + 1, ContextAddress, 496, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .MisalignedContextPointer) ||
                !ExpectPrepareStatus(
                    (byte*)0x0000_8000_0000_0000UL,
                    ContextAddress, 496, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalContextPointer) ||
                !ExpectPrepareStatus(
                    (byte*)0xffff_ffff_ffff_fff0UL,
                    ContextAddress, 496, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .ContextPointerSpanOverflow) ||
                !ExpectPrepareStatus(
                    (byte*)0x0000_7fff_ffff_ff00UL,
                    ContextAddress, 512, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalContextPointerSpan) ||
                !ExpectPrepareStatus(
                    context, 0, 496, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NullContextAddress) ||
                !ExpectPrepareStatus(
                    context, ContextAddress + 1, 496, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .MisalignedContextAddress) ||
                !ExpectPrepareStatus(
                    context, 0x0000_8000_0000_0000UL, 496, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalContextAddress) ||
                !ExpectPrepareStatus(
                    context, 0xffff_ffff_ffff_fff0UL, 496, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .ContextAddressSpanOverflow) ||
                !ExpectPrepareStatus(
                    context, 0x0000_7fff_ffff_ff00UL, 512, IdtAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalContextAddressSpan) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 496, 0,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NullIdtAddress) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 496, IdtAddress + 1,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .MisalignedIdtAddress) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 496,
                    0x0000_8000_0000_0000UL,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalIdtAddress) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 496,
                    0xffff_ffff_ffff_f000UL,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .IdtAddressSpanOverflow) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 496, ContextAddress,
                    Ist1StackTop, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .IdtOverlapsContextAddress) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 496, IdtAddress,
                    0, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NullIstStackTop) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 496, IdtAddress,
                    Ist1StackTop + 1, Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .MisalignedIstStackTop) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 496, IdtAddress,
                    0x0000_8000_0000_0000UL,
                    Ist2StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalIstStackTop) ||
                !ExpectPrepareStatus(
                    context, ContextAddress, 496, IdtAddress,
                    Ist1StackTop, Ist1StackTop, Ist3StackTop,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .DuplicateIstStackTop))
            {
                return false;
            }

            Fill(context, AllocationByteCount, Sentinel);
            if (!Prepare(context, PageByteCount).ContextPrepared)
            {
                return false;
            }

            if (!ExpectEmitStatus(
                    null, StreamAddress, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .MissingStream) ||
                !ExpectEmitStatus(
                    stream, StreamAddress, PageByteCount - 1,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .StreamCapacityTooSmall) ||
                !ExpectEmitStatus(
                    stream + 1, StreamAddress, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .MisalignedStreamPointer) ||
                !ExpectEmitStatus(
                    (byte*)0x0000_8000_0000_0000UL,
                    StreamAddress, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalStreamPointer) ||
                !ExpectEmitStatus(
                    (byte*)0xffff_ffff_ffff_f000UL,
                    StreamAddress, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .StreamPointerSpanOverflow) ||
                !ExpectEmitStatus(
                    (byte*)0x0000_7fff_ffff_f000UL,
                    StreamAddress, AllocationByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalStreamPointerSpan) ||
                !ExpectEmitStatus(
                    stream, 0, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NullStreamAddress) ||
                !ExpectEmitStatus(
                    stream, StreamAddress + 1, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .MisalignedStreamAddress) ||
                !ExpectEmitStatus(
                    stream, 0x0000_8000_0000_0000UL, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalStreamAddress) ||
                !ExpectEmitStatus(
                    stream, 0xffff_ffff_ffff_f000UL, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .StreamAddressSpanOverflow) ||
                !ExpectEmitStatus(
                    stream, 0x0000_7fff_ffff_f000UL,
                    AllocationByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .NonCanonicalStreamAddressSpan) ||
                !ExpectEmitStatus(
                    context, StreamAddress, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .StreamOverlapsContextPointer) ||
                !ExpectEmitStatus(
                    stream, ContextAddress, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .StreamOverlapsContextAddress) ||
                !ExpectEmitStatus(
                    stream, IdtAddress, PageByteCount,
                    context, PageByteCount,
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .IdtOverlapsStreamAddress))
            {
                return false;
            }

            Fill(stream, AllocationByteCount, Sentinel);
            DisabledDefaultUd2VectorStateTransitionResult atomic =
                DisabledDefaultUd2VectorStateTransition.EmitStream(
                    stream,
                    StreamAddress + 1,
                    PageByteCount,
                    context,
                    ContextAddress,
                    PageByteCount,
                    IdtAddress,
                    Ist1StackTop,
                    Ist2StackTop,
                    Ist3StackTop);
            if (atomic.Status !=
                    DisabledDefaultUd2VectorStateTransitionStatus
                        .MisalignedStreamAddress ||
                atomic.DestinationMutated || atomic.ReadBackPerformed ||
                atomic.Succeeded ||
                !IsAll(stream, AllocationByteCount, Sentinel))
            {
                return false;
            }

            Fill(stream, AllocationByteCount, Sentinel);
            DisabledDefaultUd2VectorStateTransitionResult adjacent =
                DisabledDefaultUd2VectorStateTransition.EmitStream(
                    stream,
                    ContextAddress + PageByteCount,
                    PageByteCount,
                    context,
                    ContextAddress,
                    PageByteCount,
                    IdtAddress,
                    Ist1StackTop,
                    Ist2StackTop,
                    Ist3StackTop);
            return adjacent.StreamEmitted
                && adjacent.ModeledStreamAddress ==
                    ContextAddress + PageByteCount
                && IsAll(stream + PageByteCount,
                    AllocationByteCount - PageByteCount, Sentinel);
        }
        finally
        {
            NativeMemory.AlignedFree(context);
            NativeMemory.AlignedFree(stream);
        }
    }

    private static bool TestZeroAllocation()
    {
        byte* context = AllocateAligned(PageByteCount, PageByteCount);
        byte* stream = AllocateAligned(PageByteCount, PageByteCount);
        try
        {
            ulong evidence = 0;
            for (int index = 0; index < 4; index++)
            {
                evidence ^= Prepare(context, PageByteCount).ContextFingerprint;
                evidence ^= Emit(stream, PageByteCount, context, PageByteCount)
                    .StreamFingerprint;
                evidence ^= Validate(
                    stream, PageByteCount, context, PageByteCount)
                    .StreamFingerprint;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 16; index++)
            {
                evidence ^= Prepare(context, PageByteCount).ContextFingerprint;
                evidence ^= Emit(stream, PageByteCount, context, PageByteCount)
                    .StreamFingerprint;
                evidence ^= Validate(
                    stream, PageByteCount, context, PageByteCount)
                    .StreamFingerprint;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            return allocated == 0 && evidence == 0;
        }
        finally
        {
            NativeMemory.AlignedFree(context);
            NativeMemory.AlignedFree(stream);
        }
    }

    private static bool TestNoStdLibCompile()
    {
        string repositoryRoot = FindRepositoryRoot();
        string diagnostics = Path.Combine(
            repositoryRoot, @"src\OS.Kernel\Diagnostics");
        string[] productionFiles =
        [
            "InvalidOpcodeProbeAuthority.cs",
            "InvalidOpcodeVectorAuthority.cs",
            "CpuFaultFrame.cs",
            "DisabledDefaultUd2VectorState.cs",
            "OwnedGDTTSSGeneratedAuditedCSharpStubBytes.cs",
            "PopulateAllN256IDTVectorsOwnedGatesNever.cs",
            "DisabledDefaultUd2VectorStateTransition.cs"
        ];
        for (int index = 0; index < productionFiles.Length; index++)
        {
            if (!File.Exists(Path.Combine(diagnostics, productionFiles[index])))
            {
                Console.WriteLine(
                    "M02_013_TRANSITION_NOSTDLIB_MISSING " +
                    productionFiles[index]);
                return false;
            }
        }

        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m02-013-transition-nostdlib-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibShim.cs"),
                NoStdLibShimSource,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            StringBuilder compileItems = new();
            for (int index = 0; index < productionFiles.Length; index++)
            {
                string absolutePath =
                    Path.Combine(diagnostics, productionFiles[index]);
                compileItems.Append("    <Compile Include=\"");
                compileItems.Append(EscapeXml(absolutePath));
                compileItems.Append("\" Link=\"Diagnostics\\");
                compileItems.Append(productionFiles[index]);
                compileItems.AppendLine("\" />");
            }

            string project = NoStdLibProjectSource.Replace(
                "__PRODUCTION_COMPILE_ITEMS__",
                compileItems.ToString(),
                StringComparison.Ordinal);
            File.WriteAllText(
                Path.Combine(workspace, "M02.013.Transition.NoStdLib.csproj"),
                project,
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
            start.ArgumentList.Add("M02.013.Transition.NoStdLib.csproj");
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
                    "Failed to start focused M02-013 transition " +
                    "no-stdlib build.");
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
                Console.WriteLine(
                    "M02_013_TRANSITION_NOSTDLIB_FAIL " +
                    output.Replace('\r', ' ').Replace('\n', ' ').Trim());
                return false;
            }

            return File.Exists(Path.Combine(
                workspace,
                "out",
                "CLROS.M02.Ud2VectorStateTransition.dll"));
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
            FindRepositoryRoot(),
            @"src\OS.Kernel\Diagnostics\DisabledDefaultUd2VectorStateTransition.cs"));
        string pointerCore = RemoveNoStdLibHostBlocks(source);
        string[] forbidden =
        [
            "SharpOS",
            @"references\",
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
            "Console.",
            "System.",
            "CLROS_M02_013_TRANSITION_HOST_READY",
            "CLROS_M02_013_READY",
            "M02_UD2_VECTOR_STATE_VERIFY_FAILED",
            "TEST_START m02.013",
            "TEST_PASS m02.013",
            "class Probes",
            "class FaultVectorCatalog",
            "struct CpuFaultFrame"
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
            && source.Contains(
                "DisabledDefaultUd2VectorState.InvalidOpcodeVector",
                StringComparison.Ordinal)
            && source.Contains(
                "DisabledDefaultUd2VectorState.CpuFaultFrameFieldCount",
                StringComparison.Ordinal)
            && source.Contains("StreamFnv1A64 = 0xe507_0e16_f235_33a3UL",
                StringComparison.Ordinal)
            && source.Contains("HasCalls = false", StringComparison.Ordinal)
            && source.Contains("HasReturns = false", StringComparison.Ordinal)
            && source.Contains("HasExternalRelocations = false",
                StringComparison.Ordinal)
            && source.Contains("UsesFirmwareCallbacks = false",
                StringComparison.Ordinal)
            && source.Contains("ContinuesExecution = false",
                StringComparison.Ordinal)
            && source.Contains("TerminalHalt = true", StringComparison.Ordinal)
            && source.Contains("HostTestsInvokeBytes = false",
                StringComparison.Ordinal)
            && source.Contains("GuardPagesProved = false",
                StringComparison.Ordinal);
    }

    private static bool HasExactContextOffsets()
    {
        return Marshal.OffsetOf<DisabledDefaultUd2VectorStateTransitionContext>(
                nameof(DisabledDefaultUd2VectorStateTransitionContext
                    .OwnedGdtTss)).ToInt32() == 0
            && Marshal.OffsetOf<
                DisabledDefaultUd2VectorStateTransitionContext>(
                    nameof(DisabledDefaultUd2VectorStateTransitionContext
                        .AlignmentReserved)).ToInt32() == 168
            && Marshal.OffsetOf<
                DisabledDefaultUd2VectorStateTransitionContext>(
                    nameof(DisabledDefaultUd2VectorStateTransitionContext
                        .RequestedIdtr)).ToInt32() == 176
            && Marshal.OffsetOf<
                DisabledDefaultUd2VectorStateTransitionContext>(
                    nameof(DisabledDefaultUd2VectorStateTransitionContext
                        .RequestedIdtrReserved)).ToInt32() == 186
            && Marshal.OffsetOf<
                DisabledDefaultUd2VectorStateTransitionContext>(
                    nameof(DisabledDefaultUd2VectorStateTransitionContext
                        .ObservedIdtr)).ToInt32() == 192
            && Marshal.OffsetOf<
                DisabledDefaultUd2VectorStateTransitionContext>(
                    nameof(DisabledDefaultUd2VectorStateTransitionContext
                        .ObservedIdtrReserved)).ToInt32() == 202
            && HasOffset(
                nameof(DisabledDefaultUd2VectorStateTransitionContext
                    .ExpectedIst1StackTop), 208)
            && HasOffset(
                nameof(DisabledDefaultUd2VectorStateTransitionContext
                    .ExpectedIst2StackTop), 216)
            && HasOffset(
                nameof(DisabledDefaultUd2VectorStateTransitionContext
                    .ExpectedIst3StackTop), 224)
            && HasOffset(
                nameof(DisabledDefaultUd2VectorStateTransitionContext
                    .PreparedContextFingerprint), 232)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .ExpectedRip), 240)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .ExpectedRsp), 248)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .ExpectedRflags), 256)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .ExpectedCr2), 264)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .ExpectedCr3), 272)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .ExpectedCs), 280)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .ExpectedSs), 288)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .ExpectedR15), 296)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedVector), 304)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedHasErrorCode), 312)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedErrorCode), 320)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRip), 328)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRsp), 336)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRbp), 344)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRflags), 352)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedCr2), 360)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedCr3), 368)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRax), 376)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRbx), 384)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRcx), 392)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRdx), 400)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRsi), 408)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedRdi), 416)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedR8), 424)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedR9), 432)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedR10), 440)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedR11), 448)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedR12), 456)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedR13), 464)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedR14), 472)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CapturedR15), 480)
            && HasOffset(nameof(DisabledDefaultUd2VectorStateTransitionContext
                .CaptureCompletion), 488);
    }

    private static bool HasExactCompositeTopology()
    {
        return DisabledDefaultUd2VectorStateTransition.CodePageByteCount == 0x1000
            && DisabledDefaultUd2VectorStateTransition.CompositeCodeByteCount ==
                0x2000
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
            && DisabledDefaultUd2VectorStateTransition.Ud2CompositeOffset ==
                0x19ce
            && DisabledDefaultUd2VectorStateTransition.IdtDataOffset == 0
            && DisabledDefaultUd2VectorStateTransition.IdtDataCapacity == 0x1000
            && DisabledDefaultUd2VectorStateTransition.ContextDataOffset ==
                0x1000
            && DisabledDefaultUd2VectorStateTransition.ContextDataCapacity ==
                0x1000
            && DisabledDefaultUd2VectorStateTransition.Ist1DataOffset == 0x2000
            && DisabledDefaultUd2VectorStateTransition.Ist2DataOffset == 0x3000
            && DisabledDefaultUd2VectorStateTransition.Ist3DataOffset == 0x4000
            && DisabledDefaultUd2VectorStateTransition.IstStackDataCapacity ==
                0x1000
            && DisabledDefaultUd2VectorStateTransition
                .Ist1StackTopDataOffset == 0x3000
            && DisabledDefaultUd2VectorStateTransition
                .Ist2StackTopDataOffset == 0x4000
            && DisabledDefaultUd2VectorStateTransition
                .Ist3StackTopDataOffset == 0x5000
            && DisabledDefaultUd2VectorStateTransition.CompositeDataByteCount ==
                0x5000;
    }

    private static bool HasExactNormalizedFrameLayout()
    {
        return DisabledDefaultUd2VectorStateTransition.SavedGprByteCount == 120
            && DisabledDefaultUd2VectorStateTransition.NormalizedFrameByteCount ==
                64
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedVectorStackOffset == 120
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedHasErrorCodeStackOffset == 128
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedErrorCodeStackOffset == 136
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedRipStackOffset == 144
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedCsStackOffset == 152
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedRflagsStackOffset == 160
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedRspStackOffset == 168
            && DisabledDefaultUd2VectorStateTransition
                .NormalizedSsStackOffset == 176;
    }

    private static bool AuditFaultFrameRflagsContract(
        ReadOnlySpan<byte> bytes)
    {
        int currentCs = FindSequence(
            bytes,
            0,
            DisabledDefaultUd2VectorStateTransition.SuccessSetupOffset,
            [0x8c, 0xc8, 0x66, 0x3d, 0x08, 0x00]);
        int capturedRflagsCompare = FindSequence(
            bytes,
            0,
            DisabledDefaultUd2VectorStateTransition.SuccessSetupOffset,
            [0x49, 0x81, 0xbf, 0x60, 0x01, 0x00, 0x00,
                0x46, 0x00, 0x01, 0x00]);
        if (currentCs < 0 || capturedRflagsCompare < 0)
        {
            return false;
        }

        int branchCursor = capturedRflagsCompare + 11;
        return ExpectBytes(bytes, ref branchCursor, [0x0f, 0x85])
            && ReadRel32Target(bytes, branchCursor) ==
                DisabledDefaultUd2VectorStateTransition.FailureSetupOffset;
    }

    private static bool AuditSuccessAndFailurePaths(ReadOnlySpan<byte> bytes)
    {
        int cursor = DisabledDefaultUd2VectorStateTransition
            .SuccessSetupOffset;
        if (!ExpectRipRelativeLoad(
                bytes,
                ref cursor,
                0x48,
                0x8d,
                0x35,
                DisabledDefaultUd2VectorStateTransition
                    .SuccessTranscriptOffset) ||
            cursor + 5 > bytes.Length ||
            bytes[cursor] != 0xb9 ||
            ReadU32(bytes, cursor + 1) !=
                DisabledDefaultUd2VectorStateTransition
                    .SuccessTranscriptByteCount)
        {
            return false;
        }

        cursor += 5;
        if (cursor + 5 > bytes.Length || bytes[cursor] != 0xe9 ||
            ReadRel32Target(bytes, cursor + 1) !=
                DisabledDefaultUd2VectorStateTransition.SerialWriterOffset)
        {
            return false;
        }

        cursor += 5;
        if (cursor !=
            DisabledDefaultUd2VectorStateTransition.FailureSetupOffset ||
            !ExpectRipRelativeLoad(
                bytes,
                ref cursor,
                0x48,
                0x8d,
                0x35,
                DisabledDefaultUd2VectorStateTransition
                    .FailureTranscriptOffset) ||
            cursor + 5 > bytes.Length ||
            bytes[cursor] != 0xb9 ||
            ReadU32(bytes, cursor + 1) !=
                DisabledDefaultUd2VectorStateTransition
                    .FailureTranscriptByteCount)
        {
            return false;
        }

        cursor += 5;
        byte[] serial =
        [
            0x66, 0xba, 0xfd, 0x03,
            0xec, 0xa8, 0x20, 0x74, 0xfb,
            0x66, 0xba, 0xf8, 0x03, 0xac, 0xee,
            0x66, 0xba, 0xfd, 0x03, 0xff, 0xc9, 0x75, 0xed
        ];
        if (cursor !=
                DisabledDefaultUd2VectorStateTransition.SerialWriterOffset ||
            !bytes.Slice(cursor, serial.Length).SequenceEqual(serial) ||
            cursor + 4 !=
                DisabledDefaultUd2VectorStateTransition.SerialPollOffset ||
            cursor + 9 - 5 !=
                DisabledDefaultUd2VectorStateTransition.SerialPollOffset ||
            cursor + serial.Length - 2 + 2 - 0x13 !=
                DisabledDefaultUd2VectorStateTransition.SerialPollOffset)
        {
            return false;
        }

        cursor += serial.Length;
        return cursor ==
                DisabledDefaultUd2VectorStateTransition.TerminalOffset
            && bytes[cursor] == 0xfa
            && bytes[cursor + 1] == 0xf4
            && bytes[cursor + 2] == 0xeb
            && bytes[cursor + 3] == 0xfd
            && cursor + 1 ==
                DisabledDefaultUd2VectorStateTransition.HaltOffset;
    }

    private static bool AuditTransition(ReadOnlySpan<byte> bytes)
    {
        int cursor = DisabledDefaultUd2VectorStateTransition
            .TransitionEntryOffset;
        if (bytes[cursor++] != 0xfa ||
            !ExpectRipRelativeLoad(
                bytes,
                ref cursor,
                0x4c,
                0x8b,
                0x3d,
                DisabledDefaultUd2VectorStateTransition
                    .ContextAddressLiteralOffset) ||
            !ExpectBytes(bytes, ref cursor, [0x4c, 0x39, 0xf9]))
        {
            return false;
        }

        int entryBranch = cursor;
        if (!ExpectBytes(bytes, ref cursor, [0x0f, 0x85]) ||
            ReadRel32Target(bytes, cursor) !=
                DisabledDefaultUd2VectorStateTransition.FailureSetupOffset)
        {
            return false;
        }

        cursor += 4;
        if (cursor != entryBranch + 6 ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x41, 0x0f, 0x01, 0x17]) ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x41, 0x0f, 0x01, 0x47, 0x0a]) ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x66, 0xb8, 0x10, 0x00, 0x8e, 0xd8, 0x8e, 0xc0,
                    0x8e, 0xd0]) ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x66, 0xb8, 0x18, 0x00, 0x0f, 0x00, 0xd8]) ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x41, 0x80, 0x7f, 0x35, 0x8b]) ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x41, 0x0f, 0x01, 0x9f, 0xb0, 0x00, 0x00, 0x00]) ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x41, 0x0f, 0x01, 0x8f, 0xc0, 0x00, 0x00, 0x00]) ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x6a, 0x46, 0x9d, 0x9c, 0x58,
                    0x48, 0x83, 0xf8, 0x46]) ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x49, 0xc7, 0x87, 0x00, 0x01, 0x00, 0x00,
                    0x46, 0x00, 0x01, 0x00]) ||
            !ContainsSequence(
                bytes,
                DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
                DisabledDefaultUd2VectorStateTransition.Ud2Offset,
                [0x49, 0x89, 0xa7, 0xf8, 0x00, 0x00, 0x00]))
        {
            return false;
        }

        int ripLea = FindSequence(
            bytes,
            DisabledDefaultUd2VectorStateTransition.TransitionEntryOffset,
            DisabledDefaultUd2VectorStateTransition.Ud2Offset,
            [0x48, 0x8d, 0x05]);
        if (ripLea < 0 ||
            ReadRel32Target(bytes, ripLea + 3) !=
                DisabledDefaultUd2VectorStateTransition.Ud2Offset)
        {
            return false;
        }

        int seedCursor =
            DisabledDefaultUd2VectorStateTransition.Ud2Offset - 150;
        byte[] rex =
        [
            0x48, 0x48, 0x48, 0x48, 0x48, 0x48, 0x48,
            0x49, 0x49, 0x49, 0x49, 0x49, 0x49, 0x49, 0x49
        ];
        byte[] opcodes =
        [
            0xb8, 0xbb, 0xb9, 0xba, 0xbe, 0xbf, 0xbd,
            0xb8, 0xb9, 0xba, 0xbb, 0xbc, 0xbd, 0xbe, 0xbf
        ];
        for (int index = 0; index < 15; index++)
        {
            if (!ExpectMovAbs(
                    bytes,
                    ref seedCursor,
                    rex[index],
                    opcodes[index],
                    DisabledDefaultUd2VectorStateTransition
                        .RegisterCookieBase + (ulong)index + 1UL))
            {
                return false;
            }
        }

        return seedCursor ==
                DisabledDefaultUd2VectorStateTransition.Ud2Offset
            && bytes[seedCursor] == 0x0f
            && bytes[seedCursor + 1] == 0x0b;
    }

    private static bool AuditTerminalOpcodes(ReadOnlySpan<byte> bytes)
    {
        int terminal = DisabledDefaultUd2VectorStateTransition.TerminalOffset;
        if (!bytes.Slice(terminal, 4).SequenceEqual(
                new byte[] { 0xfa, 0xf4, 0xeb, 0xfd }) ||
            bytes[DisabledDefaultUd2VectorStateTransition
                .TransitionEntryOffset] != 0xfa ||
            CountSequence(bytes, [0x0f, 0x0b]) != 1 ||
            CountByte(bytes, 0xec) != 1 ||
            CountByte(bytes, 0xee) != 1 ||
            CountByte(bytes, 0xc3) != 0 ||
            CountByte(bytes, 0xc2) != 0 ||
            CountByte(bytes, 0xca) != 0 ||
            CountByte(bytes, 0xcb) != 1 ||
            bytes[474] != 0xcb)
        {
            return false;
        }

        for (int offset = 0; offset + 1 < bytes.Length; offset++)
        {
            if (bytes[offset] == 0xff)
            {
                int extension = (bytes[offset + 1] >> 3) & 7;
                if (extension == 2 || extension == 3)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AuditFailureBranches(
        ReadOnlySpan<byte> bytes,
        int start,
        int end,
        ref int branchCount)
    {
        for (int offset = start; offset + 6 <= end; offset++)
        {
            if (bytes[offset] != 0x0f || bytes[offset + 1] != 0x85)
            {
                continue;
            }

            if (ReadRel32Target(bytes, offset + 2) !=
                DisabledDefaultUd2VectorStateTransition.FailureSetupOffset)
            {
                return false;
            }

            branchCount++;
            offset += 5;
        }

        return true;
    }

    private static bool ExpectCapture(
        ReadOnlySpan<byte> bytes,
        ref int cursor,
        int stackOffset,
        int contextOffset)
    {
        if (cursor + 15 > bytes.Length ||
            !bytes.Slice(cursor, 4).SequenceEqual(
                new byte[] { 0x48, 0x8b, 0x84, 0x24 }) ||
            ReadU32(bytes, cursor + 4) != (uint)stackOffset)
        {
            return false;
        }

        cursor += 8;
        return ExpectContextStore(bytes, ref cursor, contextOffset);
    }

    private static bool ExpectContextStore(
        ReadOnlySpan<byte> bytes,
        ref int cursor,
        int contextOffset)
    {
        if (cursor + 7 > bytes.Length ||
            !bytes.Slice(cursor, 3).SequenceEqual(
                new byte[] { 0x49, 0x89, 0x87 }) ||
            ReadU32(bytes, cursor + 3) != (uint)contextOffset)
        {
            return false;
        }

        cursor += 7;
        return true;
    }

    private static bool ExpectMovAbs(
        ReadOnlySpan<byte> bytes,
        ref int cursor,
        byte rex,
        byte opcode,
        ulong value)
    {
        if (cursor + 10 > bytes.Length ||
            bytes[cursor] != rex || bytes[cursor + 1] != opcode ||
            ReadU64(bytes, cursor + 2) != value)
        {
            return false;
        }

        cursor += 10;
        return true;
    }

    private static bool ExpectRipRelativeLoad(
        ReadOnlySpan<byte> bytes,
        ref int cursor,
        byte first,
        byte second,
        byte third,
        int target)
    {
        if (cursor + 7 > bytes.Length ||
            bytes[cursor] != first || bytes[cursor + 1] != second ||
            bytes[cursor + 2] != third ||
            ReadRel32Target(bytes, cursor + 3) != target)
        {
            return false;
        }

        cursor += 7;
        return true;
    }

    private static bool ExpectBytes(
        ReadOnlySpan<byte> bytes,
        ref int cursor,
        ReadOnlySpan<byte> expected)
    {
        if (cursor + expected.Length > bytes.Length ||
            !bytes.Slice(cursor, expected.Length).SequenceEqual(expected))
        {
            return false;
        }

        cursor += expected.Length;
        return true;
    }

    private static bool ContainsSequence(
        ReadOnlySpan<byte> bytes,
        int start,
        int end,
        ReadOnlySpan<byte> sequence)
    {
        return FindSequence(bytes, start, end, sequence) >= 0;
    }

    private static int FindSequence(
        ReadOnlySpan<byte> bytes,
        int start,
        int end,
        ReadOnlySpan<byte> sequence)
    {
        for (int offset = start; offset + sequence.Length <= end; offset++)
        {
            if (bytes.Slice(offset, sequence.Length).SequenceEqual(sequence))
            {
                return offset;
            }
        }

        return -1;
    }

    private static int CountSequence(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<byte> sequence)
    {
        int count = 0;
        for (int offset = 0; offset + sequence.Length <= bytes.Length; offset++)
        {
            if (bytes.Slice(offset, sequence.Length).SequenceEqual(sequence))
            {
                count++;
                offset += sequence.Length - 1;
            }
        }

        return count;
    }

    private static int CountByte(ReadOnlySpan<byte> bytes, byte value)
    {
        int count = 0;
        for (int offset = 0; offset < bytes.Length; offset++)
        {
            if (bytes[offset] == value)
            {
                count++;
            }
        }

        return count;
    }

    private static DisabledDefaultUd2VectorStateTransitionResult Prepare(
        byte* context,
        ulong contextCapacity)
    {
        return DisabledDefaultUd2VectorStateTransition.PrepareContext(
            context,
            ContextAddress,
            contextCapacity,
            IdtAddress,
            Ist1StackTop,
            Ist2StackTop,
            Ist3StackTop);
    }

    private static DisabledDefaultUd2VectorStateTransitionResult Emit(
        byte* stream,
        ulong streamCapacity,
        byte* context,
        ulong contextCapacity)
    {
        return DisabledDefaultUd2VectorStateTransition.EmitStream(
            stream,
            StreamAddress,
            streamCapacity,
            context,
            ContextAddress,
            contextCapacity,
            IdtAddress,
            Ist1StackTop,
            Ist2StackTop,
            Ist3StackTop);
    }

    private static DisabledDefaultUd2VectorStateTransitionResult Validate(
        byte* stream,
        ulong streamCapacity,
        byte* context,
        ulong contextCapacity)
    {
        return DisabledDefaultUd2VectorStateTransition.ValidateStreamReadBack(
            stream,
            StreamAddress,
            streamCapacity,
            context,
            ContextAddress,
            contextCapacity,
            IdtAddress,
            Ist1StackTop,
            Ist2StackTop,
            Ist3StackTop);
    }

    private static bool ExpectPrepareStatus(
        byte* context,
        ulong contextAddress,
        ulong contextCapacity,
        ulong idtAddress,
        ulong ist1StackTop,
        ulong ist2StackTop,
        ulong ist3StackTop,
        DisabledDefaultUd2VectorStateTransitionStatus expected)
    {
        DisabledDefaultUd2VectorStateTransitionResult result =
            DisabledDefaultUd2VectorStateTransition.PrepareContext(
                context,
                contextAddress,
                contextCapacity,
                idtAddress,
                ist1StackTop,
                ist2StackTop,
                ist3StackTop);
        return result.IsInitialized
            && result.Operation ==
                DisabledDefaultUd2VectorStateTransitionOperation.PrepareContext
            && result.Status == expected
            && result.ContextBytesWritten == 0
            && result.StreamBytesWritten == 0
            && !result.DestinationMutated
            && !result.ReadBackPerformed
            && !result.Succeeded;
    }

    private static bool ExpectEmitStatus(
        byte* stream,
        ulong streamAddress,
        ulong streamCapacity,
        byte* context,
        ulong contextCapacity,
        DisabledDefaultUd2VectorStateTransitionStatus expected)
    {
        DisabledDefaultUd2VectorStateTransitionResult result =
            DisabledDefaultUd2VectorStateTransition.EmitStream(
                stream,
                streamAddress,
                streamCapacity,
                context,
                ContextAddress,
                contextCapacity,
                IdtAddress,
                Ist1StackTop,
                Ist2StackTop,
                Ist3StackTop);
        return result.IsInitialized
            && result.Operation ==
                DisabledDefaultUd2VectorStateTransitionOperation.EmitStream
            && result.Status == expected
            && result.StreamBytesWritten == 0
            && result.StreamBytesCompared == 0
            && !result.DestinationMutated
            && !result.ReadBackPerformed
            && !result.Succeeded;
    }

    private static bool SameResult(
        DisabledDefaultUd2VectorStateTransitionResult first,
        DisabledDefaultUd2VectorStateTransitionResult second)
    {
        return first.IsInitialized == second.IsInitialized
            && first.Operation == second.Operation
            && first.Status == second.Status
            && first.ModeledContextAddress == second.ModeledContextAddress
            && first.ModeledStreamAddress == second.ModeledStreamAddress
            && first.OwnedIdtAddress == second.OwnedIdtAddress
            && first.ContextCapacity == second.ContextCapacity
            && first.StreamCapacity == second.StreamCapacity
            && first.ContextBytesWritten == second.ContextBytesWritten
            && first.ContextBytesCompared == second.ContextBytesCompared
            && first.StreamBytesWritten == second.StreamBytesWritten
            && first.StreamBytesCompared == second.StreamBytesCompared
            && first.FirstMismatchOffset == second.FirstMismatchOffset
            && first.ContextFingerprint == second.ContextFingerprint
            && first.StreamFingerprint == second.StreamFingerprint
            && first.ExpectedStreamFingerprint ==
                second.ExpectedStreamFingerprint
            && first.DestinationMutated == second.DestinationMutated
            && first.ReadBackPerformed == second.ReadBackPerformed
            && first.ContextPrepared == second.ContextPrepared
            && first.StreamEmitted == second.StreamEmitted
            && first.ReadBackValidated == second.ReadBackValidated
            && first.Succeeded == second.Succeeded;
    }

    private static void BuildExpectedContext(byte* destination)
    {
        Fill(destination, 496, 0);
        WriteU16(destination, 39);
        WriteU64(destination + 2, ContextAddress + 24);

        byte[] codeDescriptor = [0xff, 0xff, 0, 0, 0, 0x9a, 0xaf, 0];
        byte[] dataDescriptor = [0xff, 0xff, 0, 0, 0, 0x92, 0xcf, 0];
        for (int index = 0; index < 8; index++)
        {
            destination[32 + index] = codeDescriptor[index];
            destination[40 + index] = dataDescriptor[index];
        }

        ulong tssAddress = ContextAddress + 64;
        byte* tssDescriptor = destination + 48;
        tssDescriptor[0] = 103;
        tssDescriptor[2] = (byte)tssAddress;
        tssDescriptor[3] = (byte)(tssAddress >> 8);
        tssDescriptor[4] = (byte)(tssAddress >> 16);
        tssDescriptor[5] = 0x89;
        tssDescriptor[7] = (byte)(tssAddress >> 24);
        tssDescriptor[8] = (byte)(tssAddress >> 32);
        tssDescriptor[9] = (byte)(tssAddress >> 40);
        tssDescriptor[10] = (byte)(tssAddress >> 48);
        tssDescriptor[11] = (byte)(tssAddress >> 56);

        WriteU64(destination + 100, Ist1StackTop);
        WriteU64(destination + 108, Ist2StackTop);
        WriteU64(destination + 116, Ist3StackTop);
        WriteU16(destination + 166, 104);
        WriteU16(destination + 176, 4095);
        WriteU64(destination + 178, IdtAddress);
        WriteU64(destination + 208, Ist1StackTop);
        WriteU64(destination + 216, Ist2StackTop);
        WriteU64(destination + 224, Ist3StackTop);
        WriteU64(
            destination + 232,
            IndependentFnv1A64(new ReadOnlySpan<byte>(destination, 496)));
    }

    private static bool HasOffset(string fieldName, int expected)
    {
        return Marshal.OffsetOf<DisabledDefaultUd2VectorStateTransitionContext>(
            fieldName).ToInt32() == expected;
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
        void* allocation = NativeMemory.AlignedAlloc(
            (nuint)byteCount,
            alignment);
        if (allocation == null)
        {
            throw new OutOfMemoryException(
                "Unable to allocate M02-013 transition test storage.");
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

    private static uint ReadU32(ReadOnlySpan<byte> source, int offset)
    {
        return (uint)source[offset]
            | ((uint)source[offset + 1] << 8)
            | ((uint)source[offset + 2] << 16)
            | ((uint)source[offset + 3] << 24);
    }

    private static int ReadRel32Target(
        ReadOnlySpan<byte> source,
        int displacementOffset)
    {
        int displacement = unchecked((int)ReadU32(source, displacementOffset));
        return displacementOffset + 4 + displacement;
    }

    private static ushort ReadU16(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
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

    private static ulong ReadU64(ReadOnlySpan<byte> source, int offset)
    {
        ulong value = 0;
        for (int index = 0; index < 8; index++)
        {
            value |= (ulong)source[offset + index] << (index * 8);
        }

        return value;
    }

    private static void WriteU16(byte* destination, ushort value)
    {
        destination[0] = (byte)value;
        destination[1] = (byte)(value >> 8);
    }

    private static void WriteU64(byte* destination, ulong value)
    {
        for (int index = 0; index < 8; index++)
        {
            destination[index] = (byte)(value >> (index * 8));
        }
    }

    private static ulong DecodeTssBase(byte* descriptor)
    {
        return descriptor[2]
            | ((ulong)descriptor[3] << 8)
            | ((ulong)descriptor[4] << 16)
            | ((ulong)descriptor[7] << 24)
            | ((ulong)descriptor[8] << 32)
            | ((ulong)descriptor[9] << 40)
            | ((ulong)descriptor[10] << 48)
            | ((ulong)descriptor[11] << 56);
    }

    private static string FindRepositoryRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "CLROS.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "CLROS.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Unable to locate CLROS root.");
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
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
            public sealed class String : Object { }
            public struct Void { }
            public struct Boolean { }
            public struct Byte { }
            public struct Int32 { }
            public struct UInt16 { }
            public struct UInt32 { }
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
            <AssemblyName>CLROS.M02.Ud2VectorStateTransition</AssemblyName>
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
        __PRODUCTION_COMPILE_ITEMS__
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
