namespace OS.Kernel.Diagnostics;

public readonly struct FaultProbeStubPlan
{
    public const byte TwoByteOpcodePrefix = 0x0f;
    public const byte Ud2Opcode = 0x0b;
    public const byte MovRm8Imm8Opcode = 0xc6;
    public const byte AbsoluteAddressModRm = 0x04;
    public const byte AbsoluteAddressSib = 0x25;
    public const byte RetOpcode = 0xc3;
    public const byte NullWriteValue = 0x00;
    public const int InvalidOpcodeRequiredByteLength = 3;
    public const int NullWriteRequiredByteLength = 9;
    public const int MaximumRequiredByteLength = NullWriteRequiredByteLength;
    public FaultProbeStubPlan(FaultProbeStubKind kind)
    {
        Kind = kind;
    }

    public FaultProbeStubKind Kind
    {
        get;
    }
    public static FaultProbeStubPlan InvalidOpcode => new FaultProbeStubPlan(FaultProbeStubKind.InvalidOpcode);
    public static FaultProbeStubPlan NullWrite => new FaultProbeStubPlan(FaultProbeStubKind.NullWrite);

    public ProbeDefinition ProbeDefinition
    {
        get
        {
            if (Kind == FaultProbeStubKind.InvalidOpcode)
            {
                return Probes.GetInvalidOpcodePanicProbe();
            }

            if (Kind == FaultProbeStubKind.NullWrite)
            {
                return Probes.GetNullPointerPanicProbe();
            }

            return default;
        }
    }

    public FaultProbeStubPlanResult Emit(Span<byte> destination)
    {
        int requiredBytes = GetRequiredByteLength(Kind);
        if (requiredBytes == 0)
        {
            return FaultProbeStubPlanResult.Fail(FaultProbeStubPlanStatus.UnknownKind, Kind, 0);
        }

        if (destination.Length < requiredBytes)
        {
            return FaultProbeStubPlanResult.Fail(FaultProbeStubPlanStatus.OutputBufferTooSmall, Kind, requiredBytes);
        }

        if (Kind == FaultProbeStubKind.InvalidOpcode)
        {
            WriteInvalidOpcode(destination);
            return FaultProbeStubPlanResult.Success(Kind, InvalidOpcodeRequiredByteLength);
        }

        WriteNullWrite(destination);
        return FaultProbeStubPlanResult.Success(Kind, NullWriteRequiredByteLength);
    }

    public static int GetRequiredByteLength(FaultProbeStubKind kind)
    {
        return kind switch
        {
            FaultProbeStubKind.InvalidOpcode => InvalidOpcodeRequiredByteLength,
            FaultProbeStubKind.NullWrite => NullWriteRequiredByteLength,
            _ => 0
        };
    }

    public static FaultProbeStubPlanStatus Validate(FaultProbeStubKind kind, int outputBufferLength)
    {
        int requiredBytes = GetRequiredByteLength(kind);
        if (requiredBytes == 0)
        {
            return FaultProbeStubPlanStatus.UnknownKind;
        }

        if (outputBufferLength < requiredBytes)
        {
            return FaultProbeStubPlanStatus.OutputBufferTooSmall;
        }

        return FaultProbeStubPlanStatus.Success;
    }

    private static void WriteInvalidOpcode(Span<byte> destination)
    {
        destination[0] = TwoByteOpcodePrefix;
        destination[1] = Ud2Opcode;
        destination[2] = RetOpcode;
    }

    private static void WriteNullWrite(Span<byte> destination)
    {
        // C6 04 25 disp32 imm8 writes an immediate byte through absolute address zero in x64 mode.
        destination[0] = MovRm8Imm8Opcode;
        destination[1] = AbsoluteAddressModRm;
        destination[2] = AbsoluteAddressSib;
        destination[3] = 0x00;
        destination[4] = 0x00;
        destination[5] = 0x00;
        destination[6] = 0x00;
        destination[7] = NullWriteValue;
        destination[8] = RetOpcode;
    }
}
