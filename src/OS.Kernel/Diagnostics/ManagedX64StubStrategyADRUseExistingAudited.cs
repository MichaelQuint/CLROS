namespace OS.Kernel.Diagnostics;

public enum ManagedX64StubAuthoringKind : byte
{
    None = 0,
    ExistingAuditedBytePlans = 1,
    BuildTimeGenerator = 2
}

public enum ManagedX64StubStrategyStatus : byte
{
    Success = 0,
    MissingDemonstratedFamily = 1,
    GeneratorThresholdTooLow = 2,
    Out8ByteLengthMismatch = 3,
    In8ByteLengthMismatch = 4,
    WriteThenExecuteRequired = 5,
    PermanentRwxForbidden = 6,
    AuditedBytePlanContractMismatch = 7
}

public readonly struct ManagedX64StubStrategyResult
{
    private ManagedX64StubStrategyResult(
        ManagedX64StubStrategyStatus status,
        ManagedX64StubAuthoringKind strategy,
        int demonstratedFamilyCount,
        int generatorThreshold,
        bool generatorEligible,
        int out8ByteCount,
        int in8ByteCount,
        bool writeThenExecute,
        bool permanentRwx)
    {
        IsInitialized = true;
        Status = status;
        Strategy = strategy;
        DemonstratedFamilyCount = demonstratedFamilyCount;
        GeneratorThreshold = generatorThreshold;
        GeneratorEligible = generatorEligible;
        Out8ByteCount = out8ByteCount;
        In8ByteCount = in8ByteCount;
        WriteThenExecute = writeThenExecute;
        PermanentRwx = permanentRwx;
    }

    public bool IsInitialized { get; }

    public ManagedX64StubStrategyStatus Status { get; }

    public ManagedX64StubAuthoringKind Strategy { get; }

    public int DemonstratedFamilyCount { get; }

    public int GeneratorThreshold { get; }

    public bool GeneratorEligible { get; }

    public int Out8ByteCount { get; }

    public int In8ByteCount { get; }

    public bool WriteThenExecute { get; }

    public bool PermanentRwx { get; }

    public bool Succeeded
    {
        get
        {
            if (!IsInitialized ||
                Status != ManagedX64StubStrategyStatus.Success ||
                DemonstratedFamilyCount <= 0 ||
                GeneratorThreshold <
                    ManagedX64StubStrategyADRUseExistingAudited.GeneratorEligibilityThreshold ||
                Out8ByteCount != PortIoStubPlan.Out8RequiredByteLength ||
                In8ByteCount != PortIoStubPlan.In8RequiredByteLength ||
                !WriteThenExecute ||
                PermanentRwx)
            {
                return false;
            }

            bool expectedGeneratorEligibility =
                DemonstratedFamilyCount >= GeneratorThreshold;

            if (GeneratorEligible != expectedGeneratorEligibility)
            {
                return false;
            }

            if (expectedGeneratorEligibility)
            {
                return Strategy == ManagedX64StubAuthoringKind.BuildTimeGenerator;
            }

            return Strategy == ManagedX64StubAuthoringKind.ExistingAuditedBytePlans;
        }
    }

    internal static ManagedX64StubStrategyResult Success(
        ManagedX64StubAuthoringKind strategy,
        int demonstratedFamilyCount,
        int generatorThreshold,
        bool generatorEligible,
        int out8ByteCount,
        int in8ByteCount,
        bool writeThenExecute,
        bool permanentRwx)
    {
        return new ManagedX64StubStrategyResult(
            ManagedX64StubStrategyStatus.Success,
            strategy,
            demonstratedFamilyCount,
            generatorThreshold,
            generatorEligible,
            out8ByteCount,
            in8ByteCount,
            writeThenExecute,
            permanentRwx);
    }

    internal static ManagedX64StubStrategyResult Fail(
        ManagedX64StubStrategyStatus status,
        int demonstratedFamilyCount,
        int generatorThreshold,
        int out8ByteCount,
        int in8ByteCount,
        bool writeThenExecute,
        bool permanentRwx)
    {
        return new ManagedX64StubStrategyResult(
            status,
            ManagedX64StubAuthoringKind.None,
            demonstratedFamilyCount,
            generatorThreshold,
            generatorEligible: false,
            out8ByteCount,
            in8ByteCount,
            writeThenExecute,
            permanentRwx);
    }
}

public static class ManagedX64StubStrategyADRUseExistingAudited
{
    public const int CurrentDemonstratedFamilyCount = 1;
    public const int GeneratorEligibilityThreshold = 2;
    public const bool RequiresWriteThenExecute = true;
    public const bool AllowsPermanentRwx = false;

    public static ManagedX64StubStrategyResult ResolveCurrent()
    {
        return Evaluate(
            CurrentDemonstratedFamilyCount,
            GeneratorEligibilityThreshold,
            PortIoStubPlan.Out8RequiredByteLength,
            PortIoStubPlan.In8RequiredByteLength,
            RequiresWriteThenExecute,
            AllowsPermanentRwx);
    }

    public static ManagedX64StubStrategyResult Evaluate(
        int demonstratedFamilyCount,
        int generatorThreshold,
        int out8ByteCount,
        int in8ByteCount,
        bool writeThenExecute,
        bool permanentRwx)
    {
        ManagedX64StubStrategyStatus status = Validate(
            demonstratedFamilyCount,
            generatorThreshold,
            out8ByteCount,
            in8ByteCount,
            writeThenExecute,
            permanentRwx);

        if (status != ManagedX64StubStrategyStatus.Success)
        {
            return ManagedX64StubStrategyResult.Fail(
                status,
                demonstratedFamilyCount,
                generatorThreshold,
                out8ByteCount,
                in8ByteCount,
                writeThenExecute,
                permanentRwx);
        }

        bool generatorEligible = demonstratedFamilyCount >= generatorThreshold;
        ManagedX64StubAuthoringKind strategy = generatorEligible
            ? ManagedX64StubAuthoringKind.BuildTimeGenerator
            : ManagedX64StubAuthoringKind.ExistingAuditedBytePlans;

        return ManagedX64StubStrategyResult.Success(
            strategy,
            demonstratedFamilyCount,
            generatorThreshold,
            generatorEligible,
            out8ByteCount,
            in8ByteCount,
            writeThenExecute,
            permanentRwx);
    }

    public static ManagedX64StubStrategyStatus Validate(
        int demonstratedFamilyCount,
        int generatorThreshold,
        int out8ByteCount,
        int in8ByteCount,
        bool writeThenExecute,
        bool permanentRwx)
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

    private static bool HasExactAuditedBytePlanContract()
    {
        return PortIoStubPlan.Out8RequiredByteLength == 7 &&
            PortIoStubPlan.In8RequiredByteLength == 5 &&
            PortIoStubPlan.OperandSizeOverridePrefix == 0x66 &&
            PortIoStubPlan.MovR16ToRm16Opcode == 0x89 &&
            PortIoStubPlan.MovCxToDxModRm == 0xca &&
            PortIoStubPlan.MovR8ToRm8Opcode == 0x88 &&
            PortIoStubPlan.MovDlToAlModRm == 0xd0 &&
            PortIoOperationPlan.Out8Opcode == 0xee &&
            PortIoOperationPlan.In8Opcode == 0xec &&
            PortIoStubPlan.RetOpcode == 0xc3;
    }
}
