namespace OS.Std;

public readonly struct NativeUIntOperationResult
{
    public NativeUIntOperationResult(NativeUIntOperationStatus status, nuint value)
    {
        Status = status;
        Value = value;
        IsInitialized = true;
    }

    public NativeUIntOperationStatus Status
    {
        get;
    }
    public nuint Value
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeUIntOperationStatus.Success;
}
