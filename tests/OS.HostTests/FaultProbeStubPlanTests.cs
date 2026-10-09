using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class FaultProbeStubPlanTests
{

    [HostTest("fault probe stub plan encodes invalid opcode probe", HostLane.Fast, 0)]
    private static bool TestEncodesInvalidOpcodeProbe()
    {
        byte[] bytes = CreatePoisonedBuffer(FaultProbeStubPlan.InvalidOpcodeRequiredByteLength + 1);

        FaultProbeStubPlanResult result = FaultProbeStubPlan.InvalidOpcode.Emit(bytes);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == FaultProbeStubPlanStatus.Success
            && result.Kind == FaultProbeStubKind.InvalidOpcode
            && result.BytesWritten == FaultProbeStubPlan.InvalidOpcodeRequiredByteLength
            && result.RequiredBytes == FaultProbeStubPlan.InvalidOpcodeRequiredByteLength
            && FaultProbeStubPlan.GetRequiredByteLength(FaultProbeStubKind.InvalidOpcode)
                == FaultProbeStubPlan.InvalidOpcodeRequiredByteLength
            && FaultProbeStubPlan.Validate(FaultProbeStubKind.InvalidOpcode, bytes.Length)
                == FaultProbeStubPlanStatus.Success
            && bytes[0] == FaultProbeStubPlan.TwoByteOpcodePrefix
            && bytes[1] == FaultProbeStubPlan.Ud2Opcode
            && bytes[2] == FaultProbeStubPlan.RetOpcode
            && bytes[3] == 0xcc);
    }

    [HostTest("fault probe stub plan encodes null write probe", HostLane.Fast, 1)]
    private static bool TestEncodesNullWriteProbe()
    {
        byte[] bytes = CreatePoisonedBuffer(FaultProbeStubPlan.NullWriteRequiredByteLength + 1);

        FaultProbeStubPlanResult result = FaultProbeStubPlan.NullWrite.Emit(bytes);

        return HostCheck.Result(result.Succeeded
            && result.Status == FaultProbeStubPlanStatus.Success
            && result.Kind == FaultProbeStubKind.NullWrite
            && result.BytesWritten == FaultProbeStubPlan.NullWriteRequiredByteLength
            && result.RequiredBytes == FaultProbeStubPlan.NullWriteRequiredByteLength
            && FaultProbeStubPlan.GetRequiredByteLength(FaultProbeStubKind.NullWrite)
                == FaultProbeStubPlan.NullWriteRequiredByteLength
            && bytes[0] == FaultProbeStubPlan.MovRm8Imm8Opcode
            && bytes[1] == FaultProbeStubPlan.AbsoluteAddressModRm
            && bytes[2] == FaultProbeStubPlan.AbsoluteAddressSib
            && bytes[3] == 0x00
            && bytes[4] == 0x00
            && bytes[5] == 0x00
            && bytes[6] == 0x00
            && bytes[7] == FaultProbeStubPlan.NullWriteValue
            && bytes[8] == FaultProbeStubPlan.RetOpcode
            && bytes[9] == 0xcc);
    }

    [HostTest("fault probe stub plan keeps destructive probes disabled by default", HostLane.Fast, 2)]
    private static bool TestKeepsDestructiveProbesDisabledByDefault()
    {
        ProbeDefinition invalidOpcode = FaultProbeStubPlan.InvalidOpcode.ProbeDefinition;
        ProbeDefinition nullWrite = FaultProbeStubPlan.NullWrite.ProbeDefinition;

        return HostCheck.Result(invalidOpcode.Name == nameof(Probes.EnableInvalidOpcodePanicProbe)
            && invalidOpcode.Group == Probes.PanicSmokeGroup
            && !invalidOpcode.EnabledByDefault
            && invalidOpcode.Risk == ProbeRisk.Destructive
            && nullWrite.Name == nameof(Probes.EnableNullPointerPanicProbe)
            && nullWrite.Group == Probes.PanicSmokeGroup
            && !nullWrite.EnabledByDefault
            && nullWrite.Risk == ProbeRisk.Destructive
            && !Probes.EnableInvalidOpcodePanicProbe
            && !Probes.EnableNullPointerPanicProbe);
    }

    [HostTest("fault probe stub plan rejects invalid inputs", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidInputs()
    {
        byte[] validBytes = CreatePoisonedBuffer(FaultProbeStubPlan.MaximumRequiredByteLength);
        byte[] shortInvalidOpcodeBytes = CreatePoisonedBuffer(FaultProbeStubPlan.InvalidOpcodeRequiredByteLength - 1);
        byte[] shortNullWriteBytes = CreatePoisonedBuffer(FaultProbeStubPlan.NullWriteRequiredByteLength - 1);

        FaultProbeStubPlanResult defaultKind = default(FaultProbeStubPlan).Emit(validBytes);
        FaultProbeStubPlanResult unknownKind = new FaultProbeStubPlan((FaultProbeStubKind)255).Emit(validBytes);
        FaultProbeStubPlanResult shortInvalidOpcode = FaultProbeStubPlan.InvalidOpcode.Emit(shortInvalidOpcodeBytes);
        FaultProbeStubPlanResult shortNullWrite = FaultProbeStubPlan.NullWrite.Emit(shortNullWriteBytes);

        return HostCheck.Result(!defaultKind.Succeeded
            && defaultKind.IsInitialized
            && defaultKind.Status == FaultProbeStubPlanStatus.UnknownKind
            && defaultKind.Kind == FaultProbeStubKind.Unknown
            && defaultKind.BytesWritten == 0
            && defaultKind.RequiredBytes == 0
            && validBytes[0] == 0xcc
            && !unknownKind.Succeeded
            && unknownKind.Status == FaultProbeStubPlanStatus.UnknownKind
            && unknownKind.Kind == (FaultProbeStubKind)255
            && unknownKind.BytesWritten == 0
            && unknownKind.RequiredBytes == 0
            && !shortInvalidOpcode.Succeeded
            && shortInvalidOpcode.Status == FaultProbeStubPlanStatus.OutputBufferTooSmall
            && shortInvalidOpcode.Kind == FaultProbeStubKind.InvalidOpcode
            && shortInvalidOpcode.BytesWritten == 0
            && shortInvalidOpcode.RequiredBytes == FaultProbeStubPlan.InvalidOpcodeRequiredByteLength
            && shortInvalidOpcodeBytes[0] == 0xcc
            && !shortNullWrite.Succeeded
            && shortNullWrite.Status == FaultProbeStubPlanStatus.OutputBufferTooSmall
            && shortNullWrite.Kind == FaultProbeStubKind.NullWrite
            && shortNullWrite.BytesWritten == 0
            && shortNullWrite.RequiredBytes == FaultProbeStubPlan.NullWriteRequiredByteLength
            && shortNullWriteBytes[0] == 0xcc
            && FaultProbeStubPlan.Validate(FaultProbeStubKind.Unknown, FaultProbeStubPlan.MaximumRequiredByteLength)
                == FaultProbeStubPlanStatus.UnknownKind
            && FaultProbeStubPlan.Validate(FaultProbeStubKind.NullWrite, FaultProbeStubPlan.NullWriteRequiredByteLength - 1)
                == FaultProbeStubPlanStatus.OutputBufferTooSmall);
    }

    [HostTest("fault probe stub plan default result is not success", HostLane.Fast, 4)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        FaultProbeStubPlanResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == FaultProbeStubPlanStatus.Success
            && result.Kind == FaultProbeStubKind.Unknown
            && result.BytesWritten == 0
            && result.RequiredBytes == 0);
    }

    private static byte[] CreatePoisonedBuffer(int length)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, (byte)0xcc);
        return bytes;
    }
}
