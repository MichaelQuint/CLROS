using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Std.Binary;

internal static class DescriptorTableInstructionPlanTests
{
    private const int LidtDisplacement = unchecked((int)0x44332211);
    private const int LgdtDisplacement = unchecked((int)0x88776655);

    [HostTest("descriptor instruction plan encodes lidt", HostLane.Fast, 0)]
    private static bool TestEncodesLidt()
    {
        byte[] bytes = CreatePoisonedBuffer(DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength + 1);

        DescriptorTableInstructionPlanResult result = DescriptorTableInstructionPlan.Write(
            DescriptorTableInstructionKind.Lidt,
            LidtDisplacement,
            bytes);

        return HostCheck.Result(result.Succeeded
            && result.Status == DescriptorTableInstructionPlanStatus.Ok
            && result.OperationKind == DescriptorTableInstructionKind.Lidt
            && result.BytesWritten == DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength
            && result.RequiredBytes == DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength
            && DescriptorTableInstructionPlan.GetRequiredByteLength(DescriptorTableInstructionKind.Lidt)
                == DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength
            && DescriptorTableInstructionPlan.Validate(
                DescriptorTableInstructionKind.Lidt,
                bytes.Length) == DescriptorTableInstructionPlanStatus.Ok
            && bytes[0] == DescriptorTableInstructionPlan.TwoByteOpcodePrefix
            && bytes[1] == DescriptorTableInstructionPlan.DescriptorTableOpcode
            && bytes[2] == DescriptorTableInstructionPlan.LidtRipRelativeModRm
            && EndianPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(
                    DescriptorTableInstructionPlan.RipRelativeDisplacementOffset,
                    DescriptorTableInstructionPlan.RipRelativeDisplacementLength)) == unchecked((uint)LidtDisplacement)
            && bytes[DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength] == 0xcc);
    }

    [HostTest("descriptor instruction plan encodes lgdt", HostLane.Fast, 1)]
    private static bool TestEncodesLgdt()
    {
        byte[] bytes = CreatePoisonedBuffer(DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength + 1);

        DescriptorTableInstructionPlanResult result = DescriptorTableInstructionPlan.Write(
            DescriptorTableInstructionKind.Lgdt,
            LgdtDisplacement,
            bytes);

        return HostCheck.Result(result.Succeeded
            && result.Status == DescriptorTableInstructionPlanStatus.Ok
            && result.OperationKind == DescriptorTableInstructionKind.Lgdt
            && result.BytesWritten == DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength
            && result.RequiredBytes == DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength
            && DescriptorTableInstructionPlan.GetRequiredByteLength(DescriptorTableInstructionKind.Lgdt)
                == DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength
            && bytes[0] == DescriptorTableInstructionPlan.TwoByteOpcodePrefix
            && bytes[1] == DescriptorTableInstructionPlan.DescriptorTableOpcode
            && bytes[2] == DescriptorTableInstructionPlan.LgdtRipRelativeModRm
            && EndianPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(
                    DescriptorTableInstructionPlan.RipRelativeDisplacementOffset,
                    DescriptorTableInstructionPlan.RipRelativeDisplacementLength)) == unchecked((uint)LgdtDisplacement)
            && bytes[DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength] == 0xcc);
    }

    [HostTest("descriptor instruction plan encodes ltr", HostLane.Fast, 2)]
    private static bool TestEncodesLtr()
    {
        byte[] bytes = CreatePoisonedBuffer(DescriptorTableInstructionPlan.LtrAxInstructionLength + 1);

        DescriptorTableInstructionPlanResult result = DescriptorTableInstructionPlan.Write(
            DescriptorTableInstructionKind.Ltr,
            ripRelativeDisplacement: 0,
            bytes);

        return HostCheck.Result(result.Succeeded
            && result.Status == DescriptorTableInstructionPlanStatus.Ok
            && result.OperationKind == DescriptorTableInstructionKind.Ltr
            && result.BytesWritten == DescriptorTableInstructionPlan.LtrAxInstructionLength
            && result.RequiredBytes == DescriptorTableInstructionPlan.LtrAxInstructionLength
            && DescriptorTableInstructionPlan.GetRequiredByteLength(DescriptorTableInstructionKind.Ltr)
                == DescriptorTableInstructionPlan.LtrAxInstructionLength
            && bytes[0] == DescriptorTableInstructionPlan.TwoByteOpcodePrefix
            && bytes[1] == DescriptorTableInstructionPlan.LtrOpcode
            && bytes[2] == DescriptorTableInstructionPlan.LtrAxModRm
            && bytes[DescriptorTableInstructionPlan.LtrAxInstructionLength] == 0xcc);
    }

    [HostTest("descriptor instruction plan rejects invalid inputs", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidInputs()
    {
        byte[] validBytes = CreatePoisonedBuffer(DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength);
        byte[] shortRipBytes = CreatePoisonedBuffer(DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength - 1);
        byte[] shortLtrBytes = CreatePoisonedBuffer(DescriptorTableInstructionPlan.LtrAxInstructionLength - 1);

        DescriptorTableInstructionPlanResult invalidKind = DescriptorTableInstructionPlan.Write(
            (DescriptorTableInstructionKind)99,
            ripRelativeDisplacement: 0,
            validBytes);
        DescriptorTableInstructionPlanResult shortLidt = DescriptorTableInstructionPlan.Write(
            DescriptorTableInstructionKind.Lidt,
            LidtDisplacement,
            shortRipBytes);
        DescriptorTableInstructionPlanResult shortLtr = DescriptorTableInstructionPlan.Write(
            DescriptorTableInstructionKind.Ltr,
            ripRelativeDisplacement: 0,
            shortLtrBytes);

        return HostCheck.Result(!invalidKind.Succeeded
            && invalidKind.Status == DescriptorTableInstructionPlanStatus.InvalidOperationKind
            && invalidKind.OperationKind == (DescriptorTableInstructionKind)99
            && invalidKind.BytesWritten == 0
            && invalidKind.RequiredBytes == 0
            && validBytes[0] == 0xcc
            && !shortLidt.Succeeded
            && shortLidt.Status == DescriptorTableInstructionPlanStatus.OutputBufferTooSmall
            && shortLidt.BytesWritten == 0
            && shortLidt.RequiredBytes == DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength
            && shortRipBytes[0] == 0xcc
            && !shortLtr.Succeeded
            && shortLtr.Status == DescriptorTableInstructionPlanStatus.OutputBufferTooSmall
            && shortLtr.BytesWritten == 0
            && shortLtr.RequiredBytes == DescriptorTableInstructionPlan.LtrAxInstructionLength
            && shortLtrBytes[0] == 0xcc
            && DescriptorTableInstructionPlan.Validate(
                DescriptorTableInstructionKind.Unknown,
                DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength)
                == DescriptorTableInstructionPlanStatus.InvalidOperationKind
            && DescriptorTableInstructionPlan.Validate(
                DescriptorTableInstructionKind.Lgdt,
                DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength - 1)
                == DescriptorTableInstructionPlanStatus.OutputBufferTooSmall);
    }

    private static byte[] CreatePoisonedBuffer(int length)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, (byte)0xcc);
        return bytes;
    }
}
