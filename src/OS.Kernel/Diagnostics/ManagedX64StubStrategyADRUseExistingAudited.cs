namespace OS.Kernel.Diagnostics;

public static class ManagedX64StubStrategyADRUseExistingAudited
{
    public const int CurrentDemonstratedFamilyCount = 1;
    public const int GeneratorEligibilityThreshold = 2;
    public const bool RequiresWriteThenExecute = true;
    public const bool AllowsPermanentRwx = false;
    public static ManagedX64StubStrategyResult ResolveCurrent()
    {
        return Evaluate(CurrentDemonstratedFamilyCount, GeneratorEligibilityThreshold, PortIoStubPlan.Out8RequiredByteLength, PortIoStubPlan.In8RequiredByteLength, RequiresWriteThenExecute, AllowsPermanentRwx);
    }

    public static ManagedX64StubStrategyResult Evaluate(int demonstratedFamilyCount, int generatorThreshold, int out8ByteCount, int in8ByteCount, bool writeThenExecute, bool permanentRwx)
    {
        ManagedX64StubStrategyStatus status = Validate(demonstratedFamilyCount, generatorThreshold, out8ByteCount, in8ByteCount, writeThenExecute, permanentRwx);
        if (status != ManagedX64StubStrategyStatus.Success)
        {
            return ManagedX64StubStrategyResult.Fail(status, demonstratedFamilyCount, generatorThreshold, out8ByteCount, in8ByteCount, writeThenExecute, permanentRwx);
        }

        bool generatorEligible = demonstratedFamilyCount >= generatorThreshold;
        ManagedX64StubAuthoringKind strategy = generatorEligible ? ManagedX64StubAuthoringKind.BuildTimeGenerator : ManagedX64StubAuthoringKind.ExistingAuditedBytePlans;
        return ManagedX64StubStrategyResult.Success(strategy, demonstratedFamilyCount, generatorThreshold, generatorEligible, out8ByteCount, in8ByteCount, writeThenExecute, permanentRwx);
    }

    public static ManagedX64StubStrategyStatus Validate(int demonstratedFamilyCount, int generatorThreshold, int out8ByteCount, int in8ByteCount, bool writeThenExecute, bool permanentRwx)
    {
        if (!HasExactAuditedBytePlanContract())
        {
            return ManagedX64StubStrategyStatus.AuditedBytePlanContractMismatch;
        }

        if (demonstratedFamilyCount <= 0)
        {
            return ManagedX64StubStrategyStatus.MissingDemonstratedFamily;
        }

        if (generatorThreshold < GeneratorEligibilityThreshold)
        {
            return ManagedX64StubStrategyStatus.GeneratorThresholdTooLow;
        }

        if (out8ByteCount != PortIoStubPlan.Out8RequiredByteLength)
        {
            return ManagedX64StubStrategyStatus.Out8ByteLengthMismatch;
        }

        if (in8ByteCount != PortIoStubPlan.In8RequiredByteLength)
        {
            return ManagedX64StubStrategyStatus.In8ByteLengthMismatch;
        }

        if (!writeThenExecute)
        {
            return ManagedX64StubStrategyStatus.WriteThenExecuteRequired;
        }

        if (permanentRwx)
        {
            return ManagedX64StubStrategyStatus.PermanentRwxForbidden;
        }

        return ManagedX64StubStrategyStatus.Success;
    }

    private static unsafe bool HasExactAuditedBytePlanContract()
    {
        byte* out8 = stackalloc byte[PortIoStubPlan.Out8RequiredByteLength];
        byte* in8 = stackalloc byte[PortIoStubPlan.In8RequiredByteLength];
        PortIoStubPlanResult out8Result = PortIoStubPlan.Out8.Emit(out8, PortIoStubPlan.Out8RequiredByteLength);
        PortIoStubPlanResult in8Result = PortIoStubPlan.In8.Emit(in8, PortIoStubPlan.In8RequiredByteLength);
        return PortIoStubPlan.Out8RequiredByteLength == 7 && PortIoStubPlan.In8RequiredByteLength == 5 && PortIoStubPlan.OperandSizeOverridePrefix == 0x66 && PortIoStubPlan.MovR16ToRm16Opcode == 0x89 && PortIoStubPlan.MovCxToDxModRm == 0xca && PortIoStubPlan.MovR8ToRm8Opcode == 0x88 && PortIoStubPlan.MovDlToAlModRm == 0xd0 && PortIoOperationPlan.Out8Opcode == 0xee && PortIoOperationPlan.In8Opcode == 0xec && PortIoStubPlan.RetOpcode == 0xc3 && out8Result.Succeeded && in8Result.Succeeded && out8[0] == PortIoStubPlan.MovR8ToRm8Opcode && out8[1] == PortIoStubPlan.MovDlToAlModRm && out8[2] == PortIoStubPlan.OperandSizeOverridePrefix && out8[3] == PortIoStubPlan.MovR16ToRm16Opcode && out8[4] == PortIoStubPlan.MovCxToDxModRm && out8[5] == PortIoOperationPlan.Out8Opcode && out8[6] == PortIoStubPlan.RetOpcode && in8[0] == PortIoStubPlan.OperandSizeOverridePrefix && in8[1] == PortIoStubPlan.MovR16ToRm16Opcode && in8[2] == PortIoStubPlan.MovCxToDxModRm && in8[3] == PortIoOperationPlan.In8Opcode && in8[4] == PortIoStubPlan.RetOpcode;
    }
}
