using OS.Std.Binary;
using OS.Std.Memory;

namespace OS.Kernel.Diagnostics;
public static class DescriptorTableInstallStubPlan
{
    public const int DescriptorPayloadLength = IdtrDescriptor.EncodedLength;
    public const int DescriptorStubExecutableByteLength = DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength + RetInstructionLength;
    public const int DescriptorStubRequiredByteLength = DescriptorStubExecutableByteLength + DescriptorPayloadLength;
    public const int LtrStubRequiredByteLength = MovAxImmediate16InstructionLength + DescriptorTableInstructionPlan.LtrAxInstructionLength + RetInstructionLength;
    public const int MaximumRequiredByteLength = DescriptorStubRequiredByteLength;
    public const int EmbeddedDescriptorOffset = DescriptorStubExecutableByteLength;
    public const int EmbeddedDescriptorRipRelativeDisplacement = EmbeddedDescriptorOffset - DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength;
    public const byte OperandSizeOverridePrefix = 0x66;
    public const byte MovAxImmediateOpcode = 0xb8;
    public const byte RetOpcode = 0xc3;
    public const int MovAxImmediate16InstructionLength = 4;
    public const int RetInstructionLength = 1;
    public const int MovAxImmediate16Offset = 0;
    public const int LtrInstructionOffset = MovAxImmediate16InstructionLength;
    public const int LtrRetOffset = LtrInstructionOffset + DescriptorTableInstructionPlan.LtrAxInstructionLength;
    public const int LtrSelectorOffset = 2;
    public static DescriptorTableInstallStubPlanResult Emit(DescriptorTableInstructionKind operationKind, ReadOnlySpan<byte> descriptorPayload, ushort tssSelector, Span<byte> destination)
    {
        if (operationKind == DescriptorTableInstructionKind.Lidt || operationKind == DescriptorTableInstructionKind.Lgdt)
        {
            return EmitDescriptorStub(operationKind, descriptorPayload, destination);
        }

        if (operationKind == DescriptorTableInstructionKind.Ltr)
        {
            return EmitLtr(tssSelector, destination);
        }

        return DescriptorTableInstallStubPlanResult.Fail(DescriptorTableInstallStubPlanStatus.InvalidOperationKind, operationKind, 0);
    }

    public static DescriptorTableInstallStubPlanResult EmitLidt(ReadOnlySpan<byte> idtrPayload, Span<byte> destination)
    {
        return EmitDescriptorStub(DescriptorTableInstructionKind.Lidt, idtrPayload, destination);
    }

    public static DescriptorTableInstallStubPlanResult EmitLgdt(ReadOnlySpan<byte> gdtrPayload, Span<byte> destination)
    {
        return EmitDescriptorStub(DescriptorTableInstructionKind.Lgdt, gdtrPayload, destination);
    }

    public static DescriptorTableInstallStubPlanResult EmitLtr(ushort tssSelector, Span<byte> destination)
    {
        if (!IsValidTssSelector(tssSelector))
        {
            return DescriptorTableInstallStubPlanResult.Fail(DescriptorTableInstallStubPlanStatus.InvalidSelector, DescriptorTableInstructionKind.Ltr, LtrStubRequiredByteLength);
        }

        if (destination.Length < LtrStubRequiredByteLength)
        {
            return DescriptorTableInstallStubPlanResult.Fail(DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall, DescriptorTableInstructionKind.Ltr, LtrStubRequiredByteLength);
        }

        destination[MovAxImmediate16Offset] = OperandSizeOverridePrefix;
        destination[MovAxImmediate16Offset + 1] = MovAxImmediateOpcode;
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice(LtrSelectorOffset, sizeof(ushort)), tssSelector);
        DescriptorTableInstructionPlanResult instructionResult = DescriptorTableInstructionPlan.Write(DescriptorTableInstructionKind.Ltr, ripRelativeDisplacement: 0, destination.Slice(LtrInstructionOffset, DescriptorTableInstructionPlan.LtrAxInstructionLength));
        if (!instructionResult.Succeeded)
        {
            return DescriptorTableInstallStubPlanResult.Fail(DescriptorTableInstallStubPlanStatus.InvalidOperationKind, DescriptorTableInstructionKind.Ltr, LtrStubRequiredByteLength);
        }

