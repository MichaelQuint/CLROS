namespace OS.Kernel.Diagnostics;
public readonly struct GuardedIstStackLease
{
    internal GuardedIstStackLease(ulong ownerToken, ulong generation, ulong arenaBaseAddress, ulong requiredBytes)
    {
        IsInitialized = true;
        OwnerToken = ownerToken;
        Generation = generation;
        ArenaBaseAddress = arenaBaseAddress;
        RequiredBytes = requiredBytes;
    }

    public bool IsInitialized { get; }
    public ulong OwnerToken { get; }
    public ulong Generation { get; }
    public ulong ArenaBaseAddress { get; }
    public ulong RequiredBytes { get; }
    public bool IsUsable => IsInitialized && OwnerToken != 0 && Generation != 0 && ArenaBaseAddress != 0 && RequiredBytes != 0;
}
