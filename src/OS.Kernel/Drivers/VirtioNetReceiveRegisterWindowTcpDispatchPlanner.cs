namespace OS.Drivers;

using OS.Net;

public static class VirtioNetReceiveRegisterWindowTcpDispatchPlanner
{
    public const bool RequiresSuccessfulRegisterWindowReadiness = true;
    public const bool UsesReceiveRegisterWindowDispatchPlanner = true;
    public const bool UsesTcpReceiveDispatcher = true;
    public const bool SelectsCallerOwnedReceiveBufferFromSlab = true;
    public const bool UsesSelectedCallerOwnedReceiveBufferSlice = true;
    public const bool PreservesReceiveDispatchMetadata = true;
    public const bool PreservesSlabCompletionMetadata = true;
    public const bool PreservesNetworkDispatchMetadata = true;
    public const bool PreservesTcpDispatchMetadata = true;
    public const bool ResultLayoutIsPrimitiveOnly = true;
    public const bool PerformsLiveMmio = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketReceive = false;
    public const bool OpensSockets = false;
    public const bool StartsKestrel = false;
    public const bool DispatchesSockets = false;
    public const bool MutatesTcpControlBlocks = true;
    public static VirtioNetReceiveRegisterWindowTcpDispatchResult DispatchTcp(VirtioNetReceiveReadinessResult readiness, ReadOnlySpan<byte> queueMemory, ReadOnlySpan<byte> receiveBufferSlab, ref TcpEstablishedStream stream, ref TcpConnectionClose close, Span<byte> passiveCloseAckSegment)
    {
        VirtioNetReceiveRegisterWindowDispatchResult receiveDispatch = VirtioNetReceiveRegisterWindowDispatchPlanner.CompleteReceiveAndDispatch(readiness, queueMemory, receiveBufferSlab);
        if (!receiveDispatch.Succeeded)
        {
            return VirtioNetReceiveRegisterWindowTcpDispatchResult.FromReceiveDispatch(receiveDispatch);
        }

        if (!TryValidateSelectedBufferRange(receiveDispatch, receiveBufferSlab.Length))
        {
            return VirtioNetReceiveRegisterWindowTcpDispatchResult.FromReceiveDispatch(VirtioNetReceiveRegisterWindowDispatchResult.FromInvalidSelectedBuffer(receiveDispatch.SlabCompletion));
        }

        ReadOnlySpan<byte> selectedReceiveBuffer = receiveBufferSlab.Slice((int)receiveDispatch.SelectedBufferOffset, (int)receiveDispatch.UsedByteCount);
        TcpReceiveDispatcher.Dispatch(selectedReceiveBuffer, receiveDispatch.Dispatch, ref stream, ref close, passiveCloseAckSegment, out TcpReceiveDispatchResult tcpDispatch);
        return VirtioNetReceiveRegisterWindowTcpDispatchResult.FromTcpDispatch(receiveDispatch, tcpDispatch);
    }

    private static bool TryValidateSelectedBufferRange(VirtioNetReceiveRegisterWindowDispatchResult receiveDispatch, int slabLength)
    {
        ulong selectedEnd = (ulong)receiveDispatch.SelectedBufferOffset + receiveDispatch.SelectedBufferLength;
        ulong usedEnd = (ulong)receiveDispatch.SelectedBufferOffset + receiveDispatch.UsedByteCount;
        return selectedEnd <= int.MaxValue && usedEnd <= int.MaxValue && selectedEnd <= (ulong)slabLength && usedEnd <= (ulong)slabLength && receiveDispatch.UsedByteCount <= receiveDispatch.SelectedBufferLength;
    }
}
