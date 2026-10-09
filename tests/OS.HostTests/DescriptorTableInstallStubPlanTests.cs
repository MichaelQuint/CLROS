using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;
using OS.Std.Binary;

internal static class DescriptorTableInstallStubPlanTests
{
    private const ushort TssSelector = GdtDescriptorPlan.TssSelector;

    [HostTest("descriptor install stub plan encodes lidt stub", HostLane.Fast, 0)]
    private static bool TestEncodesLidtStub()
    {
        byte[] descriptor = CreateDescriptorPayload(0x10);
        byte[] bytes = CreatePoisonedBuffer(DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength + 1);

        DescriptorTableInstallStubPlanResult result =
            DescriptorTableInstallStubPlan.EmitLidt(descriptor, bytes);

        return HostCheck.Result(result.Succeeded
            && result.Status == DescriptorTableInstallStubPlanStatus.Ok
            && result.OperationKind == DescriptorTableInstructionKind.Lidt
            && result.BytesWritten == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && result.RequiredBytes == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && DescriptorTableInstallStubPlan.GetRequiredByteLength(DescriptorTableInstructionKind.Lidt)
                == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && DescriptorTableInstallStubPlan.Validate(
                DescriptorTableInstructionKind.Lidt,
                descriptor.Length,
                tssSelector: 0,
                bytes.Length) == DescriptorTableInstallStubPlanStatus.Ok
            && bytes[0] == DescriptorTableInstructionPlan.TwoByteOpcodePrefix
            && bytes[1] == DescriptorTableInstructionPlan.DescriptorTableOpcode
            && bytes[2] == DescriptorTableInstructionPlan.LidtRipRelativeModRm
            && EndianPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(
                    DescriptorTableInstructionPlan.RipRelativeDisplacementOffset,
                    DescriptorTableInstructionPlan.RipRelativeDisplacementLength))
                == DescriptorTableInstallStubPlan.EmbeddedDescriptorRipRelativeDisplacement
            && bytes[DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength]
                == DescriptorTableInstallStubPlan.RetOpcode
            && PayloadMatches(bytes, descriptor)
            && bytes[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength] == 0xcc);
    }

    [HostTest("descriptor install stub plan encodes lgdt stub", HostLane.Fast, 1)]
    private static bool TestEncodesLgdtStub()
    {
        byte[] descriptor = CreateDescriptorPayload(0x40);
        byte[] bytes = CreatePoisonedBuffer(DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength + 1);

        DescriptorTableInstallStubPlanResult result =
            DescriptorTableInstallStubPlan.EmitLgdt(descriptor, bytes);

        return HostCheck.Result(result.Succeeded
            && result.Status == DescriptorTableInstallStubPlanStatus.Ok
            && result.OperationKind == DescriptorTableInstructionKind.Lgdt
            && result.BytesWritten == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && result.RequiredBytes == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && DescriptorTableInstallStubPlan.GetRequiredByteLength(DescriptorTableInstructionKind.Lgdt)
                == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && bytes[0] == DescriptorTableInstructionPlan.TwoByteOpcodePrefix
            && bytes[1] == DescriptorTableInstructionPlan.DescriptorTableOpcode
            && bytes[2] == DescriptorTableInstructionPlan.LgdtRipRelativeModRm
            && EndianPrimitives.ReadUInt32LittleEndian(
                bytes.AsSpan(
                    DescriptorTableInstructionPlan.RipRelativeDisplacementOffset,
                    DescriptorTableInstructionPlan.RipRelativeDisplacementLength))
                == DescriptorTableInstallStubPlan.EmbeddedDescriptorRipRelativeDisplacement
            && bytes[DescriptorTableInstructionPlan.RipRelativeDescriptorInstructionLength]
                == DescriptorTableInstallStubPlan.RetOpcode
            && PayloadMatches(bytes, descriptor)
            && bytes[DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength] == 0xcc);
    }

    [HostTest("descriptor install stub plan encodes ltr stub", HostLane.Fast, 2)]
    private static bool TestEncodesLtrStub()
    {
        byte[] bytes = CreatePoisonedBuffer(DescriptorTableInstallStubPlan.LtrStubRequiredByteLength + 1);

        DescriptorTableInstallStubPlanResult result =
            DescriptorTableInstallStubPlan.EmitLtr(TssSelector, bytes);

        return HostCheck.Result(result.Succeeded
            && result.Status == DescriptorTableInstallStubPlanStatus.Ok
            && result.OperationKind == DescriptorTableInstructionKind.Ltr
            && result.BytesWritten == DescriptorTableInstallStubPlan.LtrStubRequiredByteLength
            && result.RequiredBytes == DescriptorTableInstallStubPlan.LtrStubRequiredByteLength
            && DescriptorTableInstallStubPlan.GetRequiredByteLength(DescriptorTableInstructionKind.Ltr)
                == DescriptorTableInstallStubPlan.LtrStubRequiredByteLength
            && DescriptorTableInstallStubPlan.Validate(
                DescriptorTableInstructionKind.Ltr,
                descriptorPayloadLength: 0,
                TssSelector,
                bytes.Length) == DescriptorTableInstallStubPlanStatus.Ok
            && bytes[0] == DescriptorTableInstallStubPlan.OperandSizeOverridePrefix
            && bytes[1] == DescriptorTableInstallStubPlan.MovAxImmediateOpcode
            && EndianPrimitives.ReadUInt16LittleEndian(
                bytes.AsSpan(DescriptorTableInstallStubPlan.LtrSelectorOffset, sizeof(ushort))) == TssSelector
            && bytes[DescriptorTableInstallStubPlan.LtrInstructionOffset]
                == DescriptorTableInstructionPlan.TwoByteOpcodePrefix
            && bytes[DescriptorTableInstallStubPlan.LtrInstructionOffset + 1]
                == DescriptorTableInstructionPlan.LtrOpcode
            && bytes[DescriptorTableInstallStubPlan.LtrInstructionOffset + 2]
                == DescriptorTableInstructionPlan.LtrAxModRm
            && bytes[DescriptorTableInstallStubPlan.LtrRetOffset] == DescriptorTableInstallStubPlan.RetOpcode
            && bytes[DescriptorTableInstallStubPlan.LtrStubRequiredByteLength] == 0xcc);
    }

    [HostTest("descriptor install stub plan rejects invalid inputs", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidInputs()
    {
        byte[] descriptor = CreateDescriptorPayload(0x70);
        byte[] validBytes = CreatePoisonedBuffer(DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength);
        byte[] shortBytes = CreatePoisonedBuffer(DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength - 1);
        byte[] shortDescriptor = CreateDescriptorPayload(0x90);
        byte[] ltrBytes = CreatePoisonedBuffer(DescriptorTableInstallStubPlan.LtrStubRequiredByteLength);

        DescriptorTableInstallStubPlanResult invalidKind = DescriptorTableInstallStubPlan.Emit(
            (DescriptorTableInstructionKind)99,
            descriptor,
            TssSelector,
            validBytes);
        DescriptorTableInstallStubPlanResult invalidDescriptor = DescriptorTableInstallStubPlan.EmitLidt(
            shortDescriptor.AsSpan(0, DescriptorTableInstallStubPlan.DescriptorPayloadLength - 1),
            validBytes);
        DescriptorTableInstallStubPlanResult invalidSelector = DescriptorTableInstallStubPlan.EmitLtr(0, ltrBytes);
        DescriptorTableInstallStubPlanResult outputTooSmall =
            DescriptorTableInstallStubPlan.EmitLgdt(descriptor, shortBytes);

        return HostCheck.Result(!invalidKind.Succeeded
            && invalidKind.Status == DescriptorTableInstallStubPlanStatus.InvalidOperationKind
            && invalidKind.OperationKind == (DescriptorTableInstructionKind)99
            && invalidKind.BytesWritten == 0
            && invalidKind.RequiredBytes == 0
            && !invalidDescriptor.Succeeded
            && invalidDescriptor.Status == DescriptorTableInstallStubPlanStatus.InvalidDescriptorPayloadLength
            && invalidDescriptor.BytesWritten == 0
            && invalidDescriptor.RequiredBytes == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && !invalidSelector.Succeeded
            && invalidSelector.Status == DescriptorTableInstallStubPlanStatus.InvalidSelector
            && invalidSelector.BytesWritten == 0
            && invalidSelector.RequiredBytes == DescriptorTableInstallStubPlan.LtrStubRequiredByteLength
            && !outputTooSmall.Succeeded
            && outputTooSmall.Status == DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall
            && outputTooSmall.BytesWritten == 0
            && outputTooSmall.RequiredBytes == DescriptorTableInstallStubPlan.DescriptorStubRequiredByteLength
            && validBytes[0] == 0xcc
            && ltrBytes[0] == 0xcc
            && shortBytes[0] == 0xcc
            && DescriptorTableInstallStubPlan.Validate(
                DescriptorTableInstructionKind.Unknown,
                descriptor.Length,
                TssSelector,
                validBytes.Length) == DescriptorTableInstallStubPlanStatus.InvalidOperationKind
            && DescriptorTableInstallStubPlan.Validate(
                DescriptorTableInstructionKind.Lidt,
                DescriptorTableInstallStubPlan.DescriptorPayloadLength - 1,
                TssSelector,
                validBytes.Length) == DescriptorTableInstallStubPlanStatus.InvalidDescriptorPayloadLength
            && DescriptorTableInstallStubPlan.Validate(
                DescriptorTableInstructionKind.Ltr,
                descriptorPayloadLength: 0,
                tssSelector: 0,
                ltrBytes.Length) == DescriptorTableInstallStubPlanStatus.InvalidSelector
            && DescriptorTableInstallStubPlan.Validate(
                DescriptorTableInstructionKind.Lgdt,
                descriptor.Length,
                TssSelector,
                shortBytes.Length) == DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall);
    }

    private static bool PayloadMatches(byte[] bytes, byte[] descriptor)
    {
        return bytes.AsSpan(
            DescriptorTableInstallStubPlan.EmbeddedDescriptorOffset,
            DescriptorTableInstallStubPlan.DescriptorPayloadLength).SequenceEqual(descriptor);
    }

    [HostTest("descriptor install stub plan uses shared buffer operations", HostLane.Fast, 4)]
    private static bool TestUsesSharedBufferOperations()
    {
        string source = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Diagnostics",
            "DescriptorTableInstallStubPlan.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(", StringComparison.Ordinal)
            && source.Contains("descriptorPayload,", StringComparison.Ordinal)
            && source.Contains(
                "destination.Slice(EmbeddedDescriptorOffset, DescriptorPayloadLength)",
                StringComparison.Ordinal)
            && source.Contains(
                "DescriptorTableInstallStubPlanStatus.OutputBufferTooSmall",
                StringComparison.Ordinal)
            && !source.Contains(
                "descriptorPayload.CopyTo(destination.Slice(EmbeddedDescriptorOffset, DescriptorPayloadLength))",
                StringComparison.Ordinal));
    }

    private static byte[] CreateDescriptorPayload(byte seed)
    {
        byte[] bytes = new byte[DescriptorTableInstallStubPlan.DescriptorPayloadLength];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(seed + index);
        }

        return bytes;
    }

    private static byte[] CreatePoisonedBuffer(int length)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, (byte)0xcc);
        return bytes;
    }
}
