using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Diagnostics;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetReceiveRegisterWindowTcpDispatchPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ushort RxQueueSize = 8;
    private const ushort TxQueueSize = 4;
    private const ushort RxBufferCount = 3;
    private const ushort RxNotifyOffset = 3;
    private const ushort TxNotifyOffset = 5;
    private const ushort RxDeviceMaximumQueueSize = 16;
    private const ushort TxDeviceMaximumQueueSize = 8;
    private const uint NotifyOffsetMultiplier = 4;
    private const uint NotifyApertureBarOffset = 0x80;
    private const ulong NotifyBarPhysicalBase = 0x80001000UL;
    private const ulong NotifyBarVirtualBase = 0xFFFF800000001000UL;
    private const uint FrameBufferBytes = VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes;
    private const ulong RxBufferPhysicalAddress = 0x0040_0000UL;
    private const ushort LocalPort = 49190;
    private const ushort RemotePort = 9443;
    private const uint LocalNextSequence = 0x01020304;
    private const uint RemoteNextSequence = 0x10203040;
    [HostTest("drivers.virtionet.rx.tcp.dispatch.windowexec accepts established payload", HostLane.Fast, 0)]
    private static bool TestAcceptsEstablishedPayload()
    {
        byte[] payload = Encoding.ASCII.GetBytes("data");
        TestDispatchInput input = CreateDispatchInput(CreateTcpReceiveBuffer(RemoteNextSequence, LocalNextSequence, TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck, payload, corruptChecksum: false), descriptorHead: 2, bufferIndex: 1, barrierSequence: 761);
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchTcp(input.Readiness, input.QueueMemory, input.ReceiveBufferSlab, ref stream, ref close, Span<byte>.Empty);
        Span<byte> read = stackalloc byte[4];
        TcpEstablishedStreamStatus readStatus = stream.Read(read, out TcpEstablishedStreamReadResult readResult);
        return HostCheck.Result(result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success && result.ReceiveDispatchStatus == VirtioNetReceiveRegisterWindowDispatchStatus.Success && result.SlabCompletionStatus == VirtioNetReceiveRegisterWindowSlabCompletionStatus.Success && result.NetworkDispatchStatus == NetworkReceiveDispatchStatus.Success && result.TcpDispatchStatus == TcpReceiveDispatchStatus.Success && result.TcpAction == TcpReceiveDispatchAction.PayloadAccepted && result.DescriptorHead == 2 && result.BufferIndex == 1 && result.SelectedBufferOffset == FrameBufferBytes && result.SelectedBufferLength == FrameBufferBytes && result.UsedByteCount == input.ReceiveBufferLength && result.PayloadBytes == payload.Length && result.PassiveAckBytes == 0 && result.ReceiveDispatch.Dispatch.PayloadKind == EthernetPayloadKind.Ipv4 && result.NetworkDispatch.Ipv4Header.Protocol == TcpReceiveDispatcher.ProtocolTcp && result.TcpDispatch.PayloadBytes == payload.Length && result.TcpDispatch.LocalNextSequenceNumber == LocalNextSequence && result.TcpDispatch.RemoteNextSequenceNumber == RemoteNextSequence + (uint)payload.Length && stream.UnreadBytes == 0 && stream.RemoteNextSequenceNumber == RemoteNextSequence + (uint)payload.Length && close.State == TcpConnectionCloseState.Established && readStatus == TcpEstablishedStreamStatus.Success && readResult.BytesRead == payload.Length && BytesEqual(read, payload));
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch.windowexec acknowledges passive fin", HostLane.Fast, 1)]
    private static bool TestAcknowledgesPassiveFin()
    {
        TestDispatchInput input = CreateDispatchInput(CreateTcpReceiveBuffer(RemoteNextSequence, LocalNextSequence, TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck, ReadOnlySpan<byte>.Empty, corruptChecksum: false), descriptorHead: 1, bufferIndex: 0, barrierSequence: 762);
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        Span<byte> ackSegment = stackalloc byte[TcpHeader.MinHeaderLength];
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchTcp(input.Readiness, input.QueueMemory, input.ReceiveBufferSlab, ref stream, ref close, ackSegment);
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(LocalAddress(), RemoteAddress(), TcpReceiveDispatcher.ProtocolTcp, ackSegment, out Ipv4TransportChecksumResult checksum);
        return HostCheck.Result(result.Succeeded && result.TcpAction == TcpReceiveDispatchAction.FinAcknowledged && result.PayloadBytes == 0 && result.PassiveAckBytes == TcpHeader.MinHeaderLength && result.TcpDispatch.AckBytesWritten == TcpHeader.MinHeaderLength && result.TcpDispatch.CloseState == TcpConnectionCloseState.CloseWait && close.State == TcpConnectionCloseState.CloseWait && checksumStatus == Ipv4TransportChecksumStatus.Success && checksum.IsValid);
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch.windowexec rejects failed rx dispatch", HostLane.Fast, 2)]
    private static bool TestRejectsFailedRxDispatch()
    {
        TestContext context = CreateContext();
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetReceiveReadinessResult readiness = CreateSuccessfulReadiness(context, queueMemory, notifyBytes, barrierSequence: 763);
        WriteUsedIndex(readiness.ReceivePublish.QueuePlan, queueMemory, readiness.PreviousUsedIndex);
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchTcp(readiness, queueMemory, CreateReceiveSlab(), ref stream, ref close, Span<byte>.Empty);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowTcpDispatchStatus.ReceiveDispatchFailed && result.ReceiveDispatchStatus == VirtioNetReceiveRegisterWindowDispatchStatus.SlabCompletionFailed && result.SlabCompletionStatus == VirtioNetReceiveRegisterWindowSlabCompletionStatus.NoCompletion && result.NetworkDispatchStatus == default && result.TcpDispatchStatus == default && result.PayloadBytes == 0 && result.PassiveAckBytes == 0 && stream.RemoteNextSequenceNumber == RemoteNextSequence && close.State == TcpConnectionCloseState.Established);
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch.windowexec rejects non-TCP protocol", HostLane.Fast, 3)]
    private static bool TestRejectsNonTcpProtocol()
    {
        TestDispatchInput input = CreateDispatchInput(CreateIpv4UdpReceiveBuffer(), descriptorHead: 1, bufferIndex: 0, barrierSequence: 764);
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchTcp(input.Readiness, input.QueueMemory, input.ReceiveBufferSlab, ref stream, ref close, Span<byte>.Empty);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowTcpDispatchStatus.UnsupportedProtocol && result.ReceiveDispatchStatus == VirtioNetReceiveRegisterWindowDispatchStatus.Success && result.NetworkDispatchStatus == NetworkReceiveDispatchStatus.Success && result.TcpDispatchStatus == TcpReceiveDispatchStatus.UnsupportedIpProtocol && result.TcpDispatch.TcpPacketStatus == PacketParseStatus.UnsupportedIpProtocol && result.PayloadBytes == 0 && result.PassiveAckBytes == 0 && stream.RemoteNextSequenceNumber == RemoteNextSequence && close.State == TcpConnectionCloseState.Established);
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch.windowexec rejects bad TCP checksum or TCP dispatch failure", HostLane.Fast, 4)]
    private static bool TestRejectsBadTcpChecksumOrDispatchFailure()
    {
        TestDispatchInput input = CreateDispatchInput(CreateTcpReceiveBuffer(RemoteNextSequence, LocalNextSequence, TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck, Encoding.ASCII.GetBytes("bad"), corruptChecksum: true), descriptorHead: 1, bufferIndex: 0, barrierSequence: 765);
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchTcp(input.Readiness, input.QueueMemory, input.ReceiveBufferSlab, ref stream, ref close, Span<byte>.Empty);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowTcpDispatchStatus.BadTcpChecksum && result.ReceiveDispatchStatus == VirtioNetReceiveRegisterWindowDispatchStatus.Success && result.NetworkDispatchStatus == NetworkReceiveDispatchStatus.Success && result.TcpDispatchStatus == TcpReceiveDispatchStatus.BadTcpChecksum && result.TcpDispatch.TcpPacketStatus == PacketParseStatus.BadChecksum && result.PayloadBytes == 0 && result.PassiveAckBytes == 0 && stream.RemoteNextSequenceNumber == RemoteNextSequence && stream.UnreadBytes == 0 && close.State == TcpConnectionCloseState.Established);
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch.windowexec rejects failed TCP stream path", HostLane.Fast, 5)]
    private static bool TestRejectsFailedTcpStreamPath()
    {
        TestDispatchInput input = CreateDispatchInput(CreateTcpReceiveBuffer(RemoteNextSequence + 4, LocalNextSequence, TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck, Encoding.ASCII.GetBytes("skip"), corruptChecksum: false), descriptorHead: 2, bufferIndex: 1, barrierSequence: 766);
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchTcp(input.Readiness, input.QueueMemory, input.ReceiveBufferSlab, ref stream, ref close, Span<byte>.Empty);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowTcpDispatchStatus.TcpStreamFailed && result.ReceiveDispatchStatus == VirtioNetReceiveRegisterWindowDispatchStatus.Success && result.TcpDispatchStatus == TcpReceiveDispatchStatus.TcpStreamFailed && result.TcpDispatch.StreamStatus == TcpEstablishedStreamStatus.OutOfOrderSegment && result.DescriptorHead == 2 && result.BufferIndex == 1 && result.PayloadBytes == 0 && stream.RemoteNextSequenceNumber == RemoteNextSequence && stream.UnreadBytes == 0 && close.State == TcpConnectionCloseState.Established);
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch.windowexec default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = default;
        return HostCheck.Result(!result.IsInitialized && !result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success && result.ReceiveDispatchStatus == default && result.SlabCompletionStatus == default && result.NetworkDispatchStatus == default && result.TcpDispatchStatus == default && result.TcpAction == default && result.DescriptorHead == 0 && result.BufferIndex == 0 && result.SelectedBufferOffset == 0 && result.SelectedBufferLength == 0 && result.UsedByteCount == 0 && result.PayloadBytes == 0 && result.PassiveAckBytes == 0 && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.RequiresSuccessfulRegisterWindowReadiness && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.UsesReceiveRegisterWindowDispatchPlanner && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.UsesTcpReceiveDispatcher && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.SelectsCallerOwnedReceiveBufferFromSlab && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.UsesSelectedCallerOwnedReceiveBufferSlice && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PreservesReceiveDispatchMetadata && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PreservesSlabCompletionMetadata && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PreservesNetworkDispatchMetadata && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PreservesTcpDispatchMetadata && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.ResultLayoutIsPrimitiveOnly && !VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PerformsLiveMmio && !VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PerformsPacketIo && !VirtioNetReceiveRegisterWindowTcpDispatchPlanner.PerformsPacketReceive && !VirtioNetReceiveRegisterWindowTcpDispatchPlanner.OpensSockets && !VirtioNetReceiveRegisterWindowTcpDispatchPlanner.StartsKestrel && !VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchesSockets && VirtioNetReceiveRegisterWindowTcpDispatchPlanner.MutatesTcpControlBlocks && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetReceiveRegisterWindowTcpDispatchResult>());
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch formatter formats success marker", HostLane.Fast, 7)]
    private static bool TestFormatterFormatsSuccessMarker()
    {
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = CreateSuccessfulPayloadDispatchForFormatter(barrierSequence: 767, out int payloadLength);
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult query = VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.QueryRequiredBytes(result);
        byte[] destination = new byte[query.RequiredBytes];
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult format = VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.FormatResult(result, destination);
        string marker = Encoding.ASCII.GetString(destination);
        return HostCheck.Result(result.Succeeded && query.IsInitialized && query.Status == VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success && query.DispatchStatus == VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success && query.RequiredBytes == destination.Length && format.Succeeded && format.BytesWritten == destination.Length && format.RequiredBytes == destination.Length && marker.Contains("VIRTIO_NET_RX_TCP_DISPATCH status=Success", StringComparison.Ordinal) && marker.Contains(" rx=Success", StringComparison.Ordinal) && marker.Contains(" slab=Success", StringComparison.Ordinal) && marker.Contains(" network=Success", StringComparison.Ordinal) && marker.Contains(" protocol=6", StringComparison.Ordinal) && marker.Contains(" payloadKind=Ipv4", StringComparison.Ordinal) && marker.Contains(" tcp=Success", StringComparison.Ordinal) && marker.Contains(" action=PayloadAccepted", StringComparison.Ordinal) && marker.Contains(" descriptorHead=2", StringComparison.Ordinal) && marker.Contains(" bufferIndex=1", StringComparison.Ordinal) && marker.Contains(" selectedOffset=" + FrameBufferBytes, StringComparison.Ordinal) && marker.Contains(" selectedLength=" + FrameBufferBytes, StringComparison.Ordinal) && marker.Contains(" usedBytes=" + result.UsedByteCount, StringComparison.Ordinal) && marker.Contains(" payloadBytes=" + payloadLength, StringComparison.Ordinal) && marker.Contains(" passiveAckBytes=0", StringComparison.Ordinal) && marker.Contains(" liveMmio=false", StringComparison.Ordinal) && marker.Contains(" packetIo=false", StringComparison.Ordinal) && marker.Contains(" packetRx=false", StringComparison.Ordinal) && marker.Contains(" sockets=false", StringComparison.Ordinal) && marker.Contains(" socketDispatch=false", StringComparison.Ordinal) && marker.Contains(" kestrel=false", StringComparison.Ordinal) && marker.Contains(" tcpControlBlocks=true", StringComparison.Ordinal));
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch formatter reports short destination", HostLane.Fast, 8)]
    private static bool TestFormatterReportsShortDestination()
    {
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = CreateSuccessfulPayloadDispatchForFormatter(barrierSequence: 768, out int _);
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult query = VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.QueryRequiredBytes(result);
        byte[] destination = new byte[query.RequiredBytes - 1];
        Fill(destination, 0xCC);
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult format = VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.FormatResult(result, destination);
        return HostCheck.Result(query.IsInitialized && query.Status == VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success && format.IsInitialized && !format.Succeeded && format.Status == VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.DestinationTooSmall && format.DispatchStatus == VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success && format.BytesWritten == 0 && format.RequiredBytes == query.RequiredBytes && format.AvailableBytes == destination.Length && destination[0] == 0xCC && destination[destination.Length - 1] == 0xCC);
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch formatter rejects default result", HostLane.Fast, 9)]
    private static bool TestFormatterRejectsDefaultResult()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xCC);
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = default;
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult format = VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.FormatResult(result, destination);
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult query = VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.QueryRequiredBytes(result);
        VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult defaultFormat = default;
        return HostCheck.Result(format.IsInitialized && !format.Succeeded && format.Status == VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.InvalidResult && format.DispatchStatus == VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success && format.BytesWritten == 0 && format.RequiredBytes == 0 && format.AvailableBytes == destination.Length && query.IsInitialized && !query.Succeeded && query.Status == VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.InvalidResult && query.RequiredBytes == 0 && !defaultFormat.IsInitialized && !defaultFormat.Succeeded && defaultFormat.Status == VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatStatus.Success && defaultFormat.DispatchStatus == VirtioNetReceiveRegisterWindowTcpDispatchStatus.Success && destination[0] == 0xCC && destination[63] == 0xCC && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatResult>());
    }

    [HostTest("drivers.virtionet.rx.tcp.dispatch formatter uses shared ascii text builder", HostLane.Fast, 10)]
    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(repositoryRoot, "src", "OS.Kernel", "Drivers", "VirtioNetReceiveRegisterWindowTcpDispatchDiagnosticFormatter.cs"));
        return HostCheck.Result(source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, value)", StringComparison.Ordinal) && source.Contains("AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)", StringComparison.Ordinal) && source.Contains("AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)", StringComparison.Ordinal) && source.Contains("AsciiDecimalFormatter.CountInt32Digits", StringComparison.Ordinal) && source.Contains("AsciiDecimalFormatter.CountUInt32Digits", StringComparison.Ordinal) && !source.Contains("value.CopyTo(destination[offset..])", StringComparison.Ordinal) && !source.Contains("AsciiDecimalFormatter.FormatInt32(destination[offset..], value)", StringComparison.Ordinal) && !source.Contains("AsciiDecimalFormatter.FormatUInt32(destination[offset..], value)", StringComparison.Ordinal) && !source.Contains("private static void AppendInt32", StringComparison.Ordinal) && !source.Contains("private static void AppendUInt32", StringComparison.Ordinal) && !source.Contains("private static void Append(ReadOnlySpan<byte>", StringComparison.Ordinal));
    }

    [HostTest("boot.sequence virtio rx tcp dispatch emits ready marker", HostLane.Fast, 11)]
    private static bool TestBootSequenceEmitsReadyMarker()
    {
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = CreateSuccessfulPayloadDispatchForFormatter(barrierSequence: 769, out int _);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult boot = BootSequence.RunVirtioNetRxTcpDispatchReadiness(result, serial, events);
        return HostCheck.Result(result.Succeeded && boot.Succeeded && serial.Count == 2 && serial.LineAt(0) == BootSequence.VirtioNetRxTcpDispatchReady && serial.LineAt(1).StartsWith("VIRTIO_NET_RX_TCP_DISPATCH status=Success", StringComparison.Ordinal) && serial.LineAt(1).Contains(" tcpControlBlocks=true", StringComparison.Ordinal) && events.Snapshot()[0] == BootSequence.VirtioNetRxTcpDispatchReady && events.Snapshot()[1].StartsWith("VIRTIO_NET_RX_TCP_DISPATCH status=Success", StringComparison.Ordinal));
    }

    [HostTest("boot.sequence virtio rx tcp dispatch rejects failed result", HostLane.Fast, 12)]
    private static bool TestBootSequenceRejectsFailedResult()
    {
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = CreateBadChecksumDispatchForBootSequence();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult boot = BootSequence.RunVirtioNetRxTcpDispatchReadiness(result, serial, events);
        return HostCheck.Result(result.IsInitialized && !result.Succeeded && result.Status == VirtioNetReceiveRegisterWindowTcpDispatchStatus.BadTcpChecksum && !boot.Succeeded && boot.Reason.Contains(nameof(VirtioNetRxTcpDispatchReadinessStatus.DispatchFailed), StringComparison.Ordinal) && boot.Reason.Contains(nameof(VirtioNetReceiveRegisterWindowTcpDispatchStatus.BadTcpChecksum), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0).StartsWith(BootSequence.VirtioNetRxTcpDispatchNotReadyPrefix, StringComparison.Ordinal) && serial.LineAt(0).Contains("status=DispatchFailed", StringComparison.Ordinal) && serial.LineAt(0).Contains("dispatch=BadTcpChecksum", StringComparison.Ordinal) && events.Snapshot()[0] == serial.LineAt(0));
    }

    [HostTest("boot.sequence virtio rx tcp dispatch rejects default result", HostLane.Fast, 13)]
    private static bool TestBootSequenceRejectsDefaultResult()
    {
        VirtioNetReceiveRegisterWindowTcpDispatchResult result = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);
        BootResult boot = BootSequence.RunVirtioNetRxTcpDispatchReadiness(result, serial, events);
        return HostCheck.Result(!result.IsInitialized && !boot.Succeeded && boot.Reason.Contains(nameof(VirtioNetRxTcpDispatchReadinessStatus.NotInitialized), StringComparison.Ordinal) && serial.Count == 1 && serial.LineAt(0).StartsWith(BootSequence.VirtioNetRxTcpDispatchNotReadyPrefix, StringComparison.Ordinal) && serial.LineAt(0).Contains("status=NotInitialized", StringComparison.Ordinal) && serial.LineAt(0).Contains("format=InvalidResult", StringComparison.Ordinal) && events.Snapshot()[0] == serial.LineAt(0));
    }

    [HostTest("boot.sequence virtio rx tcp dispatch preserves default markers", HostLane.Fast, 14)]
    private static bool TestBootSequencePreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.Contains(BootSequence.VirtioNetRxTcpDispatchReady) && !serial.ContainsPrefix(BootSequence.VirtioNetRxTcpDispatchNotReadyPrefix) && !serial.ContainsPrefix("VIRTIO_NET_RX_TCP_DISPATCH status="));
    }

    private static TestDispatchInput CreateDispatchInput(byte[] receiveBuffer, uint descriptorHead, ushort bufferIndex, uint barrierSequence)
    {
        TestContext context = CreateContext();
        byte[] queueMemory = CreateRxQueueMemory(context);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetReceiveReadinessResult readiness = CreateSuccessfulReadiness(context, queueMemory, notifyBytes, barrierSequence);
        byte[] slab = CreateReceiveSlab();
        Copy(receiveBuffer, slab.AsSpan((int)(bufferIndex * FrameBufferBytes)));
        WriteUsedCompletion(readiness.ReceivePublish.QueuePlan, queueMemory, readiness.PreviousUsedIndex, readiness.ExpectedNextUsedIndex, descriptorHead, (uint)receiveBuffer.Length);
        return new TestDispatchInput(readiness, queueMemory, slab, receiveBuffer.Length);
    }

    private static VirtioNetReceiveRegisterWindowTcpDispatchResult CreateSuccessfulPayloadDispatchForFormatter(uint barrierSequence, out int payloadLength)
    {
        byte[] payload = Encoding.ASCII.GetBytes("diag");
        payloadLength = payload.Length;
        TestDispatchInput input = CreateDispatchInput(CreateTcpReceiveBuffer(RemoteNextSequence, LocalNextSequence, TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck, payload, corruptChecksum: false), descriptorHead: 2, bufferIndex: 1, barrierSequence);
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        return VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchTcp(input.Readiness, input.QueueMemory, input.ReceiveBufferSlab, ref stream, ref close, Span<byte>.Empty);
    }

    private static VirtioNetReceiveRegisterWindowTcpDispatchResult CreateBadChecksumDispatchForBootSequence()
    {
        byte[] payload = Encoding.ASCII.GetBytes("bad");
        TestDispatchInput input = CreateDispatchInput(CreateTcpReceiveBuffer(RemoteNextSequence, LocalNextSequence, TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck, payload, corruptChecksum: true), descriptorHead: 2, bufferIndex: 1, barrierSequence: 770);
        Span<byte> receiveStorage = stackalloc byte[16];
        TcpEstablishedStream stream = CreateStream(receiveStorage);
        TcpConnectionClose close = CreateClose();
        return VirtioNetReceiveRegisterWindowTcpDispatchPlanner.DispatchTcp(input.Readiness, input.QueueMemory, input.ReceiveBufferSlab, ref stream, ref close, Span<byte>.Empty);
    }

    private static VirtioNetReceiveReadinessResult CreateSuccessfulReadiness(TestContext context, Span<byte> queueMemory, Span<byte> notifyBytes, uint barrierSequence)
    {
        byte[] commonBytes = CreateCommonWindowBytes();
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult setupExecution = VirtioNetQueueMmioSetupRegisterWindowExecutor.Execute(context.QueueSetup, context.QueueSetupCommands, context.RegisterWindows.Bindings.CommonConfiguration, commonBytes);
        return VirtioNetReceiveReadinessPlanner.ComposeReceive(context.QueueSetup, setupExecution, firstDescriptorIndex: 1, availableDescriptorCount: RxBufferCount, previousAvailableRingIndex: 6, previousUsedIndex: 4, RxBufferPhysicalAddress, receiveBufferStrideBytes: FrameBufferBytes, receiveBufferCount: 2, frameBufferBytes: FrameBufferBytes, queueMemory, VirtioNetQueueNotificationBarrierReadiness.Ready(barrierSequence), context.Apertures.NotifyConfiguration, context.RegisterWindows.Bindings.NotifyConfiguration, notifyBytes, negotiatedFeatures: 0);
    }

    private static TestContext CreateContext()
    {
        VirtioPciBindingPlanResult bindingPlan = CreateBindingPlan();
        VirtioPciBarRegisterWindowBindingResult registerWindows = VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(bindingPlan, CreateRegisterWindows());
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(bindingPlan);
        VirtioNetQueuePlanResult queuePlan = VirtioNetQueuePlan.Create(VirtioNetQueuePlan.MinimumQueueCount, RxQueueSize, TxQueueSize, RxBufferCount, FrameBufferBytes);
        if (!queuePlan.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic queue plan.");
        }

        VirtioNetDriverQueueBringUpPlanResult bringUp = VirtioNetDriverQueueBringUpPlan.Create(descriptor, RxQueueSize, TxQueueSize, RxBufferCount, FrameBufferBytes, queuePlan.Plan.RxQueue.RequiredByteLength, queuePlan.Plan.TxQueue.RequiredByteLength, queuePlan.Plan.RxBuffers.RequiredBufferBytes, queuePlan.Plan.TxFrame.RequiredBufferBytes);
        if (!bringUp.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic bring-up plan.");
        }

        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult queueSetup = VirtioNetQueueMmioSetupPlanner.Create(bringUp, CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        if (!queueSetup.Succeeded || !registerWindows.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic register-window setup.");
        }

        return new TestContext(bindingPlan.Binding.Apertures, registerWindows, queueSetup, commands, queuePlan);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateRxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(descriptorTablePhysicalAddress: 0x100000, driverRingPhysicalAddress: 0x101000, deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateTxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(descriptorTablePhysicalAddress: 0x110000, driverRingPhysicalAddress: 0x111000, deviceRingPhysicalAddress: 0x112000);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateRxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(RxDeviceMaximumQueueSize, hasNotifyOffset: true, RxNotifyOffset);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateTxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(TxDeviceMaximumQueueSize, hasNotifyOffset: true, TxNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateNetworkDescriptor(VirtioPciBindingPlanResult bindingPlan)
    {
        VirtioDeviceDescriptorSource[] sources = [new VirtioDeviceDescriptorSource(sourceIndex: 49, bindingPlan, VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask, VirtioDeviceInitializationProfile.NetworkRequiredQueueCount, featuresOkAccepted: true)];
        VirtioDeviceConfigurationSource[] configurations = [new VirtioDeviceConfigurationSource(sourceIndex: 49, CreateNetworkConfiguration(new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xD5), VirtioNetDeviceConfig.StatusLinkUp))];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];
        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(sources, configurations, destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic virtio-net descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId);
        return VirtioPciBindingPlan.Create(CreateScannedDevice(deviceId), CreatePciConfiguration(deviceId), CreateBarSizes());
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(segmentIndex: 2, new PciEcamSegment(basePhysicalAddress: 0xE0000000UL, segmentGroup: 1, startBus: 0, endBus: 0x7F), new PciScannedDevice(bus: 3, device: 5, function: 0, vendorId: VirtioPciDeviceDiscovery.VirtioVendorId, deviceId: deviceId, revisionId: 0, classCode: 0, subclass: 0, programmingInterface: 0, headerType: PciConfigurationHeader.Type0HeaderType, isMultifunction: false, hasCapabilitiesList: true, capabilityPointer: CommonOffset));
    }

    private static byte[] CreatePciConfiguration(ushort deviceId)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x00, sizeof(ushort)), VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x06, sizeof(ushort)), PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, 0x80000000);
        WriteUInt32(config, 0x14, 0x80001000);
        WriteUInt32(config, 0x18, 0x80002000);
        WriteUInt32(config, 0x1C, 0x80003000);
        config[0x34] = CommonOffset;
        WriteVirtioCapability(config, CommonOffset, NotifyOffset, VirtioPciCapabilityType.CommonConfiguration, bar: 0, offsetValue: 0x20, length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, NotifyOffset, IsrOffset, VirtioPciCapabilityType.NotifyConfiguration, bar: 1, offsetValue: NotifyApertureBarOffset, length: 0x100, notifyOffsetMultiplier: NotifyOffsetMultiplier, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(config, IsrOffset, DeviceOffset, VirtioPciCapabilityType.IsrStatus, bar: 2, offsetValue: 0, length: 1, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, DeviceOffset, nextOffset: 0, VirtioPciCapabilityType.DeviceConfiguration, bar: 3, offsetValue: 0, length: 0x10, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return[0x1000, 0x1000, 0x1000, 0x1000, 0, 0];
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindows()
    {
        return[CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL), CreateRegisterWindow(1, NotifyBarPhysicalBase, NotifyBarVirtualBase), CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL), CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL), default, default ];
    }

    private static VirtioPciMappedBarWindow CreateRegisterWindow(byte barIndex, ulong physicalBaseAddress, ulong virtualBaseAddress)
    {
        return new VirtioPciMappedBarWindow(barIndex, physicalBaseAddress, virtualBaseAddress, length: 0x1000, isMapped: true, isMmio: true, isDeviceMemory: true, isUncached: true);
    }

    private static byte[] CreateNetworkConfiguration(MacAddress mac, ushort status)
    {
        byte[] configuration = new byte[8];
        configuration[0] = mac.Octet0;
        configuration[1] = mac.Octet1;
        configuration[2] = mac.Octet2;
        configuration[3] = mac.Octet3;
        configuration[4] = mac.Octet4;
        configuration[5] = mac.Octet5;
        EndianPrimitives.WriteUInt16LittleEndian(configuration.AsSpan(6, sizeof(ushort)), status);
        return configuration;
    }

    private static void WriteVirtioCapability(Span<byte> config, byte offset, byte nextOffset, VirtioPciCapabilityType type, byte bar, uint offsetValue, uint length, uint notifyOffsetMultiplier, byte capabilityLength)
    {
        config[offset] = VirtioPciCapabilityListParser.VendorSpecificCapabilityId;
        config[offset + 1] = nextOffset;
        config[offset + 2] = capabilityLength;
        config[offset + 3] = (byte)type;
        config[offset + 4] = bar;
        WriteUInt32(config, offset + 8, offsetValue);
        WriteUInt32(config, offset + 12, length);
        if (type == VirtioPciCapabilityType.NotifyConfiguration)
        {
            WriteUInt32(config, offset + 16, notifyOffsetMultiplier);
        }
    }

    private static byte[] CreateTcpReceiveBuffer(uint sequenceNumber, uint acknowledgementNumber, byte flags, ReadOnlySpan<byte> payload, bool corruptChecksum)
    {
        byte[] buffer = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpHeader.MinHeaderLength + payload.Length];
        Span<byte> tcpSegment = buffer.AsSpan(ExpectedTcpSegmentOffset, TcpHeader.MinHeaderLength + payload.Length);
        PacketParseStatus tcpStatus = TcpHeader.TryWrite(tcpSegment, RemotePort, LocalPort, sequenceNumber, acknowledgementNumber, flags, windowSize: 0x3000, checksum: 0, urgentPointer: 0, ReadOnlySpan<byte>.Empty, payload, out int tcpBytesWritten);
        if (tcpStatus != PacketParseStatus.Success || tcpBytesWritten != tcpSegment.Length)
        {
            throw new InvalidOperationException("Failed to build deterministic TCP segment.");
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(RemoteAddress(), LocalAddress(), TcpReceiveDispatcher.ProtocolTcp, tcpSegment, out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            throw new InvalidOperationException("Failed to compute deterministic TCP checksum.");
        }

        ushort checksum = checksumResult.Checksum == 0 ? ushort.MaxValue : checksumResult.Checksum;
        if (corruptChecksum)
        {
            checksum ^= 0x0100;
        }

        EndianPrimitives.WriteUInt16BigEndian(tcpSegment.Slice(16, sizeof(ushort)), checksum);
        WriteEthernetIpv4Headers(buffer, TcpReceiveDispatcher.ProtocolTcp, tcpSegment.Length);
        return buffer;
    }

    private static byte[] CreateIpv4UdpReceiveBuffer()
    {
        byte[] payload =
        {
            0x70,
            0x69,
            0x6E,
            0x67
        };
        byte[] buffer = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength + payload.Length];
        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(LocalMac(), RemoteMac(), RemoteAddress(), LocalAddress(), sourcePort: 12345, destinationPort: 8080, timeToLive: 64, identification: 0x1234);
        PacketParseStatus status = EthernetIpv4UdpPacket.TryWrite(buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers), info, payload, out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != buffer.Length - VirtioNetHeader.HeaderLengthWithoutMergeableBuffers)
        {
            throw new InvalidOperationException("Failed to build deterministic IPv4 receive buffer.");
        }

        return buffer;
    }

    private static void WriteEthernetIpv4Headers(Span<byte> buffer, byte protocol, int ipv4PayloadLength)
    {
        EthernetHeader ethernet = new EthernetHeader(LocalMac(), RemoteMac(), EthernetHeader.EtherTypeIpv4);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(buffer.Slice(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers, EthernetHeader.HeaderLength), ethernet);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic Ethernet header.");
        }

        Ipv4Header ipv4 = new Ipv4Header(RemoteAddress(), LocalAddress(), protocol, (ushort)(Ipv4Header.MinHeaderLength + ipv4PayloadLength), timeToLive: 64, identification: 0x9190, flagsAndFragmentOffset: 0, typeOfService: 0, headerChecksum: 0, headerLength: Ipv4Header.MinHeaderLength);
        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(buffer.Slice(ExpectedIpv4HeaderOffset, Ipv4Header.MinHeaderLength), ipv4, out int ipv4BytesWritten);
        if (ipv4Status != PacketParseStatus.Success || ipv4BytesWritten != Ipv4Header.MinHeaderLength)
        {
            throw new InvalidOperationException("Failed to build deterministic IPv4 header.");
        }
    }

    private static byte[] CreateRxQueueMemory(TestContext context)
    {
        return new byte[(int)context.QueuePlan.Plan.RxQueue.EndOffset];
    }

    private static byte[] CreateReceiveSlab()
    {
        return new byte[checked((int)FrameBufferBytes * 2)];
    }

    private static byte[] CreateCommonWindowBytes()
    {
        byte[] bytes = new byte[0x1000];
        Fill(bytes, 0xCC);
        return bytes;
    }

    private static byte[] CreateNotifyWindowBytes()
    {
        byte[] notifyBytes = new byte[0x1000];
        Fill(notifyBytes, 0xCC);
        return notifyBytes;
    }

    private static void WriteUsedIndex(VirtqueueSplitRingPlan queue, byte[] queueMemory, ushort currentUsedIndex)
    {
        EndianPrimitives.WriteUInt16LittleEndian(queueMemory.AsSpan((int)queue.UsedRing.Offset + 2, sizeof(ushort)), currentUsedIndex);
    }

    private static void WriteUsedCompletion(VirtqueueSplitRingPlan queue, byte[] queueMemory, ushort previousUsedIndex, ushort currentUsedIndex, uint descriptorHead, uint usedByteCount)
    {
        WriteUsedIndex(queue, queueMemory, currentUsedIndex);
        ushort slot = (ushort)(previousUsedIndex & (queue.QueueSize - 1));
        int elementOffset = (int)queue.UsedRing.Offset + (int)VirtqueueSplitRingPlan.UsedRingHeaderLength + (slot * (int)VirtqueueSplitRingPlan.UsedRingElementLength);
        EndianPrimitives.WriteUInt32LittleEndian(queueMemory.AsSpan(elementOffset, sizeof(uint)), descriptorHead);
        EndianPrimitives.WriteUInt32LittleEndian(queueMemory.AsSpan(elementOffset + sizeof(uint), sizeof(uint)), usedByteCount);
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private static TcpEstablishedStream CreateStream(Span<byte> receiveStorage)
    {
        return TcpEstablishedStream.Create(LocalPort, RemotePort, LocalNextSequence, RemoteNextSequence, receiveStorage);
    }

    private static TcpConnectionClose CreateClose()
    {
        return TcpConnectionClose.CreateEstablished(LocalPort, RemotePort, LocalNextSequence, RemoteNextSequence);
    }

    private static int ExpectedIpv4HeaderOffset => VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength;
    private static int ExpectedTcpSegmentOffset => ExpectedIpv4HeaderOffset + Ipv4Header.MinHeaderLength;

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x90);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x91);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 90);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 91);
    }

    private static void Copy(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        for (int index = 0; index < source.Length; index++)
        {
            destination[index] = source[index];
        }
    }

    private static void Fill(Span<byte> bytes, byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = value;
        }
    }

    private static bool BytesEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }
}
