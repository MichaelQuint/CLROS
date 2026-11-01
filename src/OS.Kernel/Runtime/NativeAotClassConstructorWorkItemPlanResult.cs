namespace OS.Kernel.Runtime;
public readonly struct NativeAotClassConstructorWorkItemPlanResult
{
    private const uint InitializedFlag = 1;
    private NativeAotClassConstructorWorkItemPlanResult(uint flags, NativeAotClassConstructorWorkItemPlanStatus status, NativeAotClassConstructorRunnerPreflightStatus preflightStatus, int typeCount, int targetMetadataCount, int plannedCount, int skippedInitializedCount, int requiredWorkItemCapacity, int availableWorkItemCapacity, int firstProblemIndex, NativeAotClassConstructorState firstProblemState, uint firstProblemFailureCode)
    {
        Flags = flags;
        Status = status;
        PreflightStatus = preflightStatus;
        TypeCount = typeCount;
        TargetMetadataCount = targetMetadataCount;
        PlannedCount = plannedCount;
        SkippedInitializedCount = skippedInitializedCount;
        RequiredWorkItemCapacity = requiredWorkItemCapacity;
        AvailableWorkItemCapacity = availableWorkItemCapacity;
        FirstProblemIndex = firstProblemIndex;
        FirstProblemState = firstProblemState;
        FirstProblemFailureCode = firstProblemFailureCode;
    }

    public uint Flags { get; }
    public NativeAotClassConstructorWorkItemPlanStatus Status { get; }
    public NativeAotClassConstructorRunnerPreflightStatus PreflightStatus { get; }
    public int TypeCount { get; }
    public int TargetMetadataCount { get; }
    public int PlannedCount { get; }
    public int SkippedInitializedCount { get; }
    public int RequiredWorkItemCapacity { get; }
    public int AvailableWorkItemCapacity { get; }
    public int FirstProblemIndex { get; }
    public NativeAotClassConstructorState FirstProblemState { get; }
    public uint FirstProblemFailureCode { get; }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == NativeAotClassConstructorWorkItemPlanStatus.Ready && PreflightStatus == NativeAotClassConstructorRunnerPreflightStatus.Ready && TypeCount > 0 && TargetMetadataCount == TypeCount && PlannedCount >= 0 && SkippedInitializedCount >= 0 && PlannedCount + SkippedInitializedCount == TypeCount && RequiredWorkItemCapacity == PlannedCount && AvailableWorkItemCapacity >= RequiredWorkItemCapacity && FirstProblemIndex == -1;

    internal static NativeAotClassConstructorWorkItemPlanResult Create(NativeAotClassConstructorWorkItemPlanStatus status, NativeAotClassConstructorRunnerPreflightStatus preflightStatus, int typeCount, int targetMetadataCount, int plannedCount, int skippedInitializedCount, int requiredWorkItemCapacity, int availableWorkItemCapacity, int firstProblemIndex = -1, NativeAotClassConstructorState firstProblemState = NativeAotClassConstructorState.Uninitialized, uint firstProblemFailureCode = 0)
    {
        return new NativeAotClassConstructorWorkItemPlanResult(InitializedFlag, status, preflightStatus, typeCount, targetMetadataCount, plannedCount, skippedInitializedCount, requiredWorkItemCapacity, availableWorkItemCapacity, firstProblemIndex, firstProblemState, firstProblemFailureCode);
    }
}
