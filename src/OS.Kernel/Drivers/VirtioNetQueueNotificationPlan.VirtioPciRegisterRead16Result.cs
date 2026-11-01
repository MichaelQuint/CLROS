namespace OS.Drivers;
public readonly struct VirtioPciRegisterRead16Result
{
    public VirtioPciRegisterRead16Result(VirtioPciRegisterReadStatus status, ushort value)
    {
        Status = status;
        Value = value;
        IsInitialized = true;
    }

    public VirtioPciRegisterReadStatus Status { get; }
    public ushort Value { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioPciRegisterReadStatus.Success;
}
