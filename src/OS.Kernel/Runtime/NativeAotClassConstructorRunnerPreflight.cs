using OS.Std.Text;

namespace OS.Kernel.Runtime;
public static class NativeAotClassConstructorRunnerPreflight
{
    public const bool ExecutesConstructors = false;
    public const bool UsesReflection = false;
    public const bool UsesExceptionControlFlow = false;
    public const bool RequiresTargetMetadata = true;
    public static NativeAotClassConstructorRunnerPreflightResult Validate(ReadOnlySpan<NativeAotClassConstructorStateEntry> stateEntries, int typeCount, int targetMetadataCount)
    {
        int occupiedCount = CountOccupied(stateEntries);
        if (stateEntries.Length == 0 || typeCount == 0 || targetMetadataCount == 0)
        {
            return Create(NativeAotClassConstructorRunnerPreflightStatus.NotInitialized, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length);
        }

        if (typeCount < 0 || targetMetadataCount < 0)
        {
            return Create(NativeAotClassConstructorRunnerPreflightStatus.InvalidTypeCount, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length);
        }

        if (typeCount > stateEntries.Length)
        {
            return Create(NativeAotClassConstructorRunnerPreflightStatus.TypeCountExceedsCapacity, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length);
        }

        if (targetMetadataCount < typeCount)
        {
            return Create(NativeAotClassConstructorRunnerPreflightStatus.MissingTargetMetadata, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length);
        }

        if (targetMetadataCount != typeCount)
        {
            return Create(NativeAotClassConstructorRunnerPreflightStatus.TargetMetadataCountMismatch, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length);
        }

        if (occupiedCount != typeCount)
        {
            return Create(NativeAotClassConstructorRunnerPreflightStatus.TypeCountMismatch, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length);
        }

        for (int index = 0; index < stateEntries.Length; index++)
        {
            NativeAotClassConstructorStateEntry entry = stateEntries[index];
            if (!entry.IsOccupied)
            {
                continue;
            }

            if (entry.State == NativeAotClassConstructorState.Initializing)
            {
                return Create(NativeAotClassConstructorRunnerPreflightStatus.PendingOrReentrantConstructor, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length, index, entry.State, entry.FailureCode);
            }

            if (entry.State == NativeAotClassConstructorState.Failed)
            {
                return Create(NativeAotClassConstructorRunnerPreflightStatus.FailedConstructor, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length, index, entry.State, entry.FailureCode);
            }

            if (entry.State != NativeAotClassConstructorState.Uninitialized && entry.State != NativeAotClassConstructorState.Initialized)
            {
                return Create(NativeAotClassConstructorRunnerPreflightStatus.InvalidState, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length, index, entry.State, entry.FailureCode);
            }
        }

        return Create(NativeAotClassConstructorRunnerPreflightStatus.Ready, typeCount, targetMetadataCount, occupiedCount, stateEntries.Length);
    }

    private static int CountOccupied(ReadOnlySpan<NativeAotClassConstructorStateEntry> entries)
    {
        int count = 0;
        for (int index = 0; index < entries.Length; index++)
        {
            if (entries[index].IsOccupied)
            {
                count++;
            }
        }

        return count;
    }

    private static NativeAotClassConstructorRunnerPreflightResult Create(NativeAotClassConstructorRunnerPreflightStatus status, int typeCount, int targetMetadataCount, int occupiedCount, int capacity, int firstProblemIndex = -1, NativeAotClassConstructorState firstProblemState = NativeAotClassConstructorState.Uninitialized, uint firstProblemFailureCode = 0)
    {
        return NativeAotClassConstructorRunnerPreflightResult.Create(status, typeCount, targetMetadataCount, occupiedCount, capacity, firstProblemIndex, firstProblemState, firstProblemFailureCode);
    }
}
