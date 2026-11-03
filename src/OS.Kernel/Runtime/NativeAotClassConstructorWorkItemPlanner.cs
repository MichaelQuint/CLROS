namespace OS.Kernel.Runtime;

public static class NativeAotClassConstructorWorkItemPlanner
{
    public const bool UsesCallerOwnedStorage = true;
    public const bool ExecutesConstructors = false;
    public const bool UsesReflection = false;
    public const bool UsesExceptionControlFlow = false;
    public const bool UsesManagedDelegates = false;
    public const bool DiscoversMetadata = false;
    public static NativeAotClassConstructorWorkItemPlanResult Create(ReadOnlySpan<NativeAotClassConstructorStateEntry> stateEntries, NativeAotClassConstructorRunnerPreflightResult preflight, ReadOnlySpan<NativeAotClassConstructorTargetMetadata> targetMetadata, Span<NativeAotClassConstructorWorkItem> workItems)
    {
        if (!preflight.IsInitialized)
        {
            return NativeAotClassConstructorWorkItemPlanResult.Create(NativeAotClassConstructorWorkItemPlanStatus.NotInitialized, preflight.Status, preflight.TypeCount, preflight.TargetMetadataCount, plannedCount: 0, skippedInitializedCount: 0, requiredWorkItemCapacity: 0, availableWorkItemCapacity: workItems.Length, firstProblemIndex: 0, firstProblemState: preflight.FirstProblemState, firstProblemFailureCode: preflight.FirstProblemFailureCode);
        }

        if (!preflight.Succeeded)
        {
            return CreatePreflightFailure(preflight, workItems.Length);
        }

        if (preflight.TypeCount < 0 || preflight.TargetMetadataCount < 0)
        {
            return NativeAotClassConstructorWorkItemPlanResult.Create(NativeAotClassConstructorWorkItemPlanStatus.InvalidTypeCount, preflight.Status, preflight.TypeCount, preflight.TargetMetadataCount, plannedCount: 0, skippedInitializedCount: 0, requiredWorkItemCapacity: 0, availableWorkItemCapacity: workItems.Length);
        }

        if (targetMetadata.Length != preflight.TargetMetadataCount)
        {
            return NativeAotClassConstructorWorkItemPlanResult.Create(NativeAotClassConstructorWorkItemPlanStatus.TargetMetadataCountMismatch, preflight.Status, preflight.TypeCount, targetMetadata.Length, plannedCount: 0, skippedInitializedCount: 0, requiredWorkItemCapacity: 0, availableWorkItemCapacity: workItems.Length);
        }

        if (preflight.TypeCount > stateEntries.Length)
        {
            return NativeAotClassConstructorWorkItemPlanResult.Create(NativeAotClassConstructorWorkItemPlanStatus.TypeCountMismatch, preflight.Status, preflight.TypeCount, targetMetadata.Length, plannedCount: 0, skippedInitializedCount: 0, requiredWorkItemCapacity: 0, availableWorkItemCapacity: workItems.Length);
        }

        int plannedCount = 0;
        int skippedInitializedCount = 0;
        int occupiedCount = 0;
        for (int index = 0; index < stateEntries.Length; index++)
        {
            NativeAotClassConstructorStateEntry entry = stateEntries[index];
            if (!entry.IsOccupied)
            {
                continue;
            }

            occupiedCount++;
            if (entry.State == NativeAotClassConstructorState.Initialized)
            {
                skippedInitializedCount++;
                continue;
            }

            if (entry.State == NativeAotClassConstructorState.Initializing)
            {
                return CreateProblem(NativeAotClassConstructorWorkItemPlanStatus.PendingOrReentrantConstructor, preflight, targetMetadata.Length, plannedCount, skippedInitializedCount, requiredWorkItemCapacity: plannedCount, workItems.Length, index, entry.State, entry.FailureCode);
            }

            if (entry.State == NativeAotClassConstructorState.Failed)
            {
                return CreateProblem(NativeAotClassConstructorWorkItemPlanStatus.FailedConstructor, preflight, targetMetadata.Length, plannedCount, skippedInitializedCount, requiredWorkItemCapacity: plannedCount, workItems.Length, index, entry.State, entry.FailureCode);
            }

            if (entry.State != NativeAotClassConstructorState.Uninitialized)
            {
                return CreateProblem(NativeAotClassConstructorWorkItemPlanStatus.InvalidState, preflight, targetMetadata.Length, plannedCount, skippedInitializedCount, requiredWorkItemCapacity: plannedCount, workItems.Length, index, entry.State, entry.FailureCode);
            }

            NativeAotClassConstructorTargetMetadata target = FindTarget(targetMetadata, entry.Key);
            if (!target.IsValid)
            {
                return CreateProblem(target.Key.IsValid ? NativeAotClassConstructorWorkItemPlanStatus.MissingTargetToken : NativeAotClassConstructorWorkItemPlanStatus.MissingTargetMetadata, preflight, targetMetadata.Length, plannedCount, skippedInitializedCount, requiredWorkItemCapacity: plannedCount + 1, workItems.Length, index, entry.State, entry.FailureCode);
            }

            if (plannedCount >= workItems.Length)
            {
                return CreateProblem(NativeAotClassConstructorWorkItemPlanStatus.DestinationTooSmall, preflight, targetMetadata.Length, plannedCount, skippedInitializedCount, requiredWorkItemCapacity: plannedCount + 1, workItems.Length, index, entry.State, entry.FailureCode);
            }

            workItems[plannedCount] = new NativeAotClassConstructorWorkItem(entry.Key, target.TargetToken, index);
            plannedCount++;
        }

        if (occupiedCount != preflight.TypeCount)
        {
            return NativeAotClassConstructorWorkItemPlanResult.Create(NativeAotClassConstructorWorkItemPlanStatus.TypeCountMismatch, preflight.Status, preflight.TypeCount, targetMetadata.Length, plannedCount, skippedInitializedCount, requiredWorkItemCapacity: plannedCount, availableWorkItemCapacity: workItems.Length);
        }

        return NativeAotClassConstructorWorkItemPlanResult.Create(NativeAotClassConstructorWorkItemPlanStatus.Ready, preflight.Status, preflight.TypeCount, targetMetadata.Length, plannedCount, skippedInitializedCount, requiredWorkItemCapacity: plannedCount, availableWorkItemCapacity: workItems.Length);
    }

