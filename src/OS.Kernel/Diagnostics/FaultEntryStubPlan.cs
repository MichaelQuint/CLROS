namespace OS.Kernel.Diagnostics;

public enum FaultEntryStubPlanStatus
{
    Ok = 0,
    UnsupportedVectorMetadata = 1,
    UnsupportedErrorCodeMetadata = 2,
    OutputBufferTooSmall = 3
}

public readonly struct FaultEntryStubPlanResult
{
    private FaultEntryStubPlanResult(
        bool succeeded,
        FaultEntryStubPlanStatus status,
        byte vector,
        bool pushesErrorCode,
        bool pushesSyntheticErrorCode,
        int bytesWritten,
        int requiredBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Vector = vector;
        PushesErrorCode = pushesErrorCode;
        PushesSyntheticErrorCode = pushesSyntheticErrorCode;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool Succeeded { get; }

    public FaultEntryStubPlanStatus Status { get; }

    public byte Vector { get; }

    public bool PushesErrorCode { get; }

    public bool PushesSyntheticErrorCode { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public static FaultEntryStubPlanResult Success(
        byte vector,
        bool pushesErrorCode,
        bool pushesSyntheticErrorCode,
        int bytesWritten)
    {
        return new FaultEntryStubPlanResult(
            true,
            FaultEntryStubPlanStatus.Ok,
            vector,
            pushesErrorCode,
            pushesSyntheticErrorCode,
            bytesWritten,
            bytesWritten);
    }

    public static FaultEntryStubPlanResult Fail(
        FaultEntryStubPlanStatus status,
        byte vector,
        bool pushesErrorCode,
        bool pushesSyntheticErrorCode,
        int requiredBytes)
    {
        return new FaultEntryStubPlanResult(
            false,
            status,
            vector,
            pushesErrorCode,
            pushesSyntheticErrorCode,
            0,
            requiredBytes);
    }
}

public static class FaultEntryStubPlan
{
    public const byte PushImmediate8Opcode = 0x6a;
    public const byte TwoByteOpcodePrefix = 0x0f;
    public const byte Ud2Opcode = 0x0b;
    public const byte RetOpcode = 0xc3;

    public const byte NoErrorCodeImmediate = 0x00;
    public const byte HasErrorCodeImmediate = 0x01;
    public const byte SyntheticErrorCodeImmediate = 0x00;

    public const int PushImmediate8InstructionLength = 2;
    public const int TerminalInstructionLength = 3;

    public const int InvalidOpcodeSyntheticErrorCodePushOffset = 0;
    public const int InvalidOpcodeHasErrorCodePushOffset =
        InvalidOpcodeSyntheticErrorCodePushOffset + PushImmediate8InstructionLength;
    public const int InvalidOpcodeVectorPushOffset =
        InvalidOpcodeHasErrorCodePushOffset + PushImmediate8InstructionLength;
    public const int InvalidOpcodeTerminalOffset =
        InvalidOpcodeVectorPushOffset + PushImmediate8InstructionLength;

    public const int PageFaultHasErrorCodePushOffset = 0;
    public const int PageFaultVectorPushOffset =
        PageFaultHasErrorCodePushOffset + PushImmediate8InstructionLength;
    public const int PageFaultTerminalOffset =
        PageFaultVectorPushOffset + PushImmediate8InstructionLength;

    public const int InvalidOpcodeRequiredByteLength =
        InvalidOpcodeTerminalOffset + TerminalInstructionLength;
    public const int PageFaultRequiredByteLength =
        PageFaultTerminalOffset + TerminalInstructionLength;
    public const int MaximumRequiredByteLength = InvalidOpcodeRequiredByteLength;

    public static FaultEntryStubPlanResult EmitInvalidOpcode(Span<byte> destination)
    {
        return Emit(FaultVectorCatalog.InvalidOpcode, pushesErrorCode: false, destination);
    }

    public static FaultEntryStubPlanResult EmitPageFault(Span<byte> destination)
    {
        return Emit(FaultVectorCatalog.PageFault, pushesErrorCode: true, destination);
    }

    public static FaultEntryStubPlanResult Emit(FaultVectorInfo metadata, Span<byte> destination)
    {
        return Emit(metadata.Vector, metadata.PushesErrorCode, destination);
    }

    public static FaultEntryStubPlanResult Emit(
        byte vector,
        bool pushesErrorCode,
        Span<byte> destination)
    {
        FaultEntryStubPlanStatus status = ValidateMetadata(
            vector,
            pushesErrorCode,
            out int requiredBytes);
        bool pushesSyntheticErrorCode = RequiresSyntheticErrorCode(vector, pushesErrorCode);

        if (status != FaultEntryStubPlanStatus.Ok)
        {
            return FaultEntryStubPlanResult.Fail(
                status,
                vector,
                pushesErrorCode,
                pushesSyntheticErrorCode,
                requiredBytes);
        }

        if (destination.Length < requiredBytes)
        {
            return FaultEntryStubPlanResult.Fail(
                FaultEntryStubPlanStatus.OutputBufferTooSmall,
                vector,
                pushesErrorCode,
                pushesSyntheticErrorCode,
                requiredBytes);
        }

        if (vector == FaultVectorCatalog.InvalidOpcode)
        {
            WriteInvalidOpcode(destination);
        }
        else
        {
            WritePageFault(destination);
        }

        return FaultEntryStubPlanResult.Success(
            vector,
            pushesErrorCode,
            pushesSyntheticErrorCode,
            requiredBytes);
    }

    public static int GetRequiredByteLength(byte vector)
    {
        return vector switch
        {
            FaultVectorCatalog.InvalidOpcode => InvalidOpcodeRequiredByteLength,
            FaultVectorCatalog.PageFault => PageFaultRequiredByteLength,
            _ => 0
        };
    }

    public static FaultEntryStubPlanStatus Validate(
        FaultVectorInfo metadata,
        int outputBufferLength)
    {
        return Validate(metadata.Vector, metadata.PushesErrorCode, outputBufferLength);
    }

    public static FaultEntryStubPlanStatus Validate(
        byte vector,
        bool pushesErrorCode,
        int outputBufferLength)
    {
        FaultEntryStubPlanStatus status = ValidateMetadata(
            vector,
            pushesErrorCode,
            out int requiredBytes);
        if (status != FaultEntryStubPlanStatus.Ok)
        {
            return status;
        }

        return outputBufferLength < requiredBytes
            ? FaultEntryStubPlanStatus.OutputBufferTooSmall
            : FaultEntryStubPlanStatus.Ok;
    }

    private static FaultEntryStubPlanStatus ValidateMetadata(
        byte vector,
        bool pushesErrorCode,
        out int requiredBytes)
    {
        requiredBytes = GetRequiredByteLength(vector);
        if (requiredBytes == 0)
        {
            return FaultEntryStubPlanStatus.UnsupportedVectorMetadata;
        }

        if (!FaultVectorCatalog.TryGet(vector, out FaultVectorInfo catalogMetadata)
            || catalogMetadata.PushesErrorCode != pushesErrorCode)
        {
            return FaultEntryStubPlanStatus.UnsupportedErrorCodeMetadata;
        }

        return FaultEntryStubPlanStatus.Ok;
    }

    private static bool RequiresSyntheticErrorCode(byte vector, bool pushesErrorCode)
    {
        return vector == FaultVectorCatalog.InvalidOpcode && !pushesErrorCode;
    }

    private static void WriteInvalidOpcode(Span<byte> destination)
    {
        WritePushImmediate8(
            destination,
            InvalidOpcodeSyntheticErrorCodePushOffset,
            SyntheticErrorCodeImmediate);
        WritePushImmediate8(
            destination,
            InvalidOpcodeHasErrorCodePushOffset,
            NoErrorCodeImmediate);
        WritePushImmediate8(
            destination,
            InvalidOpcodeVectorPushOffset,
            FaultVectorCatalog.InvalidOpcode);
        WriteTerminal(destination, InvalidOpcodeTerminalOffset);
    }

    private static void WritePageFault(Span<byte> destination)
    {
        WritePushImmediate8(
            destination,
            PageFaultHasErrorCodePushOffset,
            HasErrorCodeImmediate);
        WritePushImmediate8(
            destination,
            PageFaultVectorPushOffset,
            FaultVectorCatalog.PageFault);
        WriteTerminal(destination, PageFaultTerminalOffset);
    }

    private static void WritePushImmediate8(Span<byte> destination, int offset, byte value)
    {
        destination[offset] = PushImmediate8Opcode;
        destination[offset + 1] = value;
    }

    private static void WriteTerminal(Span<byte> destination, int offset)
    {
        destination[offset] = TwoByteOpcodePrefix;
        destination[offset + 1] = Ud2Opcode;
        destination[offset + 2] = RetOpcode;
    }
}
