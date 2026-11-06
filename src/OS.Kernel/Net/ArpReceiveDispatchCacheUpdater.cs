namespace OS.Net;

public static class ArpReceiveDispatchCacheUpdater
{
    public const bool RequiresCallerOwnedCacheStorage = true;
    public const bool ReadsNic = false;
    public const bool PollsNic = false;
    public const bool AllocatesCacheStorage = false;
    public const bool DispatchesSockets = false;
    public const bool RoutesPackets = false;
    public static ArpReceiveDispatchCacheUpdateStatus TryUpdateFromDispatch(ref ArpNeighborCache cache, MacAddress localMac, Ipv4Address localAddress, Ipv4Address targetAddress, ReadOnlySpan<byte> receiveBuffer, NetworkReceiveDispatchResult dispatch, ulong currentTick, out ArpReceiveDispatchCacheUpdateResult result)
    {
        if (!dispatch.Succeeded)
        {
            result = ArpReceiveDispatchCacheUpdateResult.Fail(ArpReceiveDispatchCacheUpdateStatus.DispatchFailed, dispatch.Status, ArpNeighborResolutionStatus.FrameParseFailed, ArpNeighborCacheStatus.Success, dispatch.PayloadPacketStatus, ArpNeighborResolutionState.Initial, default, cache.Count, cache.Capacity);
            return result.Status;
        }

        if (dispatch.PayloadKind != EthernetPayloadKind.Arp)
        {
            result = ArpReceiveDispatchCacheUpdateResult.Fail(ArpReceiveDispatchCacheUpdateStatus.NonArpDispatch, dispatch.Status, ArpNeighborResolutionStatus.UnsupportedEtherType, ArpNeighborCacheStatus.Success, PacketParseStatus.UnsupportedEtherType, ArpNeighborResolutionState.Initial, default, cache.Count, cache.Capacity);
            return result.Status;
        }

        if (!IsPayloadRangeValid(receiveBuffer.Length, dispatch.PayloadOffset, dispatch.PayloadLength))
        {
            result = ArpReceiveDispatchCacheUpdateResult.Fail(ArpReceiveDispatchCacheUpdateStatus.PayloadRangeInvalid, dispatch.Status, ArpNeighborResolutionStatus.FrameParseFailed, ArpNeighborCacheStatus.Success, PacketParseStatus.BufferTooSmall, ArpNeighborResolutionState.Initial, default, cache.Count, cache.Capacity);
            return result.Status;
        }

        ArpNeighborResolutionStatus arpStatus = ValidateReply(dispatch, localMac, localAddress, targetAddress, out MacAddress resolvedMac);
        if (arpStatus != ArpNeighborResolutionStatus.Success)
        {
            result = ArpReceiveDispatchCacheUpdateResult.Fail(ArpReceiveDispatchCacheUpdateStatus.ArpValidationFailed, dispatch.Status, arpStatus, ArpNeighborCacheStatus.Success, PacketParseStatus.Success, ArpNeighborResolutionState.Failed, default, cache.Count, cache.Capacity);
            return result.Status;
        }

        ArpNeighborCacheStatus cacheStatus = cache.InsertOrUpdate(targetAddress, resolvedMac, currentTick, out ArpNeighborCacheMutationResult cacheResult);
        if (cacheStatus != ArpNeighborCacheStatus.Success)
        {
            result = ArpReceiveDispatchCacheUpdateResult.Fail(ArpReceiveDispatchCacheUpdateStatus.CacheUpdateFailed, dispatch.Status, arpStatus, cacheStatus, PacketParseStatus.Success, ArpNeighborResolutionState.Resolved, resolvedMac, cacheResult.Count, cacheResult.Capacity);
            return result.Status;
        }

        result = ArpReceiveDispatchCacheUpdateResult.Success(dispatch.Status, arpStatus, cacheStatus, PacketParseStatus.Success, ArpNeighborResolutionState.Resolved, resolvedMac, cacheResult.Count, cacheResult.Capacity, cacheResult.UpdatedExisting);
        return result.Status;
    }

    private static bool IsPayloadRangeValid(int receiveBufferLength, int payloadOffset, int payloadLength)
    {
        if (payloadOffset < 0 || payloadLength < ArpPacket.PacketLength)
        {
            return false;
        }

        return payloadOffset <= receiveBufferLength && payloadLength <= receiveBufferLength - payloadOffset;
    }

    private static ArpNeighborResolutionStatus ValidateReply(NetworkReceiveDispatchResult dispatch, MacAddress localMac, Ipv4Address localAddress, Ipv4Address targetAddress, out MacAddress resolvedMac)
    {
        ArpPacket reply = dispatch.ArpPacket;
        resolvedMac = default;
        if (reply.Operation != ArpPacket.OperationReply)
        {
            return ArpNeighborResolutionStatus.UnexpectedOperation;
        }

        if (reply.SenderAddress != targetAddress)
        {
            return ArpNeighborResolutionStatus.SenderAddressMismatch;
        }

        if (reply.TargetAddress != localAddress)
        {
            return ArpNeighborResolutionStatus.TargetAddressMismatch;
        }

        if (reply.TargetMac != localMac)
        {
            return ArpNeighborResolutionStatus.TargetMacMismatch;
        }

        if (dispatch.EthernetHeader.Destination != localMac)
        {
            return ArpNeighborResolutionStatus.EthernetDestinationMismatch;
        }

        if (dispatch.EthernetHeader.Source != reply.SenderMac)
        {
            return ArpNeighborResolutionStatus.EthernetSourceMismatch;
        }

        resolvedMac = reply.SenderMac;
        return ArpNeighborResolutionStatus.Success;
    }
}
