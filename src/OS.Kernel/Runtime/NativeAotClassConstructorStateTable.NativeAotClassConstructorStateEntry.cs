namespace OS.Kernel.Runtime;
public readonly struct NativeAotClassConstructorStateEntry
{
    private NativeAotClassConstructorStateEntry(bool isOccupied, NativeAotClassConstructorKey key, NativeAotClassConstructorState state, uint failureCode, uint transitionCount)
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

    public static NativeAotClassConstructorStateEntry Occupied(NativeAotClassConstructorKey key, NativeAotClassConstructorState state, uint failureCode, uint transitionCount)
    {
        return new NativeAotClassConstructorStateEntry(key.IsValid, key, state, failureCode, transitionCount);
    }

    internal NativeAotClassConstructorStateEntry WithState(NativeAotClassConstructorState state, uint failureCode)
    {
        return new NativeAotClassConstructorStateEntry(IsOccupied, Key, state, failureCode, IncrementTransitionCount(TransitionCount));
    }

    private static uint IncrementTransitionCount(uint value)
    {
        return value == uint.MaxValue ? uint.MaxValue : value + 1;
    }
}
