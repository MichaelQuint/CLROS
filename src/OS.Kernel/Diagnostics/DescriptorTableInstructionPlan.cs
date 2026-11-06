using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;

public static class DescriptorTableInstructionPlan
{
    public const int RipRelativeDescriptorInstructionLength = 7;
    public const int LtrAxInstructionLength = 3;
    public const byte TwoByteOpcodePrefix = 0x0f;
    public const byte DescriptorTableOpcode = 0x01;
    public const byte LgdtRipRelativeModRm = 0x15;
    public const byte LidtRipRelativeModRm = 0x1d;
    public const int RipRelativeDisplacementOffset = 3;
    public const int RipRelativeDisplacementLength = 4;
    public const byte LtrOpcode = 0x00;
    public const byte LtrAxModRm = 0xd8;
    public static DescriptorTableInstructionPlanResult Write(DescriptorTableInstructionKind operationKind, int ripRelativeDisplacement, Span<byte> destination)
    {
        int requiredBytes = GetRequiredByteLength(operationKind);
        if (requiredBytes == 0)
        {
            return DescriptorTableInstructionPlanResult.Fail(DescriptorTableInstructionPlanStatus.InvalidOperationKind, operationKind, 0);
        }

        if (destination.Length < requiredBytes)
        {
            return DescriptorTableInstructionPlanResult.Fail(DescriptorTableInstructionPlanStatus.OutputBufferTooSmall, operationKind, requiredBytes);
        }

        if (operationKind == DescriptorTableInstructionKind.Ltr)
        {
            WriteLtrAx(destination);
            return DescriptorTableInstructionPlanResult.Success(operationKind, LtrAxInstructionLength);
        }

        WriteRipRelativeDescriptorInstruction(operationKind == DescriptorTableInstructionKind.Lgdt ? LgdtRipRelativeModRm : LidtRipRelativeModRm, ripRelativeDisplacement, destination);
        return DescriptorTableInstructionPlanResult.Success(operationKind, RipRelativeDescriptorInstructionLength);
    }

    public static int GetRequiredByteLength(DescriptorTableInstructionKind operationKind)
    {
        return operationKind switch
        {
            DescriptorTableInstructionKind.Lidt => RipRelativeDescriptorInstructionLength,
            DescriptorTableInstructionKind.Lgdt => RipRelativeDescriptorInstructionLength,
            DescriptorTableInstructionKind.Ltr => LtrAxInstructionLength,
            _ => 0
        };
    }

    public static DescriptorTableInstructionPlanStatus Validate(DescriptorTableInstructionKind operationKind, int outputBufferLength)
    {
        int requiredBytes = GetRequiredByteLength(operationKind);
        if (requiredBytes == 0)
        {
            return DescriptorTableInstructionPlanStatus.InvalidOperationKind;
        }

        if (outputBufferLength < requiredBytes)
        {
            return DescriptorTableInstructionPlanStatus.OutputBufferTooSmall;
        }

        return DescriptorTableInstructionPlanStatus.Ok;
    }

    private static void WriteRipRelativeDescriptorInstruction(byte modRm, int ripRelativeDisplacement, Span<byte> destination)
    {
        destination[0] = TwoByteOpcodePrefix;
        destination[1] = DescriptorTableOpcode;
        destination[2] = modRm;
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(RipRelativeDisplacementOffset, RipRelativeDisplacementLength), (uint)ripRelativeDisplacement);
    }

    private static void WriteLtrAx(Span<byte> destination)
    {
        destination[0] = TwoByteOpcodePrefix;
        destination[1] = LtrOpcode;
        destination[2] = LtrAxModRm;
    }
}
