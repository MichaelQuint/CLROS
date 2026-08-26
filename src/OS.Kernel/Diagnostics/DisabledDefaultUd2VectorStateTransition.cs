namespace OS.Kernel.Diagnostics;

public enum DisabledDefaultUd2VectorStateTransitionOperation : byte
{
    None = 0,
    PrepareContext = 1,
    EmitStream = 2,
    ValidateStreamReadBack = 3
}

public enum DisabledDefaultUd2VectorStateTransitionStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    MissingContext = 2,
    ContextCapacityTooSmall = 3,
    MisalignedContextPointer = 4,
    NonCanonicalContextPointer = 5,
    ContextPointerSpanOverflow = 6,
    NonCanonicalContextPointerSpan = 7,
    NullContextAddress = 8,
    MisalignedContextAddress = 9,
    NonCanonicalContextAddress = 10,
    ContextAddressSpanOverflow = 11,
    NonCanonicalContextAddressSpan = 12,
    MissingStream = 13,
    StreamCapacityTooSmall = 14,
    MisalignedStreamPointer = 15,
    NonCanonicalStreamPointer = 16,
    StreamPointerSpanOverflow = 17,
    NonCanonicalStreamPointerSpan = 18,
    NullStreamAddress = 19,
    MisalignedStreamAddress = 20,
    NonCanonicalStreamAddress = 21,
    StreamAddressSpanOverflow = 22,
    NonCanonicalStreamAddressSpan = 23,
    StreamOverlapsContextPointer = 24,
    StreamOverlapsContextAddress = 25,
    NullIdtAddress = 26,
    MisalignedIdtAddress = 27,
    NonCanonicalIdtAddress = 28,
    IdtAddressSpanOverflow = 29,
    NonCanonicalIdtAddressSpan = 30,
    IdtOverlapsContextAddress = 31,
    IdtOverlapsStreamAddress = 32,
    NullIstStackTop = 33,
    MisalignedIstStackTop = 34,
    NonCanonicalIstStackTop = 35,
    DuplicateIstStackTop = 36,
    BaseContextPreparationFailed = 37,
    ContextByteMismatch = 38,
    StreamByteMismatch = 39,
    UnexpectedStreamLayout = 40,
    UnexpectedStreamFingerprint = 41
}

/// <summary>
/// Exact caller-owned RCX context for the nonreturning M02-013 transition.
/// The first 168 bytes are the reviewed M02-009 owned GDT/TSS context. This
/// leaf prepares that frozen context first, then replaces only TSS IST1/2/3
/// with the caller's three validated stack tops. The remaining fields own the
/// IDTR image, execution expectations, and the 23-field captured observation.
/// </summary>
public unsafe struct DisabledDefaultUd2VectorStateTransitionContext
{
    public OwnedGdtTssTerminalContext OwnedGdtTss;
    public fixed byte AlignmentReserved[8];
    public fixed byte RequestedIdtr[10];
    public fixed byte RequestedIdtrReserved[6];
    public fixed byte ObservedIdtr[10];
    public fixed byte ObservedIdtrReserved[6];
    public ulong ExpectedIst1StackTop;
    public ulong ExpectedIst2StackTop;
    public ulong ExpectedIst3StackTop;
    public ulong PreparedContextFingerprint;
    public ulong ExpectedRip;
    public ulong ExpectedRsp;
    public ulong ExpectedRflags;
    public ulong ExpectedCr2;
    public ulong ExpectedCr3;
    public ulong ExpectedCs;
    public ulong ExpectedSs;
    public ulong ExpectedR15;
    public ulong CapturedVector;
    public ulong CapturedHasErrorCode;
    public ulong CapturedErrorCode;
    public ulong CapturedRip;
    public ulong CapturedRsp;
    public ulong CapturedRbp;
    public ulong CapturedRflags;
    public ulong CapturedCr2;
    public ulong CapturedCr3;
    public ulong CapturedRax;
    public ulong CapturedRbx;
    public ulong CapturedRcx;
    public ulong CapturedRdx;
    public ulong CapturedRsi;
    public ulong CapturedRdi;
    public ulong CapturedR8;
    public ulong CapturedR9;
    public ulong CapturedR10;
    public ulong CapturedR11;
    public ulong CapturedR12;
    public ulong CapturedR13;
    public ulong CapturedR14;
    public ulong CapturedR15;
    public ulong CaptureCompletion;
}

public readonly struct DisabledDefaultUd2VectorStateTransitionResult
{
    private readonly byte _isInitialized;
    private readonly byte _destinationMutated;
    private readonly byte _readBackPerformed;
    private readonly int _firstMismatchOffset;

    private DisabledDefaultUd2VectorStateTransitionResult(
        DisabledDefaultUd2VectorStateTransitionOperation operation,
        DisabledDefaultUd2VectorStateTransitionStatus status,
        ulong modeledContextAddress,
        ulong modeledStreamAddress,
        ulong ownedIdtAddress,
        ulong contextCapacity,
        ulong streamCapacity,
        int contextBytesWritten,
        int contextBytesCompared,
        int streamBytesWritten,
        int streamBytesCompared,
        int firstMismatchOffset,
        ulong contextFingerprint,
        ulong streamFingerprint,
        bool destinationMutated,
        bool readBackPerformed)
    {
        _isInitialized = 1;
        _destinationMutated = destinationMutated ? (byte)1 : (byte)0;
        _readBackPerformed = readBackPerformed ? (byte)1 : (byte)0;
        _firstMismatchOffset = firstMismatchOffset;
        Operation = operation;
        Status = status;
        ModeledContextAddress = modeledContextAddress;
        ModeledStreamAddress = modeledStreamAddress;
        OwnedIdtAddress = ownedIdtAddress;
        ContextCapacity = contextCapacity;
        StreamCapacity = streamCapacity;
        ContextBytesWritten = contextBytesWritten;
        ContextBytesCompared = contextBytesCompared;
        StreamBytesWritten = streamBytesWritten;
        StreamBytesCompared = streamBytesCompared;
        ContextFingerprint = contextFingerprint;
        StreamFingerprint = streamFingerprint;
    }

    public bool IsInitialized => _isInitialized == 1;

    public DisabledDefaultUd2VectorStateTransitionOperation Operation { get; }

    public DisabledDefaultUd2VectorStateTransitionStatus Status { get; }

    public ulong ModeledContextAddress { get; }

    public ulong ModeledStreamAddress { get; }

    public ulong OwnedIdtAddress { get; }

    public ulong ContextCapacity { get; }

    public ulong StreamCapacity { get; }

    public int ContextBytesWritten { get; }

    public int ContextBytesCompared { get; }

    public int StreamBytesWritten { get; }

    public int StreamBytesCompared { get; }

    public int FirstMismatchOffset =>
        IsInitialized ? _firstMismatchOffset : -1;

    public ulong ContextFingerprint { get; }

    public ulong StreamFingerprint { get; }

    public ulong ExpectedStreamFingerprint =>
        ModeledContextAddress ==
            DisabledDefaultUd2VectorStateTransition
                .CanonicalModeledContextAddress
            ? DisabledDefaultUd2VectorStateTransition.StreamFnv1A64
            : StreamFingerprint;

    public bool DestinationMutated => _destinationMutated == 1;

    public bool ReadBackPerformed => _readBackPerformed == 1;

    public bool ContextPrepared =>
        IsInitialized &&
        Operation ==
            DisabledDefaultUd2VectorStateTransitionOperation.PrepareContext &&
        Status == DisabledDefaultUd2VectorStateTransitionStatus.Success &&
        ContextBytesWritten ==
            DisabledDefaultUd2VectorStateTransition.ContextByteCount &&
        ContextBytesCompared ==
            DisabledDefaultUd2VectorStateTransition.ContextByteCount &&
        StreamBytesWritten == 0 &&
        StreamBytesCompared == 0 &&
        FirstMismatchOffset == -1 &&
        ContextFingerprint != 0 &&
        DestinationMutated &&
        ReadBackPerformed;

    public bool StreamEmitted =>
        IsInitialized &&
        Operation ==
            DisabledDefaultUd2VectorStateTransitionOperation.EmitStream &&
        Status == DisabledDefaultUd2VectorStateTransitionStatus.Success &&
        ContextBytesWritten == 0 &&
        ContextBytesCompared ==
            DisabledDefaultUd2VectorStateTransition.ContextByteCount &&
        StreamBytesWritten ==
            DisabledDefaultUd2VectorStateTransition.StreamByteCount &&
        StreamBytesCompared ==
            DisabledDefaultUd2VectorStateTransition.StreamByteCount &&
        FirstMismatchOffset == -1 &&
        StreamFingerprint == ExpectedStreamFingerprint &&
        DestinationMutated &&
        ReadBackPerformed;

    public bool ReadBackValidated =>
        IsInitialized &&
        Operation == DisabledDefaultUd2VectorStateTransitionOperation
            .ValidateStreamReadBack &&
        Status == DisabledDefaultUd2VectorStateTransitionStatus.Success &&
        ContextBytesWritten == 0 &&
        ContextBytesCompared ==
            DisabledDefaultUd2VectorStateTransition.ContextByteCount &&
        StreamBytesWritten == 0 &&
        StreamBytesCompared ==
            DisabledDefaultUd2VectorStateTransition.StreamByteCount &&
        FirstMismatchOffset == -1 &&
        StreamFingerprint == ExpectedStreamFingerprint &&
        !DestinationMutated &&
        ReadBackPerformed;

    public bool Succeeded =>
        ContextPrepared || StreamEmitted || ReadBackValidated;

    internal static DisabledDefaultUd2VectorStateTransitionResult Pass(
        DisabledDefaultUd2VectorStateTransitionOperation operation,
        ulong modeledContextAddress,
        ulong modeledStreamAddress,
        ulong ownedIdtAddress,
        ulong contextCapacity,
        ulong streamCapacity,
        int contextBytesWritten,
        int streamBytesWritten,
        ulong contextFingerprint,
        ulong streamFingerprint,
        bool destinationMutated)
    {
        return new DisabledDefaultUd2VectorStateTransitionResult(
            operation,
            DisabledDefaultUd2VectorStateTransitionStatus.Success,
            modeledContextAddress,
            modeledStreamAddress,
            ownedIdtAddress,
            contextCapacity,
            streamCapacity,
            contextBytesWritten,
            DisabledDefaultUd2VectorStateTransition.ContextByteCount,
            streamBytesWritten,
            operation == DisabledDefaultUd2VectorStateTransitionOperation
                .PrepareContext
                    ? 0
                    : DisabledDefaultUd2VectorStateTransition.StreamByteCount,
            -1,
            contextFingerprint,
            streamFingerprint,
            destinationMutated,
            true);
    }

    internal static DisabledDefaultUd2VectorStateTransitionResult Fail(
        DisabledDefaultUd2VectorStateTransitionOperation operation,
        DisabledDefaultUd2VectorStateTransitionStatus status,
        ulong modeledContextAddress,
        ulong modeledStreamAddress,
        ulong ownedIdtAddress,
        ulong contextCapacity,
        ulong streamCapacity,
        int contextBytesCompared = 0,
        int streamBytesWritten = 0,
        int streamBytesCompared = 0,
        int firstMismatchOffset = -1,
        ulong contextFingerprint = 0,
        ulong streamFingerprint = 0,
        bool destinationMutated = false,
        bool readBackPerformed = false)
    {
        return new DisabledDefaultUd2VectorStateTransitionResult(
            operation,
            status,
            modeledContextAddress,
            modeledStreamAddress,
            ownedIdtAddress,
            contextCapacity,
            streamCapacity,
            0,
            contextBytesCompared,
            streamBytesWritten,
            streamBytesCompared,
            firstMismatchOffset,
            contextFingerprint,
            streamFingerprint,
            destinationMutated,
            readBackPerformed);
    }
}

