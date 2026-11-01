namespace OS.Net;
public readonly struct ArpNeighborCacheEntry
{
    private ArpNeighborCacheEntry(Ipv4Address address, MacAddress macAddress, ArpNeighborCacheEntryState state, ulong lastUpdatedTick)
    {
        Address = address;
        MacAddress = macAddress;
        State = state;
        LastUpdatedTick = lastUpdatedTick;
    }

    public Ipv4Address Address { get; }
    public MacAddress MacAddress { get; }
    public ArpNeighborCacheEntryState State { get; }
    public ulong LastUpdatedTick { get; }
    public bool IsActive => State != ArpNeighborCacheEntryState.Empty;

    public static ArpNeighborCacheEntry Resolved(Ipv4Address address, MacAddress macAddress, ulong lastUpdatedTick)
    {
        return new ArpNeighborCacheEntry(address, macAddress, ArpNeighborCacheEntryState.Resolved, lastUpdatedTick);
    }
}
