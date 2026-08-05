namespace OS.Kernel.Diagnostics;

public enum PortIoStubPlanStatus
{
    Success = 0,
    InvalidOperationKind = 1,
    OutputBufferTooSmall = 2,
    MissingDestination = 3
}

public readonly struct PortIoStubPlanResult
{
    private PortIoStubPlanResult(
        PortIoStubPlanStatus status,
        int bytesWritten,
        int requiredBytes)
    {
        IsInitialized = true;
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized { get; }

    public PortIoStubPlanStatus Status { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public bool Succeeded => IsInitialized &&
        Status == PortIoStubPlanStatus.Success &&
        RequiredBytes > 0 &&
        BytesWritten == RequiredBytes;

    public static PortIoStubPlanResult Success(int bytesWritten, int requiredBytes)
    {
        return new PortIoStubPlanResult(PortIoStubPlanStatus.Success, bytesWritten, requiredBytes);
    }

    public static PortIoStubPlanResult Fail(PortIoStubPlanStatus status, int requiredBytes)
    {
        return new PortIoStubPlanResult(status, 0, requiredBytes);
    }
}

public readonly struct PortIoStubPlan
{
    public const byte OperandSizeOverridePrefix = 0x66;
    public const byte MovR16ToRm16Opcode = 0x89;
    public const byte MovCxToDxModRm = 0xca;
    public const byte MovR8ToRm8Opcode = 0x88;
    public const byte MovDlToAlModRm = 0xd0;
    public const byte RetOpcode = 0xc3;

    public const int In8RequiredByteLength = 5;
    public const int Out8RequiredByteLength = 7;
    public const int MaximumRequiredByteLength = Out8RequiredByteLength;

    public PortIoStubPlan(PortIoOperationKind operationKind)
    {
        OperationKind = operationKind;
    }

    public PortIoOperationKind OperationKind { get; }

    public static PortIoStubPlan Out8 => new PortIoStubPlan(PortIoOperationKind.Out8);

    public static PortIoStubPlan In8 => new PortIoStubPlan(PortIoOperationKind.In8);

    public unsafe PortIoStubPlanResult Emit(
        byte* destination,
        int destinationLength)
    {
        PortIoOperationPlanStatus operationStatus = PortIoOperationPlan.Validate(OperationKind);
        if (operationStatus != PortIoOperationPlanStatus.Success)
        {
            return PortIoStubPlanResult.Fail(PortIoStubPlanStatus.InvalidOperationKind, 0);
        }

        int requiredBytes = GetRequiredByteLength(OperationKind);
        if (destinationLength < requiredBytes)
        {
            return PortIoStubPlanResult.Fail(PortIoStubPlanStatus.OutputBufferTooSmall, requiredBytes);
        }

        if (destination == null)
        {
            return PortIoStubPlanResult.Fail(PortIoStubPlanStatus.MissingDestination, requiredBytes);
        }

        byte operationByte = 0;

        PortIoOperationPlanResult operationResult =
            new PortIoOperationPlan(OperationKind).Emit(
                &operationByte,
                PortIoOperationPlan.RequiredByteLength);

        if (!operationResult.Succeeded)
        {
            return PortIoStubPlanResult.Fail(PortIoStubPlanStatus.InvalidOperationKind, requiredBytes);
        }

        int portMoveOffset = 0;
        int operationOffset = 3;
        if (OperationKind == PortIoOperationKind.Out8)
        {
            // Windows x64 passes the port in CX and the byte value in DL.
            // Preserve DL in AL before repurposing DX for the port.
            destination[0] = MovR8ToRm8Opcode;
            destination[1] = MovDlToAlModRm;
            portMoveOffset = 2;
            operationOffset = 5;
        }

        destination[portMoveOffset] = OperandSizeOverridePrefix;
        destination[portMoveOffset + 1] = MovR16ToRm16Opcode;
        destination[portMoveOffset + 2] = MovCxToDxModRm;

        destination[operationOffset] = operationByte;
        destination[operationOffset + PortIoOperationPlan.RequiredByteLength] = RetOpcode;

        return PortIoStubPlanResult.Success(requiredBytes, requiredBytes);
    }

#if !NOSTDLIB_EFI
    public unsafe PortIoStubPlanResult Emit(Span<byte> destination)
    {
        fixed (byte* pointer = destination)
        {
            return Emit(pointer, destination.Length);
        }
    }
#endif

    public static int GetRequiredByteLength(PortIoOperationKind operationKind)
    {
        if (operationKind == PortIoOperationKind.Out8)
        {
            return Out8RequiredByteLength;
        }

        if (operationKind == PortIoOperationKind.In8)
        {
            return In8RequiredByteLength;
        }

        return 0;
    }
}
