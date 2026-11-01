namespace OS.Kernel.Diagnostics;
public struct GuardedIstStackAllocatorState
{
    public bool IsInitialized { get; private set; }
    public ulong ArenaBaseAddress { get; private set; }
    public ulong ArenaSizeBytes { get; private set; }
    public ulong StackSizeBytes { get; private set; }
    public ulong GuardSizeBytes { get; private set; }
    public ulong RequiredBytes { get; private set; }
    public ulong OwnerToken { get; private set; }
    public ulong LastGeneration { get; private set; }
    public ulong ActiveGeneration { get; private set; }
    public bool HasActiveLease => IsInitialized && ActiveGeneration != 0;

    internal void Initialize(in GuardedIstStackArenaRequest request, ulong requiredBytes)
    {
        IsInitialized = true;
        ArenaBaseAddress = request.ArenaBaseAddress;
        ArenaSizeBytes = request.ArenaSizeBytes;
        StackSizeBytes = request.StackSizeBytes;
        GuardSizeBytes = request.GuardSizeBytes;
        RequiredBytes = requiredBytes;
        OwnerToken = request.OwnerToken;
        LastGeneration = 0;
        ActiveGeneration = 0;
    }

    internal void MarkAllocated(ulong generation)
    {
        LastGeneration = generation;
        ActiveGeneration = generation;
    }

    internal void MarkReleased()
    {
        ActiveGeneration = 0;
    }
}
