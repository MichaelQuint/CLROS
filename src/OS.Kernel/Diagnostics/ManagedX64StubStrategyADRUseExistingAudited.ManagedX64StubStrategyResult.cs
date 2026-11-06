namespace OS.Kernel.Diagnostics;

public readonly struct ManagedX64StubStrategyResult
{
    private ManagedX64StubStrategyResult(ManagedX64StubStrategyStatus status, ManagedX64StubAuthoringKind strategy, int demonstratedFamilyCount, int generatorThreshold, bool generatorEligible, int out8ByteCount, int in8ByteCount, bool writeThenExecute, bool permanentRwx)
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

    public bool IsInitialized
    {
        get;
    }
    public ManagedX64StubStrategyStatus Status
    {
        get;
    }
    public ManagedX64StubAuthoringKind Strategy
    {
        get;
    }
    public int DemonstratedFamilyCount
    {
        get;
    }
    public int GeneratorThreshold
    {
        get;
    }
    public bool GeneratorEligible
    {
        get;
    }
    public int Out8ByteCount
    {
        get;
    }
    public int In8ByteCount
    {
        get;
    }
    public bool WriteThenExecute
    {
        get;
    }
    public bool PermanentRwx
    {
        get;
    }

    public bool Succeeded
    {
        get
        {
            if (!IsInitialized || Status != ManagedX64StubStrategyStatus.Success || DemonstratedFamilyCount <= 0 || GeneratorThreshold < ManagedX64StubStrategyADRUseExistingAudited.GeneratorEligibilityThreshold || Out8ByteCount != PortIoStubPlan.Out8RequiredByteLength || In8ByteCount != PortIoStubPlan.In8RequiredByteLength || !WriteThenExecute || PermanentRwx)
            {
                return false;
            }

            bool expectedGeneratorEligibility = DemonstratedFamilyCount >= GeneratorThreshold;
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

    internal static ManagedX64StubStrategyResult Success(ManagedX64StubAuthoringKind strategy, int demonstratedFamilyCount, int generatorThreshold, bool generatorEligible, int out8ByteCount, int in8ByteCount, bool writeThenExecute, bool permanentRwx)
    {
        return new ManagedX64StubStrategyResult(ManagedX64StubStrategyStatus.Success, strategy, demonstratedFamilyCount, generatorThreshold, generatorEligible, out8ByteCount, in8ByteCount, writeThenExecute, permanentRwx);
    }

    internal static ManagedX64StubStrategyResult Fail(ManagedX64StubStrategyStatus status, int demonstratedFamilyCount, int generatorThreshold, int out8ByteCount, int in8ByteCount, bool writeThenExecute, bool permanentRwx)
    {
        return new ManagedX64StubStrategyResult(status, ManagedX64StubAuthoringKind.None, demonstratedFamilyCount, generatorThreshold, generatorEligible: false, out8ByteCount, in8ByteCount, writeThenExecute, permanentRwx);
    }
}