/// <summary>
/// Materializes and validates a frozen, placement-independent x64 terminal
/// transition as ordinary caller-owned bytes. RCX names the prepared context.
/// Host code never invokes the emitted privileged bytes.
/// </summary>
public static unsafe class DisabledDefaultUd2VectorStateTransition
{
    public const int BaseContextByteCount =
        OwnedGDTTSSGeneratedAuditedCSharpStubBytes.ContextByteCount;
    public const int ContextByteCount = 496;
    public const int StreamByteCount = 4096;

    public const int RequestedIdtrOffset = 176;
    public const int ObservedIdtrOffset = 192;
    public const int ExpectedIst1StackTopOffset = 208;
    public const int ExpectedIst2StackTopOffset = 216;
    public const int ExpectedIst3StackTopOffset = 224;
    public const int PreparedContextFingerprintOffset = 232;
    public const int ExpectedRipOffset = 240;
    public const int ExpectedRspOffset = 248;
    public const int ExpectedRflagsOffset = 256;
    public const int ExpectedCr2Offset = 264;
    public const int ExpectedCr3Offset = 272;
    public const int ExpectedCsOffset = 280;
    public const int ExpectedSsOffset = 288;
    public const int ExpectedR15Offset = 296;
    public const int CapturedVectorOffset = 304;
    public const int CapturedHasErrorCodeOffset = 312;
    public const int CapturedErrorCodeOffset = 320;
    public const int CapturedRipOffset = 328;
    public const int CapturedRspOffset = 336;
    public const int CapturedRbpOffset = 344;
    public const int CapturedRflagsOffset = 352;
    public const int CapturedCr2Offset = 360;
    public const int CapturedCr3Offset = 368;
    public const int CapturedRaxOffset = 376;
    public const int CapturedRbxOffset = 384;
    public const int CapturedRcxOffset = 392;
    public const int CapturedRdxOffset = 400;
    public const int CapturedRsiOffset = 408;
    public const int CapturedRdiOffset = 416;
    public const int CapturedR8Offset = 424;
    public const int CapturedR9Offset = 432;
    public const int CapturedR10Offset = 440;
    public const int CapturedR11Offset = 448;
    public const int CapturedR12Offset = 456;
    public const int CapturedR13Offset = 464;
    public const int CapturedR14Offset = 472;
    public const int CapturedR15Offset = 480;
    public const int CaptureCompletionOffset = 488;

    public const int TssIst1Offset =
        OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssOffset + 36;
    public const int TssIst2Offset =
        OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssOffset + 44;
    public const int TssIst3Offset =
        OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssOffset + 52;

    public const int CommonHandlerOffset = 0;
    public const int SuccessSetupOffset = 920;
    public const int FailureSetupOffset = 937;
    public const int SerialWriterOffset = 949;
    public const int SerialPollOffset = 953;
    public const int TerminalOffset = 972;
    public const int HaltOffset = 973;
    public const int SuccessTranscriptOffset = 976;
    public const int FailureTranscriptOffset = 1317;
    public const int ContextAddressLiteralOffset = 1400;
    public const int HandlerBlockEndOffset = 1408;
    public const int TransitionEntryOffset = 0x0800;
    public const int Ud2Offset = 0x09ce;
    public const int TransitionEndOffset = 0x09d0;

    public const int CodePageByteCount = 0x1000;
    public const int CompositeCodeByteCount = 0x2000;
    public const int NormalizedSlotsCompositeOffset = 0x0000;
    public const int NormalizedSlotsCompositeCapacity = 0x1000;
    public const int CommonHandlerCompositeOffset = 0x1000;
    public const int CommonHandlerCompositeCapacity = 0x0800;
    public const int TransitionEntryCompositeOffset = 0x1800;
    public const int TransitionCompositeCapacity = 0x0800;
    public const int Ud2CompositeOffset = CommonHandlerCompositeOffset + Ud2Offset;
    public const int IdtDataOffset = 0x0000;
    public const int IdtDataCapacity = 0x1000;
    public const int ContextDataOffset = 0x1000;
    public const int ContextDataCapacity = 0x1000;
    public const int Ist1DataOffset = 0x2000;
    public const int Ist2DataOffset = 0x3000;
    public const int Ist3DataOffset = 0x4000;
    public const int IstStackDataCapacity = 0x1000;
    public const int Ist1StackTopDataOffset = 0x3000;
    public const int Ist2StackTopDataOffset = 0x4000;
    public const int Ist3StackTopDataOffset = 0x5000;
    public const int CompositeDataByteCount = 0x5000;

    public const int SuccessTranscriptByteCount = 341;
    public const int FailureTranscriptByteCount = 83;
    public const int SavedGprByteCount = 120;
    public const int NormalizedFrameByteCount = 64;
    public const int NormalizedVectorStackOffset = 120;
    public const int NormalizedHasErrorCodeStackOffset = 128;
    public const int NormalizedErrorCodeStackOffset = 136;
    public const int NormalizedRipStackOffset = 144;
    public const int NormalizedCsStackOffset = 152;
    public const int NormalizedRflagsStackOffset = 160;
    public const int NormalizedRspStackOffset = 168;
    public const int NormalizedSsStackOffset = 176;

    public const byte InvalidOpcodeVector =
        DisabledDefaultUd2VectorState.InvalidOpcodeVector;
    public const int RegisterFieldCount =
        DisabledDefaultUd2VectorState.CpuFaultFrameFieldCount;
    public const ushort OwnedIdtrLimit =
        PopulateAllN256IDTVectorsOwnedGatesNever.TableByteCount - 1;
    public const ulong ContextAlignment = 16;
    public const ulong StreamAlignment = CodePageByteCount;
    public const ulong IstStackTopAlignment = 16;
    public const ulong ExpectedRflags = 0x46;
    public const ulong CaptureCompletionCookie = 0x4d30_3231_3343_4150UL;
    public const ulong RegisterCookieBase = 0x4d02_0130_0000_0000UL;

    public const ushort Com1DataPort = 0x03f8;
    public const ushort Com1LineStatusPort = 0x03fd;
    public const byte Com1TransmitterHoldingRegisterEmptyMask = 0x20;