        destination[LtrRetOffset] = RetOpcode;
        return DescriptorTableInstallStubPlanResult.Success(DescriptorTableInstructionKind.Ltr, LtrStubRequiredByteLength);
    }

    public static int GetRequiredByteLength(DescriptorTableInstructionKind operationKind)
    {
        return operationKind switch
        {
            DescriptorTableInstructionKind.Lidt => DescriptorStubRequiredByteLength,
            DescriptorTableInstructionKind.Lgdt => DescriptorStubRequiredByteLength,
            DescriptorTableInstructionKind.Ltr => LtrStubRequiredByteLength,
            _ => 0
        };
    }

    public static DescriptorTableInstallStubPlanStatus Validate(DescriptorTableInstructionKind operationKind, int descriptorPayloadLength, ushort tssSelector, int outputBufferLength)
    {
        int requiredBytes = GetRequiredByteLength(operationKind);
        if (requiredBytes == 0)
        {
            return DescriptorTableInstallStubPlanStatus.InvalidOperationKind;
        }

        if (operationKind == DescriptorTableInstructionKind.Lidt || operationKind == DescriptorTableInstructionKind.Lgdt)
        {
            if (descriptorPayloadLength != DescriptorPayloadLength)
            {
                return DescriptorTableInstallStubPlanStatus.InvalidDescriptorPayloadLength;
            }
        }
        else if (!IsValidTssSelector(tssSelector))
        {
            return DescriptorTableInstallStubPlanStatus.InvalidSelector;
        }

        if (outputBufferLength < requiredBytes)
        {
            return DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall;
        }

        return DescriptorTableInstallStubPlanStatus.Ok;
    }

    private static DescriptorTableInstallStubPlanResult EmitDescriptorStub(DescriptorTableInstructionKind operationKind, ReadOnlySpan<byte> descriptorPayload, Span<byte> destination)
    {
        if (descriptorPayload.Length != DescriptorPayloadLength)
        {
            return DescriptorTableInstallStubPlanResult.Fail(DescriptorTableInstallStubPlanStatus.InvalidDescriptorPayloadLength, operationKind, DescriptorStubRequiredByteLength);
        }

        if (destination.Length < DescriptorStubRequiredByteLength)
        {
            return DescriptorTableInstallStubPlanResult.Fail(DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall, operationKind, DescriptorStubRequiredByteLength);
        }

        DescriptorTableInstructionPlanResult instructionResult = DescriptorTableInstructionPlan.Write(operationKind, EmbeddedDescriptorRipRelativeDisplacement, destination.Slice(0, DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength));
        if (!instructionResult.Succeeded)
        {
            return DescriptorTableInstallStubPlanResult.Fail(DescriptorTableInstallStubPlanStatus.InvalidOperationKind, operationKind, DescriptorStubRequiredByteLength);
        }

        destination[DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength] = RetOpcode;
        BufferOperationResult copyResult = BufferOperations.MemoryCopy(descriptorPayload, destination.Slice(EmbeddedDescriptorOffset, DescriptorPayloadLength));
        if (!copyResult.Succeeded)
        {
            return DescriptorTableInstallStubPlanResult.Fail(DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall, operationKind, DescriptorStubRequiredByteLength);
        }

        return DescriptorTableInstallStubPlanResult.Success(operationKind, DescriptorStubRequiredByteLength);
    }

    private static bool IsValidTssSelector(ushort tssSelector)
    {
        return (tssSelector & 0xfff8) != 0;
    }
}
