namespace OS.Drivers;
using OS.Net;

public readonly struct VirtioNetReceiveRegisterWindowTcpDispatchResult
{
    private VirtioNetReceiveRegisterWindowTcpDispatchResult(VirtioNetReceiveRegisterWindowTcpDispatchStatus status, VirtioNetReceiveRegisterWindowDispatchStatus receiveDispatchStatus, VirtioNetReceiveRegisterWindowSlabCompletionStatus slabCompletionStatus, NetworkReceiveDispatchStatus networkDispatchStatus, TcpReceiveDispatchStatus tcpDispatchStatus, TcpReceiveDispatchAction tcpAction, ushort descriptorHead, ushort bufferIndex, uint selectedBufferOffset, uint selectedBufferLength, uint usedByteCount, int payloadBytes, int passiveAckBytes, VirtioNetReceiveRegisterWindowDispatchResult receiveDispatch, TcpReceiveDispatchResult tcpDispatch)
    {
        IsInitialized = true;
        Status = status;
        ReceiveDispatchStatus = receiveDispatchStatus;
        SlabCompletionStatus = slabCompletionStatus;
        NetworkDispatchStatus = networkDispatchStatus;
        TcpDispatchStatus = tcpDispatchStatus;
        TcpAction = tcpAction;
        DescriptorHead = descriptorHead;
        BufferIndex = bufferIndex;
        SelectedBufferOffset = selectedBufferOffset;
        SelectedBufferLength = selectedBufferLength;
        UsedByteCount = usedByteCount;
        PayloadBytes = payloadBytes;
        PassiveAckBytes = passiveAckBytes;
        ReceiveDispatch = receiveDispatch;
        SlabCompletion = receiveDispatch.SlabCompletion;
        NetworkDispatch = receiveDispatch.Dispatch;
        TcpDispatch = tcpDispatch;
    }

    public bool IsInitialized { get; }
    public VirtioNetReceiveRegisterWindowTcpDispatchStatus Status { get; }
    public VirtioNetReceiveRegisterWindowDispatchStatus ReceiveDispatchStatus { get; }
    public VirtioNetReceiveRegisterWindowSlabCompletionStatus SlabCompletionStatus { get; }
    public NetworkReceiveDispatchStatus NetworkDispatchStatus { get; }
    public TcpReceiveDispatchStatus TcpDispatchStatus { get; }
    public TcpReceiveDispatchAction TcpAction { get; }
    public ushort DescriptorHead { get; }
    public ushort BufferIndex { get; }
    public uint SelectedBufferOffset { get; }
    public uint SelectedBufferLength { get; }
    public uint UsedByteCount { get; }
    public int PayloadBytes { get; }
    public int PassiveAckBytes { get; }
    public VirtioNetReceiveRegisterWindowDispatchResult ReceiveDispatch { get; }
    public VirtioNetReceiveRegisterWindowSlabCompletionResult SlabCompletion { get; }
    public NetworkReceiveDispatchResult NetworkDispatch { get; }
    public TcpReceiveDispatchResult TcpDispatch { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success;

    internal static VirtioNetReceiveRegisterWindowTcpDispatchResult FromReceiveDispatch(VirtioNetReceiveRegisterWindowDispatchResult receiveDispatch)
    {
        VirtioNetReceiveRegisterWindowTcpDispatchStatus status = receiveDispatch.Status == VirtioNetReceiveRegisterWindowDispatchStatus.SelectedBufferRangeInvalid ? VirtioNetReceiveRegisterWindowTcpDispatchStatus.SelectedBufferRangeInvalid : VirtioNetReceiveRegisterWindowTcpDispatchStatus.ReceiveDispatchFailed;
        return new VirtioNetReceiveRegisterWindowTcpDispatchResult(status, receiveDispatch.Status, receiveDispatch.SlabCompletionStatus, receiveDispatch.DispatchStatus, default, default, receiveDispatch.DescriptorHead, receiveDispatch.BufferIndex, receiveDispatch.SelectedBufferOffset, receiveDispatch.SelectedBufferLength, receiveDispatch.UsedByteCount, payloadBytes: 0, passiveAckBytes: 0, receiveDispatch, default);
    }

    internal static VirtioNetReceiveRegisterWindowTcpDispatchResult FromTcpDispatch(VirtioNetReceiveRegisterWindowDispatchResult receiveDispatch, TcpReceiveDispatchResult tcpDispatch)
    {
        VirtioNetReceiveRegisterWindowTcpDispatchStatus status = MapTcpStatus(tcpDispatch.Status);
        return new VirtioNetReceiveRegisterWindowTcpDispatchResult(status, receiveDispatch.Status, receiveDispatch.SlabCompletionStatus, receiveDispatch.DispatchStatus, tcpDispatch.Status, tcpDispatch.Action, receiveDispatch.DescriptorHead, receiveDispatch.BufferIndex, receiveDispatch.SelectedBufferOffset, receiveDispatch.SelectedBufferLength, receiveDispatch.UsedByteCount, tcpDispatch.PayloadBytes, tcpDispatch.AckBytesWritten, receiveDispatch, tcpDispatch);
    }

    private static VirtioNetReceiveRegisterWindowTcpDispatchStatus MapTcpStatus(TcpReceiveDispatchStatus status)
    {
        return status switch
        {
            TcpReceiveDispatchStatus.Success => VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success,
            TcpReceiveDispatchStatus.NonIpv4Payload or TcpReceiveDispatchStatus.UnsupportedIpProtocol => VirtioNetReceiveRegisterWindowTcpDispatchStatus.UnsupportedProtocol,
            TcpReceiveDispatchStatus.BadTcpChecksum => VirtioNetReceiveRegisterWindowTcpDispatchStatus.BadTcpChecksum,
            TcpReceiveDispatchStatus.TcpStreamFailed => VirtioNetReceiveRegisterWindowTcpDispatchStatus.TcpStreamFailed,
            _ => VirtioNetReceiveRegisterWindowTcpDispatchStatus.TcpDispatchFailed
        };
    }
}
