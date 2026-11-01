namespace OS.Net;
public readonly struct ArpExpiryControlResult
{
    public readonly ArpExpiryControlStatus Status;
    public readonly uint Checks;
    public readonly ushort CacheCount, RequestBytes;
    internal ArpExpiryControlResult(ArpExpiryControlStatus status, uint checks, ushort count, ushort bytes)
    {
        Status = status;
        Checks = checks;
        CacheCount = count;
        RequestBytes = bytes;
    }

    public bool Succeeded => Status == ArpExpiryControlStatus.Success && Checks == ARPExpiryUnresolvedNeighborTimeoutCacheReplacementTests.CompleteChecks;
}
