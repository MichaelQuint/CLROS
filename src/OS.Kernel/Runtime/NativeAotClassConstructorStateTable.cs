namespace OS.Kernel.Runtime;

public enum NativeAotClassConstructorState
{
    Uninitialized = 0,
    Initializing = 1,
    Initialized = 2,
    Failed = 3
}

public enum NativeAotClassConstructorStateTableStatus
{
    Success = 0,
    InvalidKey = 1,
    MissingKey = 2,
    DuplicateKey = 3,
    CapacityExceeded = 4,
    ReentryCycle = 5,
    PreviouslyFailed = 6,
    AlreadyInitialized = 7,
    NotInitializing = 8,
    FailureCodeRequired = 9,
    InvalidState = 10
}

public readonly struct NativeAotClassConstructorKey
{
    public NativeAotClassConstructorKey(uint moduleId, uint typeId)
    {
        ModuleId = moduleId;
        TypeId = typeId;
    }

    public uint ModuleId { get; }

    public uint TypeId { get; }

    public bool IsValid => ModuleId != 0 && TypeId != 0;

    public bool Equals(NativeAotClassConstructorKey other)
    {
        return ModuleId == other.ModuleId && TypeId == other.TypeId;
    }
}

public readonly struct NativeAotClassConstructorStateEntry
{
    private NativeAotClassConstructorStateEntry(
        bool isOccupied,
        NativeAotClassConstructorKey key,
        NativeAotClassConstructorState state,
        uint failureCode,
        uint transitionCount)
    {
        IsOccupied = isOccupied;
        Key = key;
        State = state;
        FailureCode = failureCode;
        TransitionCount = transitionCount;
    }

    public bool IsOccupied { get; }

    public NativeAotClassConstructorKey Key { get; }

    public NativeAotClassConstructorState State { get; }

    public uint FailureCode { get; }

    public uint TransitionCount { get; }

    public static NativeAotClassConstructorStateEntry Occupied(
        NativeAotClassConstructorKey key,
        NativeAotClassConstructorState state,
        uint failureCode,
        uint transitionCount)
    {
        return new NativeAotClassConstructorStateEntry(
            key.IsValid,
            key,
            state,
            failureCode,
            transitionCount);
    }

    internal NativeAotClassConstructorStateEntry WithState(
        NativeAotClassConstructorState state,
        uint failureCode)
    {
        return new NativeAotClassConstructorStateEntry(
            IsOccupied,
            Key,
            state,
            failureCode,
            IncrementTransitionCount(TransitionCount));
    }

    private static uint IncrementTransitionCount(uint value)
    {
        return value == uint.MaxValue ? uint.MaxValue : value + 1;
    }
}

public readonly struct NativeAotClassConstructorStateTableResult
{
    private NativeAotClassConstructorStateTableResult(
        bool isInitialized,
        NativeAotClassConstructorStateTableStatus status,
        NativeAotClassConstructorKey key,
        NativeAotClassConstructorState previousState,
        NativeAotClassConstructorState state,
        uint failureCode,
        int slotIndex,
        int duplicateSlotIndex,
        int occupiedCount,
        int capacity)
    {
        IsInitialized = isInitialized;
        Status = status;
        Key = key;
        PreviousState = previousState;
        State = state;
        FailureCode = failureCode;
        SlotIndex = slotIndex;
        DuplicateSlotIndex = duplicateSlotIndex;
        OccupiedCount = occupiedCount;
        Capacity = capacity;
    }

    public bool IsInitialized { get; }

    public NativeAotClassConstructorStateTableStatus Status { get; }

    public bool Succeeded => IsInitialized && Status == NativeAotClassConstructorStateTableStatus.Success;

    public NativeAotClassConstructorKey Key { get; }

    public NativeAotClassConstructorState PreviousState { get; }

    public NativeAotClassConstructorState State { get; }

    public uint FailureCode { get; }

    public int SlotIndex { get; }

    public int DuplicateSlotIndex { get; }

    public int OccupiedCount { get; }

    public int Capacity { get; }

    internal static NativeAotClassConstructorStateTableResult Success(
        NativeAotClassConstructorKey key,
        NativeAotClassConstructorState previousState,
        NativeAotClassConstructorState state,
        uint failureCode,
        int slotIndex,
        int occupiedCount,
        int capacity)
    {
        return new NativeAotClassConstructorStateTableResult(
            isInitialized: true,
            NativeAotClassConstructorStateTableStatus.Success,
            key,
            previousState,
            state,
            failureCode,
            slotIndex,
            duplicateSlotIndex: -1,
            occupiedCount,
            capacity);
    }

    internal static NativeAotClassConstructorStateTableResult Fail(
        NativeAotClassConstructorStateTableStatus status,
        NativeAotClassConstructorKey key,
        NativeAotClassConstructorState previousState,
        NativeAotClassConstructorState state,
        uint failureCode,
        int slotIndex,
        int duplicateSlotIndex,
        int occupiedCount,
        int capacity)
    {
        return new NativeAotClassConstructorStateTableResult(
            isInitialized: true,
            status,
            key,
            previousState,
            state,
            failureCode,
            slotIndex,
            duplicateSlotIndex,
            occupiedCount,
            capacity);
    }
}

