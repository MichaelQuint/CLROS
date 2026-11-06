namespace OS.Kernel.Runtime;

public readonly struct NativeAotClassConstructorStateTableResult
{
    private NativeAotClassConstructorStateTableResult(bool isInitialized, NativeAotClassConstructorStateTableStatus status, NativeAotClassConstructorKey key, NativeAotClassConstructorState previousState, NativeAotClassConstructorState state, uint failureCode, int slotIndex, int duplicateSlotIndex, int occupiedCount, int capacity)
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

    public bool IsInitialized
    {
        get;
    }
    public NativeAotClassConstructorStateTableStatus Status
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeAotClassConstructorStateTableStatus.Success;
    public NativeAotClassConstructorKey Key
    {
        get;
    }
    public NativeAotClassConstructorState PreviousState
    {
        get;
    }
    public NativeAotClassConstructorState State
    {
        get;
    }
    public uint FailureCode
    {
        get;
    }
    public int SlotIndex
    {
        get;
    }
    public int DuplicateSlotIndex
    {
        get;
    }
    public int OccupiedCount
    {
        get;
    }
    public int Capacity
    {
        get;
    }

    internal static NativeAotClassConstructorStateTableResult Success(NativeAotClassConstructorKey key, NativeAotClassConstructorState previousState, NativeAotClassConstructorState state, uint failureCode, int slotIndex, int occupiedCount, int capacity)
    {
        return new NativeAotClassConstructorStateTableResult(isInitialized: true, NativeAotClassConstructorStateTableStatus.Success, key, previousState, state, failureCode, slotIndex, duplicateSlotIndex: -1, occupiedCount, capacity);
    }

    internal static NativeAotClassConstructorStateTableResult Fail(NativeAotClassConstructorStateTableStatus status, NativeAotClassConstructorKey key, NativeAotClassConstructorState previousState, NativeAotClassConstructorState state, uint failureCode, int slotIndex, int duplicateSlotIndex, int occupiedCount, int capacity)
    {
        return new NativeAotClassConstructorStateTableResult(isInitialized: true, status, key, previousState, state, failureCode, slotIndex, duplicateSlotIndex, occupiedCount, capacity);
    }
}
