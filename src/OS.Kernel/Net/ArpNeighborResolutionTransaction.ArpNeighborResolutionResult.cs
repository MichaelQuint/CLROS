namespace OS.Net;

public readonly struct ArpNeighborResolutionResult
{
    private ArpNeighborResolutionResult(ArpNeighborResolutionStatus status, ArpNeighborResolutionState state, PacketParseStatus packetStatus, int bytesWritten, MacAddress resolvedMac)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        PacketStatus = packetStatus;
        BytesWritten = bytesWritten;
        ResolvedMac = resolvedMac;
    }

    public ArpNeighborResolutionStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == ArpNeighborResolutionStatus.Success;
    public ArpNeighborResolutionState State
    {
        get;
    }
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public MacAddress ResolvedMac
    {
        get;
    }

    public static ArpNeighborResolutionResult Success(ArpNeighborResolutionState state, int bytesWritten, MacAddress resolvedMac)
    {
        return new ArpNeighborResolutionResult(ArpNeighborResolutionStatus.Success, state, PacketParseStatus.Success, bytesWritten, resolvedMac);
    }

    public static ArpNeighborResolutionResult Fail(ArpNeighborResolutionStatus status, ArpNeighborResolutionState state, PacketParseStatus packetStatus)
    {
        return new ArpNeighborResolutionResult(status, state, packetStatus, 0, default);
    }
}