    public const ulong Fnv1A64OffsetBasis = 14_695_981_039_346_656_037UL;
    public const ulong Fnv1A64Prime = 1_099_511_628_211UL;
    public const ulong CanonicalModeledContextAddress = 0x0030_1000UL;
    public const ulong StreamFnv1A64 = 0xe57d_f95f_6f34_fc32UL;
    public const ulong StreamSha256Word0BigEndian = 0xf039_c657_6a04_298cUL;
    public const ulong StreamSha256Word1BigEndian = 0x7c92_5b63_5c45_fb47UL;
    public const ulong StreamSha256Word2BigEndian = 0x6f77_0354_baea_ac28UL;
    public const ulong StreamSha256Word3BigEndian = 0x2dc3_392f_7a84_d115UL;

    public const int AuditedConditionalFailureBranchCount = 37;
    public const int AuditedSavedGprCount = 15;
    public const int AuditedUd2InstructionCount = 1;
    public const int AuditedCliInstructionCount = 2;
    public const int AuditedHltInstructionCount = 1;
    public const int AuditedInInstructionCount = 1;
    public const int AuditedOutInstructionCount = 1;

    public const bool UsesWindowsX64Abi = true;
    public const bool ContextPassedInRcx = true;
    public const bool HasCalls = false;
    public const bool HasReturns = false;
    public const bool HasExternalRelocations = false;
    public const bool UsesFirmwareCallbacks = false;
    public const bool RestoresFirmwareDescriptorState = false;
    public const bool ContinuesExecution = false;
    public const bool TerminalHalt = true;
    public const bool HostTestsInvokeBytes = false;
    public const bool GuardPagesProved = false;

#if !NOSTDLIB_EFI
    public static DisabledDefaultUd2VectorStateTransitionResult PrepareContext(
        Span<byte> destination,
        ulong modeledContextAddress,
        ulong ownedIdtAddress,
        ulong ist1StackTop,
        ulong ist2StackTop,
        ulong ist3StackTop)
    {
        fixed (byte* destinationPointer = destination)
        {
            return PrepareContext(
                destinationPointer,
                modeledContextAddress,
                (ulong)destination.Length,
                ownedIdtAddress,
                ist1StackTop,
                ist2StackTop,
                ist3StackTop);
        }
    }

    public static DisabledDefaultUd2VectorStateTransitionResult EmitStream(
        Span<byte> destination,
        ulong modeledStreamAddress,
        ReadOnlySpan<byte> context,
        ulong modeledContextAddress,
        ulong ownedIdtAddress,
        ulong ist1StackTop,
        ulong ist2StackTop,
        ulong ist3StackTop)
    {
        fixed (byte* destinationPointer = destination)
        fixed (byte* contextPointer = context)
        {
            return EmitStream(
                destinationPointer,
                modeledStreamAddress,
                (ulong)destination.Length,
                contextPointer,
                modeledContextAddress,
                (ulong)context.Length,
                ownedIdtAddress,
                ist1StackTop,
                ist2StackTop,
                ist3StackTop);
        }
    }

    public static DisabledDefaultUd2VectorStateTransitionResult
        ValidateStreamReadBack(
            ReadOnlySpan<byte> source,
            ulong modeledStreamAddress,
            ReadOnlySpan<byte> context,
            ulong modeledContextAddress,
            ulong ownedIdtAddress,
            ulong ist1StackTop,
            ulong ist2StackTop,
            ulong ist3StackTop)
    {
        fixed (byte* sourcePointer = source)
        fixed (byte* contextPointer = context)
        {
            return ValidateStreamReadBack(
                sourcePointer,
                modeledStreamAddress,
                (ulong)source.Length,
                contextPointer,
                modeledContextAddress,
                (ulong)context.Length,
                ownedIdtAddress,
                ist1StackTop,
                ist2StackTop,
                ist3StackTop);
        }
    }
#endif

    public static DisabledDefaultUd2VectorStateTransitionResult PrepareContext(
        DisabledDefaultUd2VectorStateTransitionContext* destination,
        ulong modeledContextAddress,
        ulong destinationCapacity,
        ulong ownedIdtAddress,
        ulong ist1StackTop,
        ulong ist2StackTop,
        ulong ist3StackTop)
    {
        return PrepareContext(
            (byte*)destination,
            modeledContextAddress,
            destinationCapacity,
            ownedIdtAddress,
            ist1StackTop,
            ist2StackTop,
            ist3StackTop);
    }

