namespace OS.Drivers;

using OS.Net;

public static class VirtioNetReceiveRegisterWindowDispatchPlanner
{
    public const bool RequiresSuccessfulRegisterWindowReadiness = true;
    public const bool UsesSlabCompletionPlanner = true;
    public const bool SelectsCallerOwnedReceiveBufferFromSlab = true;
    public const bool DispatchesCallerOwnedReceiveBuffer = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedReceiveBufferSlab = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsCallerOwnedReceiveBuffer = true;
    public const bool ClassifiesCallerOwnedFrame = true;
    public const bool ParsesNetworkPayloadMetadata = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketReceive = false;
    public const bool CopiesPayload = false;
    public const bool WritesQueueMemory = false;
    public static VirtioNetReceiveRegisterWindowDispatchResult CompleteReceiveAndDispatch(VirtioNetReceiveReadinessResult readiness, ReadOnlySpan<byte> queueMemory, ReadOnlySpan<byte> receiveBufferSlab)
    {
        VirtioNetReceiveRegisterWindowSlabCompletionResult slabCompletion = VirtioNetReceiveRegisterWindowSlabCompletionPlanner.CompleteReceiveFromSlab(readiness, queueMemory, receiveBufferSlab);
        if (!slabCompletion.Succeeded)
        {
            return VirtioNetReceiveRegisterWindowDispatchResult.FromSlabCompletion(slabCompletion);
        }

        if (!TryValidateSelectedBufferRange(slabCompletion, receiveBufferSlab.Length))
        {
            return VirtioNetReceiveRegisterWindowDispatchResult.FromInvalidSelectedBuffer(slabCompletion);
        }

        NetworkReceiveDispatchResult dispatch = NetworkReceiveDispatcher.Dispatch(receiveBufferSlab.Slice((int)slabCompletion.SelectedBufferOffset, (int)slabCompletion.UsedByteCount));
        return VirtioNetReceiveRegisterWindowDispatchResult.FromDispatch(slabCompletion, dispatch);
    }

    private static bool TryValidateSelectedBufferRange(VirtioNetReceiveRegisterWindowSlabCompletionResult slabCompletion, int slabLength)
    {
        ulong end = (ulong)slabCompletion.SelectedBufferOffset + slabCompletion.SelectedBufferLength;
        return end <= int.MaxValue && end <= (ulong)slabLength && slabCompletion.UsedByteCount <= slabCompletion.SelectedBufferLength;
    }
}
