using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Std.Binary;

internal static class DescriptorTableInstructionPlanTests
{
    private const int LidtDisplacement = unchecked((int)0x44332211);
    private const int LgdtDisplacement = unchecked((int)0x88776655);

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("descriptor instruction plan encodes lidt", TestEncodesLidt, ref failures);
        Run("descriptor instruction plan encodes lgdt", TestEncodesLgdt, ref failures);
        Run("descriptor instruction plan encodes ltr", TestEncodesLtr, ref failures);
        Run("descriptor instruction plan rejects invalid inputs", TestRejectsInvalidInputs, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Descriptor instruction plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestEncodesLidt()
    {
        byte[] bytes = CreatePoisonedBuffer(DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength + 1);

        DescriptorTableInstructionPlanResult result = DescriptorTableInstructionPlan.Write(
            DescriptorTableInstructionKind.Lidt,
            LidtDisplacement,
            bytes);

        return result.Succeeded
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
            && bytes[DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength] == 0xcc;
    }

    private static bool TestEncodesLgdt()
    {
        byte[] bytes = CreatePoisonedBuffer(DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength + 1);

        DescriptorTableInstructionPlanResult result = DescriptorTableInstructionPlan.Write(
            DescriptorTableInstructionKind.Lgdt,
            LgdtDisplacement,
            bytes);

        return result.Succeeded
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
            && bytes[DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength] == 0xcc;
    }

    private static bool TestEncodesLtr()
    {
        byte[] bytes = CreatePoisonedBuffer(DescriptorTableInstructionPlan.LtrAxInstructionLength + 1);

        DescriptorTableInstructionPlanResult result = DescriptorTableInstructionPlan.Write(
            DescriptorTableInstructionKind.Ltr,
            ripRelativeDisplacement: 0,
            bytes);

        return result.Succeeded
            && result.Status == DescriptorTableInstructionPlanStatus.Ok
            && result.OperationKind == DescriptorTableInstructionKind.Ltr
            && result.BytesWritten == DescriptorTableInstructionPlan.LtrAxInstructionLength
            && result.RequiredBytes == DescriptorTableInstructionPlan.LtrAxInstructionLength
            && DescriptorTableInstructionPlan.GetRequiredByteLength(DescriptorTableInstructionKind.Ltr)
                == DescriptorTableInstructionPlan.LtrAxInstructionLength
            && bytes[0] == DescriptorTableInstructionPlan.TwoByteOpcodePrefix
            && bytes[1] == DescriptorTableInstructionPlan.LtrOpcode
            && bytes[2] == DescriptorTableInstructionPlan.LtrAxModRm
            && bytes[DescriptorTableInstructionPlan.LtrAxInstructionLength] == 0xcc;
    }

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

        return !invalidKind.Succeeded
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
                == DescriptorTableInstructionPlanStatus.OutputBufferTooSmall;
    }

    private static byte[] CreatePoisonedBuffer(int length)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, (byte)0xcc);
        return bytes;
    }
}
