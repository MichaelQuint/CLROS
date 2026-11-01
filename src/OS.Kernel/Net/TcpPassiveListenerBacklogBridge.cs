namespace OS.Net;
public static class TcpPassiveListenerBacklogBridge
{
    public const bool RequiresCallerOwnedBacklogStorage = true;
    public const bool ParsesPackets = false;
    public const bool ExportsSocketHandles = false;
    public const bool OwnsTimers = false;
    public const bool RetransmitsSegments = false;
    public const bool UsesVirtioNet = false;
    public static TcpPassiveListenerBacklogBridgeStatus EnqueueEstablished(Span<TcpListenerBacklogEntry> backlogEntries, TcpPassiveIpv4FramePlanResult passiveResult, out TcpPassiveListenerBacklogBridgeResult result)
    {
        if (!passiveResult.Succeeded)
        {
            result = TcpPassiveListenerBacklogBridgeResult.Create(TcpPassiveListenerBacklogBridgeStatus.PassiveOpenFailed, passiveResult, TcpListenerBacklogStatus.Success, CountBacklogEntries(backlogEntries), backlogEntries.Length, requiredCapacity: 1, TcpListenerBacklog.NoEntryIndex, default);
            return result.Status;
        }

        if (passiveResult.State != TcpPassiveOpenHandshakeState.Established)
        {
            result = TcpPassiveListenerBacklogBridgeResult.Create(TcpPassiveListenerBacklogBridgeStatus.NotEstablished, passiveResult, TcpListenerBacklogStatus.Success, CountBacklogEntries(backlogEntries), backlogEntries.Length, requiredCapacity: 1, TcpListenerBacklog.NoEntryIndex, default);
            return result.Status;
        }

        TcpListenerBacklogConnection connection = new TcpListenerBacklogConnection(passiveResult.LocalAddress, passiveResult.LocalPort, passiveResult.RemoteAddress, passiveResult.RemotePort, passiveResult.LocalNextSequenceNumber, passiveResult.RemoteNextSequenceNumber);
        TcpListenerBacklogResult backlogResult = TcpListenerBacklog.Enqueue(backlogEntries, connection);
        if (backlogResult.Status != TcpListenerBacklogStatus.Success)
        {
            result = TcpPassiveListenerBacklogBridgeResult.Create(TcpPassiveListenerBacklogBridgeStatus.BacklogEnqueueFailed, passiveResult, backlogResult.Status, backlogResult.Count, backlogResult.Capacity, backlogResult.RequiredCapacity, backlogResult.EntryIndex, backlogResult.Connection);
            return result.Status;
        }

        result = TcpPassiveListenerBacklogBridgeResult.Create(TcpPassiveListenerBacklogBridgeStatus.Success, passiveResult, backlogResult.Status, backlogResult.Count, backlogResult.Capacity, backlogResult.RequiredCapacity, backlogResult.EntryIndex, backlogResult.Connection);
        return result.Status;
    }

    private static int CountBacklogEntries(ReadOnlySpan<TcpListenerBacklogEntry> backlogEntries)
    {
        TcpListenerBacklogResult inspect = TcpListenerBacklog.Inspect(backlogEntries);
        return inspect.Status == TcpListenerBacklogStatus.Success ? inspect.Count : 0;
    }
}