public static class NativeAotClassConstructorStateTable
{
    public const bool UsesCallerOwnedStorage = true;
    public const bool ExecutesConstructors = false;
    public const bool UsesReflection = false;
    public const bool UsesExceptionControlFlow = false;

    public static NativeAotClassConstructorStateTableResult Register(
        Span<NativeAotClassConstructorStateEntry> entries,
        NativeAotClassConstructorKey key)
    {
        NativeAotClassConstructorStateTableResult validation = ValidateKey(key, entries.Length);
        if (!validation.IsInitialized || !validation.Succeeded)
        {
            return validation;
        }

        FindKey(entries, key, out int slotIndex, out int duplicateSlotIndex, out int firstFreeSlot, out int occupiedCount);
        if (slotIndex >= 0)
        {
            return NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.DuplicateKey,
                key,
                entries[slotIndex].State,
                entries[slotIndex].State,
                entries[slotIndex].FailureCode,
                slotIndex,
                duplicateSlotIndex,
                occupiedCount,
                entries.Length);
        }

        if (firstFreeSlot < 0)
        {
            return NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.CapacityExceeded,
                key,
                NativeAotClassConstructorState.Uninitialized,
                NativeAotClassConstructorState.Uninitialized,
                failureCode: 0,
                slotIndex: -1,
                duplicateSlotIndex: -1,
                occupiedCount,
                entries.Length);
        }

        entries[firstFreeSlot] = NativeAotClassConstructorStateEntry.Occupied(
            key,
            NativeAotClassConstructorState.Uninitialized,
            failureCode: 0,
            transitionCount: 0);

        return NativeAotClassConstructorStateTableResult.Success(
            key,
            NativeAotClassConstructorState.Uninitialized,
            NativeAotClassConstructorState.Uninitialized,
            failureCode: 0,
            firstFreeSlot,
            occupiedCount + 1,
            entries.Length);
    }

    public static NativeAotClassConstructorStateTableResult TryBegin(
        Span<NativeAotClassConstructorStateEntry> entries,
        NativeAotClassConstructorKey key)
    {
        NativeAotClassConstructorStateTableResult found = FindRequired(entries, key);
        if (!found.Succeeded)
        {
            return found;
        }

        NativeAotClassConstructorStateEntry entry = entries[found.SlotIndex];
        return entry.State switch
        {
            NativeAotClassConstructorState.Uninitialized => Transition(
                entries,
                found.SlotIndex,
                NativeAotClassConstructorState.Initializing,
                failureCode: 0,
                found.OccupiedCount),
            NativeAotClassConstructorState.Initializing => NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.ReentryCycle,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                found.SlotIndex,
                duplicateSlotIndex: -1,
                found.OccupiedCount,
                entries.Length),
            NativeAotClassConstructorState.Initialized => NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.AlreadyInitialized,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                found.SlotIndex,
                duplicateSlotIndex: -1,
                found.OccupiedCount,
                entries.Length),
            NativeAotClassConstructorState.Failed => NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.PreviouslyFailed,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                found.SlotIndex,
                duplicateSlotIndex: -1,
                found.OccupiedCount,
                entries.Length),
            _ => InvalidState(key, entry, found.SlotIndex, found.OccupiedCount, entries.Length)
        };
    }

    public static NativeAotClassConstructorStateTableResult MarkInitialized(
        Span<NativeAotClassConstructorStateEntry> entries,
        NativeAotClassConstructorKey key)
    {
        NativeAotClassConstructorStateTableResult found = FindRequired(entries, key);
        if (!found.Succeeded)
        {
            return found;
        }

        NativeAotClassConstructorStateEntry entry = entries[found.SlotIndex];
        return entry.State switch
        {
            NativeAotClassConstructorState.Initializing => Transition(
                entries,
                found.SlotIndex,
                NativeAotClassConstructorState.Initialized,
                failureCode: 0,
                found.OccupiedCount),
            NativeAotClassConstructorState.Failed => NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.PreviouslyFailed,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                found.SlotIndex,
                duplicateSlotIndex: -1,
                found.OccupiedCount,
                entries.Length),
            NativeAotClassConstructorState.Initialized => NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.AlreadyInitialized,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                found.SlotIndex,
                duplicateSlotIndex: -1,
                found.OccupiedCount,
                entries.Length),
            NativeAotClassConstructorState.Uninitialized => NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.NotInitializing,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                found.SlotIndex,
                duplicateSlotIndex: -1,
                found.OccupiedCount,
                entries.Length),
            _ => InvalidState(key, entry, found.SlotIndex, found.OccupiedCount, entries.Length)
        };
    }

    public static NativeAotClassConstructorStateTableResult MarkFailed(
        Span<NativeAotClassConstructorStateEntry> entries,
        NativeAotClassConstructorKey key,
        uint failureCode)
    {
        NativeAotClassConstructorStateTableResult found = FindRequired(entries, key);
        if (!found.Succeeded)
        {
            return found;
        }

        NativeAotClassConstructorStateEntry entry = entries[found.SlotIndex];
        return entry.State switch
        {
            NativeAotClassConstructorState.Initializing when failureCode == 0 =>
                NativeAotClassConstructorStateTableResult.Fail(
                    NativeAotClassConstructorStateTableStatus.FailureCodeRequired,
                    key,
                    entry.State,
                    entry.State,
                    entry.FailureCode,
                    found.SlotIndex,
                    duplicateSlotIndex: -1,
                    found.OccupiedCount,
                    entries.Length),
            NativeAotClassConstructorState.Initializing => Transition(
                entries,
                found.SlotIndex,
                NativeAotClassConstructorState.Failed,
                failureCode,
                found.OccupiedCount),
            NativeAotClassConstructorState.Failed => NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.PreviouslyFailed,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                found.SlotIndex,
                duplicateSlotIndex: -1,
                found.OccupiedCount,
                entries.Length),
            NativeAotClassConstructorState.Initialized => NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.AlreadyInitialized,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                found.SlotIndex,
                duplicateSlotIndex: -1,
                found.OccupiedCount,
                entries.Length),
            NativeAotClassConstructorState.Uninitialized => NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.NotInitializing,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                found.SlotIndex,
                duplicateSlotIndex: -1,
                found.OccupiedCount,
                entries.Length),
            _ => InvalidState(key, entry, found.SlotIndex, found.OccupiedCount, entries.Length)
        };
    }

    public static NativeAotClassConstructorStateTableResult TryGet(
        ReadOnlySpan<NativeAotClassConstructorStateEntry> entries,
        NativeAotClassConstructorKey key)
    {
        return FindRequired(entries, key);
    }

    private static NativeAotClassConstructorStateTableResult FindRequired(
        ReadOnlySpan<NativeAotClassConstructorStateEntry> entries,
        NativeAotClassConstructorKey key)
    {
        NativeAotClassConstructorStateTableResult validation = ValidateKey(key, entries.Length);
        if (!validation.IsInitialized || !validation.Succeeded)
        {
            return validation;
        }

        FindKey(entries, key, out int slotIndex, out int duplicateSlotIndex, out _, out int occupiedCount);
        if (slotIndex < 0)
        {
            return NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.MissingKey,
                key,
                NativeAotClassConstructorState.Uninitialized,
                NativeAotClassConstructorState.Uninitialized,
                failureCode: 0,
                slotIndex: -1,
                duplicateSlotIndex: -1,
                occupiedCount,
                entries.Length);
        }

        NativeAotClassConstructorStateEntry entry = entries[slotIndex];
        if (duplicateSlotIndex >= 0)
        {
            return NativeAotClassConstructorStateTableResult.Fail(
                NativeAotClassConstructorStateTableStatus.DuplicateKey,
                key,
                entry.State,
                entry.State,
                entry.FailureCode,
                slotIndex,
                duplicateSlotIndex,
                occupiedCount,
                entries.Length);
        }

        return NativeAotClassConstructorStateTableResult.Success(
            key,
            entry.State,
            entry.State,
            entry.FailureCode,
            slotIndex,
            occupiedCount,
            entries.Length);
    }

    private static NativeAotClassConstructorStateTableResult ValidateKey(
        NativeAotClassConstructorKey key,
        int capacity)
    {
        if (key.IsValid)
        {
            return NativeAotClassConstructorStateTableResult.Success(
                key,
                NativeAotClassConstructorState.Uninitialized,
                NativeAotClassConstructorState.Uninitialized,
                failureCode: 0,
                slotIndex: -1,
                occupiedCount: 0,
                capacity);
        }

        return NativeAotClassConstructorStateTableResult.Fail(
            NativeAotClassConstructorStateTableStatus.InvalidKey,
            key,
            NativeAotClassConstructorState.Uninitialized,
            NativeAotClassConstructorState.Uninitialized,
            failureCode: 0,
            slotIndex: -1,
            duplicateSlotIndex: -1,
            occupiedCount: 0,
            capacity);
    }

    private static NativeAotClassConstructorStateTableResult Transition(
        Span<NativeAotClassConstructorStateEntry> entries,
        int slotIndex,
        NativeAotClassConstructorState state,
        uint failureCode,
        int occupiedCount)
    {
        NativeAotClassConstructorStateEntry entry = entries[slotIndex];
        NativeAotClassConstructorState previousState = entry.State;
        entries[slotIndex] = entry.WithState(state, failureCode);

        return NativeAotClassConstructorStateTableResult.Success(
            entry.Key,
            previousState,
            state,
            failureCode,
            slotIndex,
            occupiedCount,
            entries.Length);
    }

    private static NativeAotClassConstructorStateTableResult InvalidState(
        NativeAotClassConstructorKey key,
        NativeAotClassConstructorStateEntry entry,
        int slotIndex,
        int occupiedCount,
        int capacity)
    {
        return NativeAotClassConstructorStateTableResult.Fail(
            NativeAotClassConstructorStateTableStatus.InvalidState,
            key,
            entry.State,
            entry.State,
            entry.FailureCode,
            slotIndex,
            duplicateSlotIndex: -1,
            occupiedCount,
            capacity);
    }

    private static void FindKey(
        ReadOnlySpan<NativeAotClassConstructorStateEntry> entries,
        NativeAotClassConstructorKey key,
        out int slotIndex,
        out int duplicateSlotIndex,
        out int firstFreeSlot,
        out int occupiedCount)
    {
        slotIndex = -1;
        duplicateSlotIndex = -1;
        firstFreeSlot = -1;
        occupiedCount = 0;

        for (int index = 0; index < entries.Length; index++)
        {
            NativeAotClassConstructorStateEntry entry = entries[index];
            if (!entry.IsOccupied)
            {
                if (firstFreeSlot < 0)
                {
                    firstFreeSlot = index;
                }

                continue;
            }

            occupiedCount++;
            if (!entry.Key.Equals(key))
            {
                continue;
            }

            if (slotIndex < 0)
            {
                slotIndex = index;
            }
            else if (duplicateSlotIndex < 0)
            {
                duplicateSlotIndex = index;
            }
        }
    }
}
