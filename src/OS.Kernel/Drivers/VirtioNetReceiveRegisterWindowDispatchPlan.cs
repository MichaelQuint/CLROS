namespace OS.Drivers;

using OS.Net;

public enum VirtioNetReceiveRegisterWindowDispatchStatus
{
    Success = 0,
    SlabCompletionFailed = 1,
    SelectedBufferRangeInvalid = 2,
    DispatchFailed = 3
}

public readonly struct VirtioNetReceiveRegisterWindowDispatchResult
{
    private VirtioNetReceiveRegisterWindowDispatchResult(
        VirtioNetReceiveRegisterWindowDispatchStatus status,
        VirtioNetReceiveRegisterWindowSlabCompletionStatus slabCompletionStatus,
        NetworkReceiveDispatchStatus dispatchStatus,
        VirtioNetReceiveReadinessStatus readinessStatus,
        VirtioNetReceiveFrameClassifierStatus classifierStatus,
        PacketParseStatus classifierPacketStatus,
        PacketParseStatus payloadPacketStatus,
        EthernetPayloadKind payloadKind,
        bool dispatchAttempted,
        ushort descriptorHead,
        ushort bufferIndex,
        uint selectedBufferOffset,
        uint selectedBufferLength,
        uint usedByteCount,
        VirtioNetReceiveRegisterWindowSlabCompletionResult slabCompletion,
        NetworkReceiveDispatchResult dispatch)
    {
        IsInitialized = true;
        Status = status;
        SlabCompletionStatus = slabCompletionStatus;
        DispatchStatus = dispatchStatus;
        ReadinessStatus = readinessStatus;
        ClassifierStatus = classifierStatus;
        ClassifierPacketStatus = classifierPacketStatus;
        PayloadPacketStatus = payloadPacketStatus;
        PayloadKind = payloadKind;
        DispatchAttempted = dispatchAttempted;
        DescriptorHead = descriptorHead;
        BufferIndex = bufferIndex;
        SelectedBufferOffset = selectedBufferOffset;
        SelectedBufferLength = selectedBufferLength;
        UsedByteCount = usedByteCount;
        SlabCompletion = slabCompletion;
        Dispatch = dispatch;
    }

    public bool IsInitialized { get; }

    public VirtioNetReceiveRegisterWindowDispatchStatus Status { get; }

    public VirtioNetReceiveRegisterWindowSlabCompletionStatus SlabCompletionStatus { get; }

    public NetworkReceiveDispatchStatus DispatchStatus { get; }

    public VirtioNetReceiveReadinessStatus ReadinessStatus { get; }

    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus { get; }

    public PacketParseStatus ClassifierPacketStatus { get; }

    public PacketParseStatus PayloadPacketStatus { get; }

    public EthernetPayloadKind PayloadKind { get; }

    public bool DispatchAttempted { get; }

    public ushort DescriptorHead { get; }

    public ushort BufferIndex { get; }

    public uint SelectedBufferOffset { get; }

    public uint SelectedBufferLength { get; }

    public uint UsedByteCount { get; }

    public VirtioNetReceiveRegisterWindowSlabCompletionResult SlabCompletion { get; }

    public NetworkReceiveDispatchResult Dispatch { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetReceiveRegisterWindowDispatchStatus.Success;

    internal static VirtioNetReceiveRegisterWindowDispatchResult FromSlabCompletion(
        VirtioNetReceiveRegisterWindowSlabCompletionResult slabCompletion)
    {
        return new VirtioNetReceiveRegisterWindowDispatchResult(
            VirtioNetReceiveRegisterWindowDispatchStatus.SlabCompletionFailed,
            slabCompletion.Status,
            default,
            slabCompletion.ReadinessStatus,
            slabCompletion.ClassifierStatus,
            slabCompletion.PacketStatus,
            default,
            slabCompletion.PayloadKind,
            dispatchAttempted: false,
            slabCompletion.DescriptorHead,
            slabCompletion.BufferIndex,
            slabCompletion.SelectedBufferOffset,
            slabCompletion.SelectedBufferLength,
            slabCompletion.UsedByteCount,
            slabCompletion,
            default);
    }

    internal static VirtioNetReceiveRegisterWindowDispatchResult FromInvalidSelectedBuffer(
        VirtioNetReceiveRegisterWindowSlabCompletionResult slabCompletion)
    {
        return new VirtioNetReceiveRegisterWindowDispatchResult(
            VirtioNetReceiveRegisterWindowDispatchStatus.SelectedBufferRangeInvalid,
            slabCompletion.Status,
            default,
            slabCompletion.ReadinessStatus,
            slabCompletion.ClassifierStatus,
            slabCompletion.PacketStatus,
            default,
            slabCompletion.PayloadKind,
            dispatchAttempted: false,
            slabCompletion.DescriptorHead,
            slabCompletion.BufferIndex,
            slabCompletion.SelectedBufferOffset,
            slabCompletion.SelectedBufferLength,
            slabCompletion.UsedByteCount,
            slabCompletion,
            default);
    }

    internal static VirtioNetReceiveRegisterWindowDispatchResult FromDispatch(
        VirtioNetReceiveRegisterWindowSlabCompletionResult slabCompletion,
        NetworkReceiveDispatchResult dispatch)
    {
        VirtioNetReceiveRegisterWindowDispatchStatus status =
            dispatch.Succeeded
                ? VirtioNetReceiveRegisterWindowDispatchStatus.Success
                : VirtioNetReceiveRegisterWindowDispatchStatus.DispatchFailed;

        return new VirtioNetReceiveRegisterWindowDispatchResult(
            status,
            slabCompletion.Status,
            dispatch.Status,
            slabCompletion.ReadinessStatus,
            dispatch.ClassifierStatus,
            dispatch.ClassifierPacketStatus,
            dispatch.PayloadPacketStatus,
            dispatch.PayloadKind,
            dispatchAttempted: true,
            slabCompletion.DescriptorHead,
            slabCompletion.BufferIndex,
            slabCompletion.SelectedBufferOffset,
            slabCompletion.SelectedBufferLength,
            slabCompletion.UsedByteCount,
            slabCompletion,
            dispatch);
    }
}

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

    public static VirtioNetReceiveRegisterWindowDispatchResult CompleteReceiveAndDispatch(
        VirtioNetReceiveReadinessResult readiness,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> receiveBufferSlab)
    {
        VirtioNetReceiveRegisterWindowSlabCompletionResult slabCompletion =
            VirtioNetReceiveRegisterWindowSlabCompletionPlanner.CompleteReceiveFromSlab(
                readiness,
                queueMemory,
                receiveBufferSlab);
        if (!slabCompletion.Succeeded)
        {
            return VirtioNetReceiveRegisterWindowDispatchResult.FromSlabCompletion(
                slabCompletion);
        }

        if (!TryValidateSelectedBufferRange(
                slabCompletion,
                receiveBufferSlab.Length))
        {
            return VirtioNetReceiveRegisterWindowDispatchResult.FromInvalidSelectedBuffer(
                slabCompletion);
        }

        NetworkReceiveDispatchResult dispatch = NetworkReceiveDispatcher.Dispatch(
            receiveBufferSlab.Slice(
                (int)slabCompletion.SelectedBufferOffset,
                (int)slabCompletion.UsedByteCount));

        return VirtioNetReceiveRegisterWindowDispatchResult.FromDispatch(
            slabCompletion,
            dispatch);
    }

    private static bool TryValidateSelectedBufferRange(
        VirtioNetReceiveRegisterWindowSlabCompletionResult slabCompletion,
        int slabLength)
    {
        ulong end = (ulong)slabCompletion.SelectedBufferOffset +
            slabCompletion.SelectedBufferLength;
        return end <= int.MaxValue &&
            end <= (ulong)slabLength &&
            slabCompletion.UsedByteCount <= slabCompletion.SelectedBufferLength;
    }
}