    public static DisabledDefaultUd2VectorStateTransitionResult PrepareContext(
        byte* destination,
        ulong modeledContextAddress,
        ulong destinationCapacity,
        ulong ownedIdtAddress,
        ulong ist1StackTop,
        ulong ist2StackTop,
        ulong ist3StackTop)
    {
        DisabledDefaultUd2VectorStateTransitionStatus status =
            ValidatePreparationInputs(
                destination,
                modeledContextAddress,
                destinationCapacity,
                ownedIdtAddress,
                ist1StackTop,
                ist2StackTop,
                ist3StackTop);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.PrepareContext,
                status,
                modeledContextAddress,
                0,
                ownedIdtAddress,
                destinationCapacity,
                0);
        }

        OwnedGdtTssTerminalStubResult baseResult =
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.PrepareContext(
                destination,
                modeledContextAddress,
                BaseContextByteCount);
        if (!baseResult.ContextPrepared)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.PrepareContext,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .BaseContextPreparationFailed,
                modeledContextAddress,
                0,
                ownedIdtAddress,
                destinationCapacity,
                0,
                contextBytesCompared: baseResult.ContextBytesCompared,
                firstMismatchOffset: baseResult.FirstMismatchOffset,
                contextFingerprint: baseResult.ContextFingerprint,
                destinationMutated: baseResult.ContextBytesWritten != 0);
        }

        for (int offset = BaseContextByteCount;
            offset < ContextByteCount;
            offset++)
        {
            destination[offset] = 0;
        }

        WriteU64(destination + TssIst1Offset, ist1StackTop);
        WriteU64(destination + TssIst2Offset, ist2StackTop);
        WriteU64(destination + TssIst3Offset, ist3StackTop);
        WriteU16(destination + RequestedIdtrOffset, OwnedIdtrLimit);
        WriteU64(destination + RequestedIdtrOffset + 2, ownedIdtAddress);
        WriteU64(destination + ExpectedIst1StackTopOffset, ist1StackTop);
        WriteU64(destination + ExpectedIst2StackTopOffset, ist2StackTop);
        WriteU64(destination + ExpectedIst3StackTopOffset, ist3StackTop);

        ulong preparedFingerprint = ComputePreparedContextFingerprint(
            destination);
        WriteU64(
            destination + PreparedContextFingerprintOffset,
            preparedFingerprint);

        status = ValidatePreparedContext(
            destination,
            modeledContextAddress,
            ownedIdtAddress,
            ist1StackTop,
            ist2StackTop,
            ist3StackTop,
            out int bytesCompared,
            out int mismatchOffset,
            out ulong contextFingerprint);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.PrepareContext,
                status,
                modeledContextAddress,
                0,
                ownedIdtAddress,
                destinationCapacity,
                0,
                contextBytesCompared: bytesCompared,
                firstMismatchOffset: mismatchOffset,
                contextFingerprint: contextFingerprint,
                destinationMutated: true,
                readBackPerformed: true);
        }

        return DisabledDefaultUd2VectorStateTransitionResult.Pass(
            DisabledDefaultUd2VectorStateTransitionOperation.PrepareContext,
            modeledContextAddress,
            0,
            ownedIdtAddress,
            destinationCapacity,
            0,
            ContextByteCount,
            0,
            contextFingerprint,
            0,
            true);
    }

    public static DisabledDefaultUd2VectorStateTransitionResult EmitStream(
        byte* destination,
        ulong modeledStreamAddress,
        ulong destinationCapacity,
        DisabledDefaultUd2VectorStateTransitionContext* context,
        ulong modeledContextAddress,
        ulong contextCapacity,
        ulong ownedIdtAddress,
        ulong ist1StackTop,
        ulong ist2StackTop,
        ulong ist3StackTop)
    {
        return EmitStream(
            destination,
            modeledStreamAddress,
            destinationCapacity,
            (byte*)context,
            modeledContextAddress,
            contextCapacity,
            ownedIdtAddress,
            ist1StackTop,
            ist2StackTop,
            ist3StackTop);
    }

    public static DisabledDefaultUd2VectorStateTransitionResult EmitStream(
        byte* destination,
        ulong modeledStreamAddress,
        ulong destinationCapacity,
        byte* context,
        ulong modeledContextAddress,
        ulong contextCapacity,
        ulong ownedIdtAddress,
        ulong ist1StackTop,
        ulong ist2StackTop,
        ulong ist3StackTop)
    {
        DisabledDefaultUd2VectorStateTransitionStatus status =
            ValidateAllInputs(
                destination,
                modeledStreamAddress,
                destinationCapacity,
                context,
                modeledContextAddress,
                contextCapacity,
                ownedIdtAddress,
                ist1StackTop,
                ist2StackTop,
                ist3StackTop);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.EmitStream,
                status,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                destinationCapacity);
        }

        status = ValidatePreparedContext(
            context,
            modeledContextAddress,
            ownedIdtAddress,
            ist1StackTop,
            ist2StackTop,
            ist3StackTop,
            out int contextBytesCompared,
            out int contextMismatchOffset,
            out ulong contextFingerprint);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.EmitStream,
                status,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                destinationCapacity,
                contextBytesCompared,
                firstMismatchOffset: contextMismatchOffset,
                contextFingerprint: contextFingerprint);
        }

        StreamBuilder expected = StreamBuilder.CreateCount();
        BuildStream(
            ref expected,
            modeledContextAddress,
            out bool expectedLayoutMatched);
        if (!expectedLayoutMatched || expected.Offset != StreamByteCount)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.EmitStream,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .UnexpectedStreamLayout,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                destinationCapacity,
                contextBytesCompared,
                contextFingerprint: contextFingerprint,
                streamFingerprint: expected.Fingerprint);
        }

        if (modeledContextAddress == CanonicalModeledContextAddress &&
            expected.Fingerprint != StreamFnv1A64)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.EmitStream,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .UnexpectedStreamFingerprint,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                destinationCapacity,
                contextBytesCompared,
                contextFingerprint: contextFingerprint,
                streamFingerprint: expected.Fingerprint);
        }

        StreamBuilder writer = StreamBuilder.CreateWriter(destination);
        BuildStream(
            ref writer,
            modeledContextAddress,
            out bool writeLayoutMatched);
        if (!writeLayoutMatched || writer.Offset != StreamByteCount)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.EmitStream,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .UnexpectedStreamLayout,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                destinationCapacity,
                contextBytesCompared,
                streamBytesWritten: writer.Offset,
                contextFingerprint: contextFingerprint,
                streamFingerprint: writer.Fingerprint,
                destinationMutated: writer.Offset != 0);
        }

        StreamBuilder reader = StreamBuilder.CreateReader(destination);
        BuildStream(
            ref reader,
            modeledContextAddress,
            out bool readLayoutMatched);
        if (!readLayoutMatched || reader.Offset != StreamByteCount)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.EmitStream,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .UnexpectedStreamLayout,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                destinationCapacity,
                contextBytesCompared,
                writer.Offset,
                reader.BytesCompared,
                reader.FirstMismatchOffset,
                contextFingerprint,
                ComputeFingerprint(destination, StreamByteCount),
                true,
                true);
        }

        ulong observedFingerprint = ComputeFingerprint(
            destination,
            StreamByteCount);
        if (reader.FirstMismatchOffset >= 0)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.EmitStream,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .StreamByteMismatch,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                destinationCapacity,
                contextBytesCompared,
                writer.Offset,
                reader.BytesCompared,
                reader.FirstMismatchOffset,
                contextFingerprint,
                observedFingerprint,
                true,
                true);
        }

        if (observedFingerprint != expected.Fingerprint)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation.EmitStream,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .UnexpectedStreamFingerprint,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                destinationCapacity,
                contextBytesCompared,
                writer.Offset,
                StreamByteCount,
                -1,
                contextFingerprint,
                observedFingerprint,
                true,
                true);
        }

        return DisabledDefaultUd2VectorStateTransitionResult.Pass(
            DisabledDefaultUd2VectorStateTransitionOperation.EmitStream,
            modeledContextAddress,
            modeledStreamAddress,
            ownedIdtAddress,
            contextCapacity,
            destinationCapacity,
            0,
            StreamByteCount,
            contextFingerprint,
            observedFingerprint,
            true);
    }

    public static DisabledDefaultUd2VectorStateTransitionResult
        ValidateStreamReadBack(
            byte* source,
            ulong modeledStreamAddress,
            ulong sourceCapacity,
            DisabledDefaultUd2VectorStateTransitionContext* context,
            ulong modeledContextAddress,
            ulong contextCapacity,
            ulong ownedIdtAddress,
            ulong ist1StackTop,
            ulong ist2StackTop,
            ulong ist3StackTop)
    {
        return ValidateStreamReadBack(
            source,
            modeledStreamAddress,
            sourceCapacity,
            (byte*)context,
            modeledContextAddress,
            contextCapacity,
            ownedIdtAddress,
            ist1StackTop,
            ist2StackTop,
            ist3StackTop);
    }

    public static DisabledDefaultUd2VectorStateTransitionResult
        ValidateStreamReadBack(
            byte* source,
            ulong modeledStreamAddress,
            ulong sourceCapacity,
            byte* context,
            ulong modeledContextAddress,
            ulong contextCapacity,
            ulong ownedIdtAddress,
            ulong ist1StackTop,
            ulong ist2StackTop,
            ulong ist3StackTop)
    {
        DisabledDefaultUd2VectorStateTransitionStatus status =
            ValidateAllInputs(
                source,
                modeledStreamAddress,
                sourceCapacity,
                context,
                modeledContextAddress,
                contextCapacity,
                ownedIdtAddress,
                ist1StackTop,
                ist2StackTop,
                ist3StackTop);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack,
                status,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                sourceCapacity);
        }

        status = ValidatePreparedContext(
            context,
            modeledContextAddress,
            ownedIdtAddress,
            ist1StackTop,
            ist2StackTop,
            ist3StackTop,
            out int contextBytesCompared,
            out int contextMismatchOffset,
            out ulong contextFingerprint);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack,
                status,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                sourceCapacity,
                contextBytesCompared,
                firstMismatchOffset: contextMismatchOffset,
                contextFingerprint: contextFingerprint);
        }

        StreamBuilder reader = StreamBuilder.CreateReader(source);
        BuildStream(
            ref reader,
            modeledContextAddress,
            out bool layoutMatched);
        ulong observedFingerprint = ComputeFingerprint(source, StreamByteCount);
        if (!layoutMatched || reader.Offset != StreamByteCount)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .UnexpectedStreamLayout,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                sourceCapacity,
                contextBytesCompared,
                streamBytesCompared: reader.BytesCompared,
                firstMismatchOffset: reader.FirstMismatchOffset,
                contextFingerprint: contextFingerprint,
                streamFingerprint: observedFingerprint,
                readBackPerformed: true);
        }

        if (reader.FirstMismatchOffset >= 0)
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .StreamByteMismatch,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                sourceCapacity,
                contextBytesCompared,
                streamBytesCompared: reader.BytesCompared,
                firstMismatchOffset: reader.FirstMismatchOffset,
                contextFingerprint: contextFingerprint,
                streamFingerprint: observedFingerprint,
                readBackPerformed: true);
        }

        if (modeledContextAddress == CanonicalModeledContextAddress &&
            (reader.Fingerprint != StreamFnv1A64 ||
            observedFingerprint != StreamFnv1A64))
        {
            return DisabledDefaultUd2VectorStateTransitionResult.Fail(
                DisabledDefaultUd2VectorStateTransitionOperation
                    .ValidateStreamReadBack,
                DisabledDefaultUd2VectorStateTransitionStatus
                    .UnexpectedStreamFingerprint,
                modeledContextAddress,
                modeledStreamAddress,
                ownedIdtAddress,
                contextCapacity,
                sourceCapacity,
                contextBytesCompared,
                streamBytesCompared: StreamByteCount,
                contextFingerprint: contextFingerprint,
                streamFingerprint: observedFingerprint,
                readBackPerformed: true);
        }

        return DisabledDefaultUd2VectorStateTransitionResult.Pass(
            DisabledDefaultUd2VectorStateTransitionOperation
                .ValidateStreamReadBack,
            modeledContextAddress,
            modeledStreamAddress,
            ownedIdtAddress,
            contextCapacity,
            sourceCapacity,
            0,
            0,
            contextFingerprint,
            observedFingerprint,
            false);
    }

    public static bool IsCanonicalX64Address(ulong address)
    {
        ulong highBits = address >> 48;
        bool signBitSet = ((address >> 47) & 1UL) != 0;
        return signBitSet ? highBits == 0xffffUL : highBits == 0;
    }

    private static DisabledDefaultUd2VectorStateTransitionStatus
        ValidatePreparationInputs(
            byte* context,
            ulong modeledContextAddress,
            ulong contextCapacity,
            ulong ownedIdtAddress,
            ulong ist1StackTop,
            ulong ist2StackTop,
            ulong ist3StackTop)
    {
        DisabledDefaultUd2VectorStateTransitionStatus status =
            ValidateContextStorage(
                context,
                modeledContextAddress,
                contextCapacity);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return status;
        }

        status = ValidateIdtAddress(ownedIdtAddress);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return status;
        }

        if (RangesOverlap(
                modeledContextAddress,
                contextCapacity,
                ownedIdtAddress,
                PopulateAllN256IDTVectorsOwnedGatesNever.TableByteCount))
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .IdtOverlapsContextAddress;
        }

        return ValidateIstStackTops(
            ist1StackTop,
            ist2StackTop,
            ist3StackTop);
    }

    private static DisabledDefaultUd2VectorStateTransitionStatus
        ValidateAllInputs(
            byte* stream,
            ulong modeledStreamAddress,
            ulong streamCapacity,
            byte* context,
            ulong modeledContextAddress,
            ulong contextCapacity,
            ulong ownedIdtAddress,
            ulong ist1StackTop,
            ulong ist2StackTop,
            ulong ist3StackTop)
    {
        DisabledDefaultUd2VectorStateTransitionStatus status =
            ValidatePreparationInputs(
                context,
                modeledContextAddress,
                contextCapacity,
                ownedIdtAddress,
                ist1StackTop,
                ist2StackTop,
                ist3StackTop);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return status;
        }

        status = ValidateStreamStorage(
            stream,
            modeledStreamAddress,
            streamCapacity);
        if (status != DisabledDefaultUd2VectorStateTransitionStatus.Success)
        {
            return status;
        }

        if (RangesOverlap(
                (ulong)stream,
                streamCapacity,
                (ulong)context,
                contextCapacity))
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .StreamOverlapsContextPointer;
        }

        if (RangesOverlap(
                modeledStreamAddress,
                streamCapacity,
                modeledContextAddress,
                contextCapacity))
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .StreamOverlapsContextAddress;
        }

        if (RangesOverlap(
                modeledStreamAddress,
                streamCapacity,
                ownedIdtAddress,
                PopulateAllN256IDTVectorsOwnedGatesNever.TableByteCount))
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .IdtOverlapsStreamAddress;
        }

        return DisabledDefaultUd2VectorStateTransitionStatus.Success;
    }

    private static DisabledDefaultUd2VectorStateTransitionStatus
        ValidateContextStorage(
            byte* storage,
            ulong modeledAddress,
            ulong capacity)
    {
        if (capacity < ContextByteCount)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .ContextCapacityTooSmall;
        }

        if (storage == null)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus.MissingContext;
        }

        return ValidatePointerAndModeledAddress(
            (ulong)storage,
            modeledAddress,
            capacity,
            ContextAlignment,
            DisabledDefaultUd2VectorStateTransitionStatus
                .MisalignedContextPointer,
            DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalContextPointer,
            DisabledDefaultUd2VectorStateTransitionStatus
                .ContextPointerSpanOverflow,
            DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalContextPointerSpan,
            DisabledDefaultUd2VectorStateTransitionStatus.NullContextAddress,
            DisabledDefaultUd2VectorStateTransitionStatus
                .MisalignedContextAddress,
            DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalContextAddress,
            DisabledDefaultUd2VectorStateTransitionStatus
                .ContextAddressSpanOverflow,
            DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalContextAddressSpan);
    }

    private static DisabledDefaultUd2VectorStateTransitionStatus
        ValidateStreamStorage(
            byte* storage,
            ulong modeledAddress,
            ulong capacity)
    {
        if (capacity < StreamByteCount)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .StreamCapacityTooSmall;
        }

        if (storage == null)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus.MissingStream;
        }

        return ValidatePointerAndModeledAddress(
            (ulong)storage,
            modeledAddress,
            capacity,
            StreamAlignment,
            DisabledDefaultUd2VectorStateTransitionStatus
                .MisalignedStreamPointer,
            DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalStreamPointer,
            DisabledDefaultUd2VectorStateTransitionStatus
                .StreamPointerSpanOverflow,
            DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalStreamPointerSpan,
            DisabledDefaultUd2VectorStateTransitionStatus.NullStreamAddress,
            DisabledDefaultUd2VectorStateTransitionStatus
                .MisalignedStreamAddress,
            DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalStreamAddress,
            DisabledDefaultUd2VectorStateTransitionStatus
                .StreamAddressSpanOverflow,
            DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalStreamAddressSpan);
    }

    private static DisabledDefaultUd2VectorStateTransitionStatus
        ValidatePointerAndModeledAddress(
            ulong pointerAddress,
            ulong modeledAddress,
            ulong capacity,
            ulong alignment,
            DisabledDefaultUd2VectorStateTransitionStatus misalignedPointer,
            DisabledDefaultUd2VectorStateTransitionStatus nonCanonicalPointer,
            DisabledDefaultUd2VectorStateTransitionStatus pointerOverflow,
            DisabledDefaultUd2VectorStateTransitionStatus pointerSpan,
            DisabledDefaultUd2VectorStateTransitionStatus nullAddress,
            DisabledDefaultUd2VectorStateTransitionStatus misalignedAddress,
            DisabledDefaultUd2VectorStateTransitionStatus nonCanonicalAddress,
            DisabledDefaultUd2VectorStateTransitionStatus addressOverflow,
            DisabledDefaultUd2VectorStateTransitionStatus addressSpan)
    {
        if ((pointerAddress & (alignment - 1)) != 0)
        {
            return misalignedPointer;
        }

        if (!IsCanonicalX64Address(pointerAddress))
        {
            return nonCanonicalPointer;
        }

        if (pointerAddress > ~0UL - capacity)
        {
            return pointerOverflow;
        }

        if (!IsCanonicalSpan(pointerAddress, capacity))
        {
            return pointerSpan;
        }

        if (modeledAddress == 0)
        {
            return nullAddress;
        }

        if ((modeledAddress & (alignment - 1)) != 0)
        {
            return misalignedAddress;
        }

        if (!IsCanonicalX64Address(modeledAddress))
        {
            return nonCanonicalAddress;
        }

        if (modeledAddress > ~0UL - capacity)
        {
            return addressOverflow;
        }

        if (!IsCanonicalSpan(modeledAddress, capacity))
        {
            return addressSpan;
        }

        return DisabledDefaultUd2VectorStateTransitionStatus.Success;
    }

    private static DisabledDefaultUd2VectorStateTransitionStatus
        ValidateIdtAddress(ulong address)
    {
        if (address == 0)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus.NullIdtAddress;
        }

        ulong alignment =
            PopulateAllN256IDTVectorsOwnedGatesNever.TableAlignment;
        if ((address & (alignment - 1)) != 0)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .MisalignedIdtAddress;
        }

        if (!IsCanonicalX64Address(address))
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalIdtAddress;
        }

        ulong byteCount =
            PopulateAllN256IDTVectorsOwnedGatesNever.TableByteCount;
        if (address > ~0UL - byteCount)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .IdtAddressSpanOverflow;
        }

        if (!IsCanonicalSpan(address, byteCount))
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalIdtAddressSpan;
        }

        return DisabledDefaultUd2VectorStateTransitionStatus.Success;
    }

    private static DisabledDefaultUd2VectorStateTransitionStatus
        ValidateIstStackTops(
            ulong ist1StackTop,
            ulong ist2StackTop,
            ulong ist3StackTop)
    {
        if (ist1StackTop == 0 || ist2StackTop == 0 || ist3StackTop == 0)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .NullIstStackTop;
        }

        ulong combined = ist1StackTop | ist2StackTop | ist3StackTop;
        if ((combined & (IstStackTopAlignment - 1)) != 0)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .MisalignedIstStackTop;
        }

        if (!IsCanonicalX64Address(ist1StackTop) ||
            !IsCanonicalX64Address(ist2StackTop) ||
            !IsCanonicalX64Address(ist3StackTop))
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .NonCanonicalIstStackTop;
        }

        if (ist1StackTop == ist2StackTop ||
            ist1StackTop == ist3StackTop ||
            ist2StackTop == ist3StackTop)
        {
            return DisabledDefaultUd2VectorStateTransitionStatus
                .DuplicateIstStackTop;
        }

        return DisabledDefaultUd2VectorStateTransitionStatus.Success;
    }

    private static DisabledDefaultUd2VectorStateTransitionStatus
        ValidatePreparedContext(
            byte* context,
            ulong modeledContextAddress,
            ulong ownedIdtAddress,
            ulong ist1StackTop,
            ulong ist2StackTop,
            ulong ist3StackTop,
            out int bytesCompared,
            out int firstMismatchOffset,
            out ulong contextFingerprint)
    {
        bytesCompared = 0;
        firstMismatchOffset = -1;
        contextFingerprint = ComputeFingerprint(context, ContextByteCount);

        ulong storedFingerprint = ReadU64(
            context + PreparedContextFingerprintOffset);
        ulong computedFingerprint = ComputePreparedContextFingerprint(context);
        bytesCompared = ContextByteCount;
        if (storedFingerprint != computedFingerprint)
        {
            firstMismatchOffset = PreparedContextFingerprintOffset;
            return DisabledDefaultUd2VectorStateTransitionStatus
                .ContextByteMismatch;
        }

        if (ReadU16(context) !=
                OwnedGDTTSSGeneratedAuditedCSharpStubBytes.GdtrLimit ||
            ReadU64(context + 2) != modeledContextAddress +
                OwnedGDTTSSGeneratedAuditedCSharpStubBytes.GdtOffset ||
            ReadU64(context + TssIst1Offset) != ist1StackTop ||
            ReadU64(context + TssIst2Offset) != ist2StackTop ||
            ReadU64(context + TssIst3Offset) != ist3StackTop ||
            ReadU64(context + ExpectedIst1StackTopOffset) != ist1StackTop ||
            ReadU64(context + ExpectedIst2StackTopOffset) != ist2StackTop ||
            ReadU64(context + ExpectedIst3StackTopOffset) != ist3StackTop ||
            ReadU16(context + RequestedIdtrOffset) != OwnedIdtrLimit ||
            ReadU64(context + RequestedIdtrOffset + 2) != ownedIdtAddress)
        {
            firstMismatchOffset = 0;
            return DisabledDefaultUd2VectorStateTransitionStatus
                .ContextByteMismatch;
        }

        if (ReadU16(context +
                OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                    .TssOffset +
                OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                    .TssIoMapBaseOffset) !=
            OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssIoMapBase)
        {
            firstMismatchOffset =
                OwnedGDTTSSGeneratedAuditedCSharpStubBytes.TssOffset +
                OwnedGDTTSSGeneratedAuditedCSharpStubBytes
                    .TssIoMapBaseOffset;
            return DisabledDefaultUd2VectorStateTransitionStatus
                .ContextByteMismatch;
        }

        return DisabledDefaultUd2VectorStateTransitionStatus.Success;
    }

    private static void BuildStream(
        ref StreamBuilder builder,
        ulong modeledContextAddress,
        out bool layoutMatched)
    {
        layoutMatched = builder.Offset == CommonHandlerOffset;

        builder.Emit(0x50, 0x53, 0x51, 0x52, 0x56, 0x57, 0x55);
        builder.Emit(
            0x41, 0x50, 0x41, 0x51, 0x41, 0x52, 0x41, 0x53,
            0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57);
        builder.Emit(0x4c, 0x8b, 0x3d);
        builder.EmitRel32(ContextAddressLiteralOffset);

        EmitCapture(ref builder, NormalizedVectorStackOffset,
            CapturedVectorOffset);
        EmitCapture(ref builder, NormalizedHasErrorCodeStackOffset,
            CapturedHasErrorCodeOffset);
        EmitCapture(ref builder, NormalizedErrorCodeStackOffset,
            CapturedErrorCodeOffset);
        EmitCapture(ref builder, NormalizedRipStackOffset,
            CapturedRipOffset);
        EmitCapture(ref builder, NormalizedRspStackOffset,
            CapturedRspOffset);
        EmitCapture(ref builder, 64, CapturedRbpOffset);
        EmitCapture(ref builder, NormalizedRflagsStackOffset,
            CapturedRflagsOffset);
        builder.Emit(0x0f, 0x20, 0xd0);
        EmitStoreRaxToContext(ref builder, CapturedCr2Offset);
        builder.Emit(0x0f, 0x20, 0xd8);
        EmitStoreRaxToContext(ref builder, CapturedCr3Offset);
        EmitCapture(ref builder, 112, CapturedRaxOffset);
        EmitCapture(ref builder, 104, CapturedRbxOffset);
        EmitCapture(ref builder, 96, CapturedRcxOffset);
        EmitCapture(ref builder, 88, CapturedRdxOffset);
        EmitCapture(ref builder, 80, CapturedRsiOffset);
        EmitCapture(ref builder, 72, CapturedRdiOffset);
        EmitCapture(ref builder, 56, CapturedR8Offset);
        EmitCapture(ref builder, 48, CapturedR9Offset);
        EmitCapture(ref builder, 40, CapturedR10Offset);
        EmitCapture(ref builder, 32, CapturedR11Offset);
        EmitCapture(ref builder, 24, CapturedR12Offset);
        EmitCapture(ref builder, 16, CapturedR13Offset);
        EmitCapture(ref builder, 8, CapturedR14Offset);
        EmitCapture(ref builder, 0, CapturedR15Offset);
        EmitMovAbs(ref builder, 0x48, 0xb8, CaptureCompletionCookie);
        EmitStoreRaxToContext(ref builder, CaptureCompletionOffset);

        builder.Emit(0x31, 0xc0, 0x66, 0x8c, 0xc8);
        builder.Emit(0x66, 0x3d, 0x08, 0x00);
        EmitJneFailure(ref builder);
        EmitMovRaxFromContext(ref builder, ExpectedCsOffset);
        EmitCompareStackToRax(ref builder, NormalizedCsStackOffset);
        EmitJneFailure(ref builder);
        EmitMovRaxFromContext(ref builder, ExpectedSsOffset);
        EmitCompareStackToRax(ref builder, NormalizedSsStackOffset);
        EmitJneFailure(ref builder);
        EmitCompareContextImmediate(ref builder, CapturedVectorOffset,
            InvalidOpcodeVector);
        EmitJneFailure(ref builder);
        EmitCompareContextImmediate(ref builder, CapturedHasErrorCodeOffset, 0);
        EmitJneFailure(ref builder);
        EmitCompareContextImmediate(ref builder, CapturedErrorCodeOffset, 0);
        EmitJneFailure(ref builder);
        EmitCompareContextPair(ref builder, ExpectedRipOffset,
            CapturedRipOffset);
        EmitJneFailure(ref builder);
        EmitCompareContextPair(ref builder, ExpectedRspOffset,
            CapturedRspOffset);
        EmitJneFailure(ref builder);
        EmitCompareContextImmediate(ref builder, CapturedRflagsOffset,
            (byte)ExpectedRflags);
        EmitJneFailure(ref builder);
        EmitCompareContextPair(ref builder, ExpectedCr2Offset,
            CapturedCr2Offset);
        EmitJneFailure(ref builder);
        EmitCompareContextPair(ref builder, ExpectedCr3Offset,
            CapturedCr3Offset);
        EmitJneFailure(ref builder);

        EmitCompareCookie(ref builder, CapturedRaxOffset,
            RegisterCookieBase + 1);
        EmitCompareCookie(ref builder, CapturedRbxOffset,
            RegisterCookieBase + 2);
        EmitCompareCookie(ref builder, CapturedRcxOffset,
            RegisterCookieBase + 3);
        EmitCompareCookie(ref builder, CapturedRdxOffset,
            RegisterCookieBase + 4);
        EmitCompareCookie(ref builder, CapturedRsiOffset,
            RegisterCookieBase + 5);
        EmitCompareCookie(ref builder, CapturedRdiOffset,
            RegisterCookieBase + 6);
        EmitCompareCookie(ref builder, CapturedRbpOffset,
            RegisterCookieBase + 7);
        EmitCompareCookie(ref builder, CapturedR8Offset,
            RegisterCookieBase + 8);
        EmitCompareCookie(ref builder, CapturedR9Offset,
            RegisterCookieBase + 9);
        EmitCompareCookie(ref builder, CapturedR10Offset,
            RegisterCookieBase + 10);
        EmitCompareCookie(ref builder, CapturedR11Offset,
            RegisterCookieBase + 11);
        EmitCompareCookie(ref builder, CapturedR12Offset,
            RegisterCookieBase + 12);
        EmitCompareCookie(ref builder, CapturedR13Offset,
            RegisterCookieBase + 13);
        EmitCompareCookie(ref builder, CapturedR14Offset,
            RegisterCookieBase + 14);
        EmitCompareCookie(ref builder, CapturedR15Offset,
            RegisterCookieBase + 15);

        layoutMatched &= builder.Offset == SuccessSetupOffset;
        builder.Emit(0x48, 0x8d, 0x35);
        builder.EmitRel32(SuccessTranscriptOffset);
        builder.Emit(0xb9);
        builder.EmitU32(SuccessTranscriptByteCount);
        builder.Emit(0xe9);
        builder.EmitRel32(SerialWriterOffset);

        layoutMatched &= builder.Offset == FailureSetupOffset;
        builder.Emit(0x48, 0x8d, 0x35);
        builder.EmitRel32(FailureTranscriptOffset);
        builder.Emit(0xb9);
        builder.EmitU32(FailureTranscriptByteCount);

        layoutMatched &= builder.Offset == SerialWriterOffset;
        builder.Emit(0x66, 0xba, 0xfd, 0x03);
        layoutMatched &= builder.Offset == SerialPollOffset;
        builder.Emit(0xec, 0xa8, 0x20, 0x74, 0xfb);
        builder.Emit(
            0x66, 0xba, 0xf8, 0x03, 0xac, 0xee,
            0x66, 0xba, 0xfd, 0x03, 0xff, 0xc9, 0x75, 0xed);

        layoutMatched &= builder.Offset == TerminalOffset;
        builder.Emit(0xfa);
        layoutMatched &= builder.Offset == HaltOffset;
        builder.Emit(0xf4, 0xeb, 0xfd);

        layoutMatched &= builder.Offset == SuccessTranscriptOffset;
        EmitSuccessTranscript(ref builder);
        layoutMatched &= builder.Offset == FailureTranscriptOffset;
        EmitFailureTranscript(ref builder);
        layoutMatched &= builder.Offset == ContextAddressLiteralOffset;
        builder.EmitU64(modeledContextAddress);
        layoutMatched &= builder.Offset == HandlerBlockEndOffset;

        while (builder.Offset < TransitionEntryOffset)
        {
            builder.Emit(0x90);
        }

        layoutMatched &= builder.Offset == TransitionEntryOffset;
        builder.Emit(0xfa);
        builder.Emit(0x4c, 0x8b, 0x3d);
        builder.EmitRel32(ContextAddressLiteralOffset);
        builder.Emit(0x4c, 0x39, 0xf9);
        EmitJneFailure(ref builder);

        builder.Emit(0x41, 0x0f, 0x01, 0x17);
        builder.Emit(0x41, 0x0f, 0x01, 0x47, 0x0a);
        builder.Emit(0x49, 0x8b, 0x07);
        builder.Emit(0x49, 0x3b, 0x47, 0x0a);
        EmitJneFailure(ref builder);
        builder.Emit(0x41, 0x0f, 0xb7, 0x47, 0x08);
        builder.Emit(0x41, 0x0f, 0xb7, 0x57, 0x12);
        builder.Emit(0x39, 0xd0);
        EmitJneFailure(ref builder);

        builder.Emit(0x66, 0xb8, 0x10, 0x00);
        builder.Emit(0x8e, 0xd8);
        builder.Emit(0x8e, 0xc0);
        builder.Emit(0x8e, 0xd0);
        builder.Emit(0x66, 0x8c, 0xd8);
        builder.Emit(0x66, 0x3d, 0x10, 0x00);
        EmitJneFailure(ref builder);
        builder.Emit(0x66, 0x8c, 0xc0);
        builder.Emit(0x66, 0x3d, 0x10, 0x00);
        EmitJneFailure(ref builder);
        builder.Emit(0x66, 0x8c, 0xd0);
        builder.Emit(0x66, 0x3d, 0x10, 0x00);
        EmitJneFailure(ref builder);

        builder.Emit(0x66, 0xb8, 0x18, 0x00);
        builder.Emit(0x0f, 0x00, 0xd8);
        builder.Emit(0x41, 0x0f, 0x00, 0x4f, 0x14);
        builder.Emit(0x66, 0x41, 0x83, 0x7f, 0x14, 0x18);
        EmitJneFailure(ref builder);
        builder.Emit(0x41, 0x80, 0x7f, 0x35, 0x8b);
        EmitJneFailure(ref builder);

        builder.Emit(0x41, 0x0f, 0x01, 0x9f);
        builder.EmitU32(RequestedIdtrOffset);
        builder.Emit(0x41, 0x0f, 0x01, 0x8f);
        builder.EmitU32(ObservedIdtrOffset);
        EmitMovRaxFromContext(ref builder, RequestedIdtrOffset);
        builder.Emit(0x49, 0x3b, 0x87);
        builder.EmitU32(ObservedIdtrOffset);
        EmitJneFailure(ref builder);
        builder.Emit(0x41, 0x0f, 0xb7, 0x87);
        builder.EmitU32(RequestedIdtrOffset + 8);
        builder.Emit(0x41, 0x0f, 0xb7, 0x97);
        builder.EmitU32(ObservedIdtrOffset + 8);
        builder.Emit(0x39, 0xd0);
        EmitJneFailure(ref builder);

        builder.Emit(0x48, 0x8d, 0x05);
        builder.EmitRel32(Ud2Offset);
        EmitStoreRaxToContext(ref builder, ExpectedRipOffset);
        builder.Emit(0x0f, 0x20, 0xd0);
        EmitStoreRaxToContext(ref builder, ExpectedCr2Offset);
        builder.Emit(0x0f, 0x20, 0xd8);
        EmitStoreRaxToContext(ref builder, ExpectedCr3Offset);
        builder.Emit(0x31, 0xc0, 0x66, 0x8c, 0xc8);
        EmitStoreRaxToContext(ref builder, ExpectedCsOffset);
        builder.Emit(0x31, 0xc0, 0x66, 0x8c, 0xd0);
        EmitStoreRaxToContext(ref builder, ExpectedSsOffset);
        EmitMovAbs(ref builder, 0x48, 0xb8, RegisterCookieBase + 15);
        EmitStoreRaxToContext(ref builder, ExpectedR15Offset);

        builder.Emit(0x6a, 0x46, 0x9d, 0x9c, 0x58);
        builder.Emit(0x48, 0x83, 0xf8, 0x46);
        EmitJneFailure(ref builder);
        builder.Emit(0x6a, 0x46, 0x9d);
        builder.Emit(0x49, 0xc7, 0x87);
        builder.EmitU32(ExpectedRflagsOffset);
        builder.EmitU32((uint)ExpectedRflags);
        builder.Emit(0x49, 0x89, 0xa7);
        builder.EmitU32(ExpectedRspOffset);

        EmitMovAbs(ref builder, 0x48, 0xb8, RegisterCookieBase + 1);
        EmitMovAbs(ref builder, 0x48, 0xbb, RegisterCookieBase + 2);
        EmitMovAbs(ref builder, 0x48, 0xb9, RegisterCookieBase + 3);
        EmitMovAbs(ref builder, 0x48, 0xba, RegisterCookieBase + 4);
        EmitMovAbs(ref builder, 0x48, 0xbe, RegisterCookieBase + 5);
        EmitMovAbs(ref builder, 0x48, 0xbf, RegisterCookieBase + 6);
        EmitMovAbs(ref builder, 0x48, 0xbd, RegisterCookieBase + 7);
        EmitMovAbs(ref builder, 0x49, 0xb8, RegisterCookieBase + 8);
        EmitMovAbs(ref builder, 0x49, 0xb9, RegisterCookieBase + 9);
        EmitMovAbs(ref builder, 0x49, 0xba, RegisterCookieBase + 10);
        EmitMovAbs(ref builder, 0x49, 0xbb, RegisterCookieBase + 11);
        EmitMovAbs(ref builder, 0x49, 0xbc, RegisterCookieBase + 12);
        EmitMovAbs(ref builder, 0x49, 0xbd, RegisterCookieBase + 13);
        EmitMovAbs(ref builder, 0x49, 0xbe, RegisterCookieBase + 14);
        EmitMovAbs(ref builder, 0x49, 0xbf, RegisterCookieBase + 15);

        layoutMatched &= builder.Offset == Ud2Offset;
        builder.Emit(0x0f, 0x0b);
        layoutMatched &= builder.Offset == TransitionEndOffset;

        while (builder.Offset < StreamByteCount)
        {
            builder.Emit(0xf4);
        }

        layoutMatched &= builder.Offset == StreamByteCount;
    }

    private static void EmitJneFailure(ref StreamBuilder builder)
    {
        builder.Emit(0x0f, 0x85);
        builder.EmitRel32(FailureSetupOffset);
    }

    private static void EmitMovAbs(
        ref StreamBuilder builder,
        byte rex,
        byte opcode,
        ulong value)
    {
        builder.Emit(rex, opcode);
        builder.EmitU64(value);
    }

    private static void EmitMovRaxFromContext(
        ref StreamBuilder builder,
        int contextOffset)
    {
        builder.Emit(0x49, 0x8b, 0x87);
        builder.EmitU32(contextOffset);
    }

    private static void EmitStoreRaxToContext(
        ref StreamBuilder builder,
        int contextOffset)
    {
        builder.Emit(0x49, 0x89, 0x87);
        builder.EmitU32(contextOffset);
    }

    private static void EmitCapture(
        ref StreamBuilder builder,
        int stackOffset,
        int contextOffset)
    {
        builder.Emit(0x48, 0x8b, 0x84, 0x24);
        builder.EmitU32(stackOffset);
        EmitStoreRaxToContext(ref builder, contextOffset);
    }

    private static void EmitCompareStackToRax(
        ref StreamBuilder builder,
        int stackOffset)
    {
        builder.Emit(0x48, 0x39, 0x84, 0x24);
        builder.EmitU32(stackOffset);
    }

    private static void EmitCompareContextImmediate(
        ref StreamBuilder builder,
        int contextOffset,
        byte value)
    {
        builder.Emit(0x49, 0x83, 0xbf);
        builder.EmitU32(contextOffset);
        builder.Emit(value);
    }

    private static void EmitCompareContextPair(
        ref StreamBuilder builder,
        int expectedOffset,
        int capturedOffset)
    {
        EmitMovRaxFromContext(ref builder, expectedOffset);
        builder.Emit(0x49, 0x39, 0x87);
        builder.EmitU32(capturedOffset);
    }

    private static void EmitCompareCookie(
        ref StreamBuilder builder,
        int contextOffset,
        ulong cookie)
    {
        EmitMovAbs(ref builder, 0x48, 0xb8, cookie);
        builder.Emit(0x49, 0x39, 0x87);
        builder.EmitU32(contextOffset);
        EmitJneFailure(ref builder);
    }

    private static void EmitSuccessTranscript(ref StreamBuilder builder)
    {
        builder.EmitU64(0x304d5f534f524c43UL);
        builder.EmitU64(0x45525f3331305f32UL);
        builder.EmitU64(0x7463657620594441UL);
        builder.EmitU64(0x72726520363d726fUL);
        builder.EmitU64(0x725065646f43726fUL);
        builder.EmitU64(0x61663d746e657365UL);
        builder.EmitU64(0x6f7272652065736cUL);
        builder.EmitU64(0x20303d65646f4372UL);
        builder.EmitU64(0x614d706952326475UL);
        builder.EmitU64(0x72743d6465686374UL);
        builder.EmitU64(0x7369676572206575UL);
        builder.EmitU64(0x646c656946726574UL);
        builder.EmitU64(0x6765722033323d73UL);
        builder.EmitU64(0x6174537265747369UL);
        builder.EmitU64(0x7275747061436574UL);
        builder.EmitU64(0x20657572743d6465UL);
        builder.EmitU64(0x7265747369676572UL);
        builder.EmitU64(0x74614d6574617453UL);
        builder.EmitU64(0x7572743d64656863UL);
        builder.EmitU64(0x4764656e776f2065UL);
        builder.EmitU64(0x646564616f4c7464UL);
        builder.EmitU64(0x776f20657572743dUL);
        builder.EmitU64(0x6f4c73735464656eUL);
        builder.EmitU64(0x7572743d64656461UL);
        builder.EmitU64(0x4964656e776f2065UL);
        builder.EmitU64(0x6564616f4c727464UL);
        builder.EmitU64(0x6f20657572743d64UL);
        builder.EmitU64(0x316d6f4364656e77UL);
        builder.EmitU64(0x616620657572743dUL);
        builder.EmitU64(0x6767697254746c75UL);
        builder.EmitU64(0x7572743d64657265UL);
        builder.EmitU64(0x6e696d7265742065UL);
        builder.EmitU64(0x743d746c61486c61UL);
        builder.EmitU64(0x6b72616d20657572UL);
        builder.EmitU64(0x70736e6172547265UL);
        builder.EmitU64(0x656e776f3d74726fUL);
        builder.EmitU64(0x6f7020316d6f4364UL);
        builder.EmitU64(0x6f42746978457473UL);
        builder.EmitU64(0x636976726553746fUL);
        builder.EmitU64(0x65736c61663d7365UL);
        builder.EmitU64(0x505f545345540a0dUL);
        builder.EmitU64(0x2e32306d20535341UL);
        builder.EmitU32(0x0d33_3130U);
        builder.Emit(0x0a);
    }

    private static void EmitFailureTranscript(ref StreamBuilder builder)
    {
        builder.EmitU64(0x5f3244555f32304dUL);
        builder.EmitU64(0x535f524f54434556UL);
        builder.EmitU64(0x5245565f45544154UL);
        builder.EmitU64(0x4c4941465f594649UL);
        builder.EmitU64(0x545345540a0d4445UL);
        builder.EmitU64(0x306d204c4941465fUL);
        builder.EmitU64(0x6576203331302e32UL);
        builder.EmitU64(0x5f726f5f726f7463UL);
        builder.EmitU64(0x7265747369676572UL);
        builder.EmitU64(0x6374616d73696d5fUL);
        builder.EmitU16(0x0d68);
        builder.Emit(0x0a);
    }

    private static ulong ComputePreparedContextFingerprint(byte* context)
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < ContextByteCount; offset++)
        {
            byte value = offset >= PreparedContextFingerprintOffset &&
                offset < PreparedContextFingerprintOffset + 8
                    ? (byte)0
                    : context[offset];
            fingerprint ^= value;
            fingerprint = unchecked(fingerprint * Fnv1A64Prime);
        }

        return fingerprint;
    }

    private static ulong ComputeFingerprint(byte* source, int length)
    {
        ulong fingerprint = Fnv1A64OffsetBasis;
        for (int offset = 0; offset < length; offset++)
        {
            fingerprint ^= source[offset];
            fingerprint = unchecked(fingerprint * Fnv1A64Prime);
        }

        return fingerprint;
    }

    private static bool IsCanonicalSpan(ulong start, ulong length)
    {
        ulong endInclusive = start + length - 1;
        if (!IsCanonicalX64Address(endInclusive))
        {
            return false;
        }

        return ((start >> 47) & 1UL) == ((endInclusive >> 47) & 1UL);
    }

    private static bool RangesOverlap(
        ulong firstStart,
        ulong firstLength,
        ulong secondStart,
        ulong secondLength)
    {
        ulong firstEnd = firstStart + firstLength;
        ulong secondEnd = secondStart + secondLength;
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private static ushort ReadU16(byte* source)
    {
        return (ushort)(source[0] | (source[1] << 8));
    }

    private static ulong ReadU64(byte* source)
    {
        return (ulong)source[0] |
            ((ulong)source[1] << 8) |
            ((ulong)source[2] << 16) |
            ((ulong)source[3] << 24) |
            ((ulong)source[4] << 32) |
            ((ulong)source[5] << 40) |
            ((ulong)source[6] << 48) |
            ((ulong)source[7] << 56);
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

    private unsafe struct StreamBuilder
    {
        private byte* _destination;
        private byte* _source;
        private byte _mode;
        private int _firstMismatchOffset;

        public int Offset;
        public ulong Fingerprint;

        public int FirstMismatchOffset => _firstMismatchOffset;

        public int BytesCompared =>
            _mode == 2
                ? (_firstMismatchOffset >= 0
                    ? _firstMismatchOffset + 1
                    : Offset)
                : 0;

        public static StreamBuilder CreateCount()
        {
            StreamBuilder builder = default;
            builder.Fingerprint = Fnv1A64OffsetBasis;
            builder._firstMismatchOffset = -1;
            return builder;
        }

        public static StreamBuilder CreateWriter(byte* destination)
        {
            StreamBuilder builder = CreateCount();
            builder._destination = destination;
            builder._mode = 1;
            return builder;
        }

        public static StreamBuilder CreateReader(byte* source)
        {
            StreamBuilder builder = CreateCount();
            builder._source = source;
            builder._mode = 2;
            return builder;
        }

        public void Emit(byte value)
        {
            if (_mode == 1)
            {
                _destination[Offset] = value;
            }
            else if (_mode == 2 &&
                _firstMismatchOffset < 0 &&
                _source[Offset] != value)
            {
                _firstMismatchOffset = Offset;
            }

            Fingerprint ^= value;
            Fingerprint = unchecked(Fingerprint * Fnv1A64Prime);
            Offset++;
        }

        public void Emit(byte first, byte second)
        {
            Emit(first);
            Emit(second);
        }

        public void Emit(byte first, byte second, byte third)
        {
            Emit(first);
            Emit(second);
            Emit(third);
        }

        public void Emit(byte first, byte second, byte third, byte fourth)
        {
            Emit(first);
            Emit(second);
            Emit(third);
            Emit(fourth);
        }

        public void Emit(
            byte first,
            byte second,
            byte third,
            byte fourth,
            byte fifth)
        {
            Emit(first);
            Emit(second);
            Emit(third);
            Emit(fourth);
            Emit(fifth);
        }

        public void Emit(
            byte first,
            byte second,
            byte third,
            byte fourth,
            byte fifth,
            byte sixth)
        {
            Emit(first);
            Emit(second);
            Emit(third);
            Emit(fourth);
            Emit(fifth);
            Emit(sixth);
        }

        public void Emit(
            byte first,
            byte second,
            byte third,
            byte fourth,
            byte fifth,
            byte sixth,
            byte seventh)
        {
            Emit(first);
            Emit(second);
            Emit(third);
            Emit(fourth);
            Emit(fifth);
            Emit(sixth);
            Emit(seventh);
        }

        public void Emit(
            byte b0, byte b1, byte b2, byte b3,
            byte b4, byte b5, byte b6, byte b7,
            byte b8, byte b9, byte b10, byte b11,
            byte b12, byte b13)
        {
            Emit(b0); Emit(b1); Emit(b2); Emit(b3);
            Emit(b4); Emit(b5); Emit(b6); Emit(b7);
            Emit(b8); Emit(b9); Emit(b10); Emit(b11);
            Emit(b12); Emit(b13);
        }

        public void Emit(
            byte b0, byte b1, byte b2, byte b3,
            byte b4, byte b5, byte b6, byte b7,
            byte b8, byte b9, byte b10, byte b11,
            byte b12, byte b13, byte b14, byte b15)
        {
            Emit(b0); Emit(b1); Emit(b2); Emit(b3);
            Emit(b4); Emit(b5); Emit(b6); Emit(b7);
            Emit(b8); Emit(b9); Emit(b10); Emit(b11);
            Emit(b12); Emit(b13); Emit(b14); Emit(b15);
        }

        public void EmitU16(ushort value)
        {
            Emit((byte)value);
            Emit((byte)(value >> 8));
        }

        public void EmitU32(int value)
        {
            EmitU32(unchecked((uint)value));
        }

        public void EmitU32(uint value)
        {
            Emit((byte)value);
            Emit((byte)(value >> 8));
            Emit((byte)(value >> 16));
            Emit((byte)(value >> 24));
        }

        public void EmitU64(ulong value)
        {
            for (int index = 0; index < 8; index++)
            {
                Emit((byte)(value >> (index * 8)));
            }
        }

        public void EmitRel32(int targetOffset)
        {
            int displacement = targetOffset - (Offset + 4);
            EmitU32(displacement);
        }
    }
}
