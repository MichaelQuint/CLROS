namespace OS.Kernel.Runtime;
public readonly struct NativeAotClassConstructorWorkItem
{
    public NativeAotClassConstructorWorkItem(NativeAotClassConstructorKey key, nuint targetToken, int stateSlotIndex)
    {
        Key = key;
        TargetToken = targetToken;
        StateSlotIndex = stateSlotIndex;
    }

    public NativeAotClassConstructorKey Key { get; }
    public nuint TargetToken { get; }
    public int StateSlotIndex { get; }
    public bool IsValid => Key.IsValid && TargetToken != 0 && StateSlotIndex >= 0;
}