    private static NativeAotClassConstructorTargetMetadata FindTarget(ReadOnlySpan<NativeAotClassConstructorTargetMetadata> targetMetadata, NativeAotClassConstructorKey key)
    {
        for (int index = 0; index < targetMetadata.Length; index++)
        {
            NativeAotClassConstructorTargetMetadata target = targetMetadata[index];
            if (target.Key.Equals(key))
            {
                return target;
            }
        }

        return default;
    }

    private static NativeAotClassConstructorWorkItemPlanResult CreatePreflightFailure(NativeAotClassConstructorRunnerPreflightResult preflight, int availableWorkItemCapacity)
    {
        NativeAotClassConstructorWorkItemPlanStatus status = preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.PendingOrReentrantConstructor ? NativeAotClassConstructorWorkItemPlanStatus.PendingOrReentrantConstructor : preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.FailedConstructor ? NativeAotClassConstructorWorkItemPlanStatus.FailedConstructor : preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.InvalidTypeCount ? NativeAotClassConstructorWorkItemPlanStatus.InvalidTypeCount : preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.TypeCountMismatch ? NativeAotClassConstructorWorkItemPlanStatus.TypeCountMismatch : preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.MissingTargetMetadata ? NativeAotClassConstructorWorkItemPlanStatus.MissingTargetMetadata : preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.TargetMetadataCountMismatch ? NativeAotClassConstructorWorkItemPlanStatus.TargetMetadataCountMismatch : preflight.Status == NativeAotClassConstructorRunnerPreflightStatus.InvalidState ? NativeAotClassConstructorWorkItemPlanStatus.InvalidState : NativeAotClassConstructorWorkItemPlanStatus.PreflightNotReady;
        return NativeAotClassConstructorWorkItemPlanResult.Create(status, preflight.Status, preflight.TypeCount, preflight.TargetMetadataCount, plannedCount: 0, skippedInitializedCount: 0, requiredWorkItemCapacity: 0, availableWorkItemCapacity, preflight.FirstProblemIndex, preflight.FirstProblemState, preflight.FirstProblemFailureCode);
    }

    private static NativeAotClassConstructorWorkItemPlanResult CreateProblem(NativeAotClassConstructorWorkItemPlanStatus status, NativeAotClassConstructorRunnerPreflightResult preflight, int targetMetadataCount, int plannedCount, int skippedInitializedCount, int requiredWorkItemCapacity, int availableWorkItemCapacity, int firstProblemIndex, NativeAotClassConstructorState firstProblemState, uint firstProblemFailureCode)
    {
        return NativeAotClassConstructorWorkItemPlanResult.Create(status, preflight.Status, preflight.TypeCount, targetMetadataCount, plannedCount, skippedInitializedCount, requiredWorkItemCapacity, availableWorkItemCapacity, firstProblemIndex, firstProblemState, firstProblemFailureCode);
    }
}
