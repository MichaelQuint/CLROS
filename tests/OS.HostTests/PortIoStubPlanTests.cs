using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class PortIoStubPlanTests
{

    [HostTest("port io stub plan encodes callable out8 stub", HostLane.Fast, 0)]
    private static bool TestEncodesCallableOut8Stub()
    {
        byte[] bytes = new byte[PortIoStubPlan.Out8RequiredByteLength];

        PortIoStubPlanResult result = PortIoStubPlan.Out8.Emit(bytes);

        return HostCheck.Result(result.Succeeded
            && result.Status == PortIoStubPlanStatus.Success
            && result.BytesWritten == PortIoStubPlan.Out8RequiredByteLength
            && result.RequiredBytes == PortIoStubPlan.Out8RequiredByteLength
            && PortIoStubPlan.Out8.OperationKind == PortIoOperationKind.Out8
            && bytes[0] == PortIoStubPlan.MovR8ToRm8Opcode
            && bytes[1] == PortIoStubPlan.MovDlToAlModRm
            && bytes[2] == PortIoStubPlan.OperandSizeOverridePrefix
            && bytes[3] == PortIoStubPlan.MovR16ToRm16Opcode
            && bytes[4] == PortIoStubPlan.MovCxToDxModRm
            && bytes[5] == PortIoOperationPlan.Out8Opcode
            && bytes[6] == PortIoStubPlan.RetOpcode
            && bytes[0] == 0x88
            && bytes[1] == 0xd0
            && bytes[2] == 0x66
            && bytes[3] == 0x89
            && bytes[4] == 0xca
            && bytes[5] == 0xee
            && bytes[6] == 0xc3);
    }

    [HostTest("port io stub plan encodes callable in8 stub", HostLane.Fast, 1)]
    private static bool TestEncodesCallableIn8Stub()
    {
        byte[] bytes = new byte[PortIoStubPlan.In8RequiredByteLength];

        PortIoStubPlanResult result = PortIoStubPlan.In8.Emit(bytes);

        return HostCheck.Result(result.Succeeded
            && result.Status == PortIoStubPlanStatus.Success
            && result.BytesWritten == PortIoStubPlan.In8RequiredByteLength
            && result.RequiredBytes == PortIoStubPlan.In8RequiredByteLength
            && PortIoStubPlan.In8.OperationKind == PortIoOperationKind.In8
            && bytes[0] == PortIoStubPlan.OperandSizeOverridePrefix
            && bytes[1] == PortIoStubPlan.MovR16ToRm16Opcode
            && bytes[2] == PortIoStubPlan.MovCxToDxModRm
            && bytes[3] == PortIoOperationPlan.In8Opcode
            && bytes[4] == PortIoStubPlan.RetOpcode
            && bytes[0] == 0x66
            && bytes[1] == 0x89
            && bytes[2] == 0xca
            && bytes[3] == 0xec
            && bytes[4] == 0xc3);
    }

    [HostTest("port io stub plan rejects invalid inputs", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidInputs()
    {
        byte[] bytes = new byte[PortIoStubPlan.MaximumRequiredByteLength];
        byte[] shortBytes = new byte[PortIoStubPlan.In8RequiredByteLength - 1];

        PortIoStubPlanResult invalidDefault = default(PortIoStubPlan).Emit(bytes);
        PortIoStubPlanResult invalidUnknown =
            new PortIoStubPlan((PortIoOperationKind)255).Emit(bytes);
        PortIoStubPlanResult shortBuffer = PortIoStubPlan.In8.Emit(shortBytes);

        return HostCheck.Result(!invalidDefault.Succeeded
            && invalidDefault.Status == PortIoStubPlanStatus.InvalidOperationKind
            && invalidDefault.BytesWritten == 0
            && invalidDefault.RequiredBytes == 0
            && !invalidUnknown.Succeeded
            && invalidUnknown.Status == PortIoStubPlanStatus.InvalidOperationKind
            && invalidUnknown.BytesWritten == 0
            && invalidUnknown.RequiredBytes == 0
            && !shortBuffer.Succeeded
            && shortBuffer.Status == PortIoStubPlanStatus.OutputBufferTooSmall
            && shortBuffer.BytesWritten == 0
            && shortBuffer.RequiredBytes == PortIoStubPlan.In8RequiredByteLength
            && PortIoStubPlan.GetRequiredByteLength(PortIoOperationKind.Out8) == PortIoStubPlan.Out8RequiredByteLength
            && PortIoStubPlan.GetRequiredByteLength(PortIoOperationKind.In8) == PortIoStubPlan.In8RequiredByteLength
            && PortIoStubPlan.GetRequiredByteLength(PortIoOperationKind.None) == 0
            && bytes[0] == 0);
    }

    [HostTest("port io stub plan default result is not success", HostLane.Fast, 3)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        PortIoStubPlanResult result = default;
        PortIoStubPlanResult failed =
            PortIoStubPlanResult.Fail(PortIoStubPlanStatus.InvalidOperationKind, requiredBytes: 0);

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == PortIoStubPlanStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == PortIoStubPlanStatus.InvalidOperationKind
            && failed.BytesWritten == 0
            && failed.RequiredBytes == 0
            && !PortIoStubPlanResult.Success(0, 0).Succeeded
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PortIoStubPlanResult>());
    }
}
