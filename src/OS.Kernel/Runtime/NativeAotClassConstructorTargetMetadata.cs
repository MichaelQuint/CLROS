namespace OS.Kernel.Runtime;

public readonly struct NativeAotClassConstructorTargetMetadata
{
    public NativeAotClassConstructorTargetMetadata(NativeAotClassConstructorKey key, nuint targetToken)
    {
        Key = key;
        TargetToken = targetToken;
    }

    public NativeAotClassConstructorKey Key
    {
        get;
    }
    public nuint TargetToken
    {
        get;
    }
    public bool IsValid => Key.IsValid && TargetToken != 0;
}
