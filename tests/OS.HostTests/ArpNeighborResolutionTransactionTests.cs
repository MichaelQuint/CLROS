using System.Runtime.CompilerServices;
using OS.Net;

internal static class ArpNeighborResolutionTransactionTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.arp resolver writes request frame", TestWritesRequestFrame, ref failures);
        Run("net.arp resolver accepts matching reply", TestAcceptsMatchingReply, ref failures);
        Run("net.arp resolver rejects mismatched reply", TestRejectsMismatchedReply, ref failures);
        Run("net.arp resolver times out request", TestTimesOutRequest, ref failures);
        Run("net.arp resolver default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ARP neighbor resolution host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestWritesRequestFrame()
    {
        ArpNeighborResolutionTransaction transaction = CreateTransaction();
        Span<byte> frame = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];

        ArpNeighborResolutionStatus status = transaction.TryWriteRequest(
            frame,
            out ArpNeighborResolutionResult result);
        if (status != ArpNeighborResolutionStatus.Success
            || !result.Succeeded
            || result.State != ArpNeighborResolutionState.RequestSent
            || result.BytesWritten != ArpNeighborResolutionTransaction.RequestFrameLength
            || transaction.State != ArpNeighborResolutionState.RequestSent)
        {
            return false;
        }

        PacketParseStatus ethernetStatus = EthernetHeader.TryDispatch(
            frame,
            out EthernetHeader ethernet,
            out EthernetPayloadKind payloadKind,
            out ReadOnlySpan<byte> payload);
        PacketParseStatus arpStatus = ArpPacket.TryParse(payload, out ArpPacket arp);
        if (ethernetStatus != PacketParseStatus.Success
            || payloadKind != EthernetPayloadKind.Arp
            || arpStatus != PacketParseStatus.Success
            || ethernet.Destination != BroadcastMac()
            || ethernet.Source != LocalMac()
            || ethernet.EtherType != EthernetHeader.EtherTypeArp
            || arp.Operation != ArpPacket.OperationRequest
            || arp.SenderMac != LocalMac()
            || arp.SenderAddress != LocalAddress()
            || arp.TargetMac != ZeroMac()
            || arp.TargetAddress != TargetAddress())
        {
            return false;
        }

        ArpNeighborResolutionStatus repeatStatus = transaction.TryWriteRequest(
            frame,
            out ArpNeighborResolutionResult repeatResult);
        if (repeatStatus != ArpNeighborResolutionStatus.InvalidState
            || repeatResult.State != ArpNeighborResolutionState.RequestSent)
        {
            return false;
        }

        ArpNeighborResolutionTransaction tooSmallTransaction = CreateTransaction();
        Span<byte> tooSmall = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength - 1];
        ArpNeighborResolutionStatus tooSmallStatus = tooSmallTransaction.TryWriteRequest(
            tooSmall,
            out ArpNeighborResolutionResult tooSmallResult);

        return tooSmallStatus == ArpNeighborResolutionStatus.BufferTooSmall
            && !tooSmallResult.Succeeded
            && tooSmallResult.State == ArpNeighborResolutionState.Initial
            && tooSmallResult.PacketStatus == PacketParseStatus.BufferTooSmall
            && tooSmallTransaction.State == ArpNeighborResolutionState.Initial;
    }

    private static bool TestAcceptsMatchingReply()
    {
        ArpNeighborResolutionTransaction transaction = CreateRequestSentTransaction();
        byte[] reply = CreateReplyFrame(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress(),
            operation: ArpPacket.OperationReply);

        ArpNeighborResolutionStatus status = transaction.ReceiveReply(
            reply,
            out ArpNeighborResolutionResult result);

        return status == ArpNeighborResolutionStatus.Success
            && result.Succeeded
            && result.State == ArpNeighborResolutionState.Resolved
            && result.ResolvedMac == RemoteMac()
            && transaction.State == ArpNeighborResolutionState.Resolved
            && transaction.ResolvedMac == RemoteMac();
    }

    private static bool TestRejectsMismatchedReply()
    {
        return RejectsReply(
                CreateReplyFrame(
                    RemoteMac(),
                    LocalMac(),
                    RemoteMac(),
                    new Ipv4Address(192, 0, 2, 77),
                    LocalMac(),
                    LocalAddress(),
                    ArpPacket.OperationReply),
                ArpNeighborResolutionStatus.SenderAddressMismatch,
                PacketParseStatus.Success)
            && RejectsReply(
                CreateReplyFrame(
                    RemoteMac(),
                    LocalMac(),
                    RemoteMac(),
                    TargetAddress(),
                    LocalMac(),
                    new Ipv4Address(192, 0, 2, 99),
                    ArpPacket.OperationReply),
                ArpNeighborResolutionStatus.TargetAddressMismatch,
                PacketParseStatus.Success)
            && RejectsReply(
                CreateReplyFrame(
                    RemoteMac(),
                    LocalMac(),
                    RemoteMac(),
                    TargetAddress(),
                    new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x77),
                    LocalAddress(),
                    ArpPacket.OperationReply),
                ArpNeighborResolutionStatus.TargetMacMismatch,
                PacketParseStatus.Success)
            && RejectsReply(
                CreateReplyFrame(
                    RemoteMac(),
                    LocalMac(),
                    RemoteMac(),
                    TargetAddress(),
                    LocalMac(),
                    LocalAddress(),
                    ArpPacket.OperationRequest),
                ArpNeighborResolutionStatus.UnexpectedOperation,
                PacketParseStatus.Success)
            && RejectsReply(
                CreateReplyFrame(
                    RemoteMac(),
                    new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x88),
                    RemoteMac(),
                    TargetAddress(),
                    LocalMac(),
                    LocalAddress(),
                    ArpPacket.OperationReply),
                ArpNeighborResolutionStatus.EthernetDestinationMismatch,
                PacketParseStatus.Success)
            && RejectsReply(
                CreateReplyFrame(
                    new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x99),
                    LocalMac(),
                    RemoteMac(),
                    TargetAddress(),
                    LocalMac(),
                    LocalAddress(),
                    ArpPacket.OperationReply),
                ArpNeighborResolutionStatus.EthernetSourceMismatch,
                PacketParseStatus.Success)
            && RejectsReply(
                CreateUnsupportedEtherTypeFrame(),
                ArpNeighborResolutionStatus.UnsupportedEtherType,
                PacketParseStatus.UnsupportedEtherType)
            && RejectsReply(
                CreateTruncatedReplyFrame(),
                ArpNeighborResolutionStatus.FrameParseFailed,
                PacketParseStatus.BufferTooSmall)
            && RejectsInitialStateReply();
    }

    private static bool TestTimesOutRequest()
    {
        ArpNeighborResolutionTransaction transaction = CreateRequestSentTransaction();
        ArpNeighborResolutionStatus timeoutStatus = transaction.Timeout(
            out ArpNeighborResolutionResult timeoutResult);

        ArpNeighborResolutionTransaction initialTransaction = CreateTransaction();
        ArpNeighborResolutionStatus invalidStatus = initialTransaction.Timeout(
            out ArpNeighborResolutionResult invalidResult);

        return timeoutStatus == ArpNeighborResolutionStatus.Timeout
            && !timeoutResult.Succeeded
            && timeoutResult.State == ArpNeighborResolutionState.Failed
            && transaction.State == ArpNeighborResolutionState.Failed
            && invalidStatus == ArpNeighborResolutionStatus.InvalidState
            && invalidResult.State == ArpNeighborResolutionState.Initial
            && initialTransaction.State == ArpNeighborResolutionState.Initial;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        ArpNeighborResolutionResult defaultResult = default;
        ArpNeighborResolutionResult failedResult =
            ArpNeighborResolutionResult.Fail(
                ArpNeighborResolutionStatus.InvalidState,
                ArpNeighborResolutionState.Initial,
                PacketParseStatus.Success);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == ArpNeighborResolutionStatus.Success
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == ArpNeighborResolutionStatus.InvalidState
            && failedResult.State == ArpNeighborResolutionState.Initial
            && failedResult.PacketStatus == PacketParseStatus.Success
            && failedResult.BytesWritten == 0
            && failedResult.ResolvedMac == default
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                ArpNeighborResolutionResult>();
    }

    private static bool RejectsReply(
        byte[] reply,
        ArpNeighborResolutionStatus expectedStatus,
        PacketParseStatus expectedPacketStatus)
    {
        ArpNeighborResolutionTransaction transaction = CreateRequestSentTransaction();
        ArpNeighborResolutionStatus status = transaction.ReceiveReply(
            reply,
            out ArpNeighborResolutionResult result);

        return status == expectedStatus
            && !result.Succeeded
            && result.State == ArpNeighborResolutionState.Failed
            && result.PacketStatus == expectedPacketStatus
            && transaction.State == ArpNeighborResolutionState.Failed;
    }

    private static bool RejectsInitialStateReply()
    {
        ArpNeighborResolutionTransaction transaction = CreateTransaction();
        byte[] reply = CreateReplyFrame(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress(),
            operation: ArpPacket.OperationReply);

        ArpNeighborResolutionStatus status = transaction.ReceiveReply(
            reply,
            out ArpNeighborResolutionResult result);

        return status == ArpNeighborResolutionStatus.InvalidState
            && !result.Succeeded
            && result.State == ArpNeighborResolutionState.Initial
            && transaction.State == ArpNeighborResolutionState.Initial;
    }

    private static ArpNeighborResolutionTransaction CreateRequestSentTransaction()
    {
        ArpNeighborResolutionTransaction transaction = CreateTransaction();
        Span<byte> request = stackalloc byte[ArpNeighborResolutionTransaction.RequestFrameLength];
        ArpNeighborResolutionStatus status = transaction.TryWriteRequest(
            request,
            out ArpNeighborResolutionResult result);
        if (status != ArpNeighborResolutionStatus.Success || !result.Succeeded)
        {
            throw new InvalidOperationException("Failed to prepare ARP request state.");
        }

        return transaction;
    }

    private static byte[] CreateUnsupportedEtherTypeFrame()
    {
        byte[] frame = new byte[ArpNeighborResolutionTransaction.RequestFrameLength];
        EthernetHeader header = new EthernetHeader(LocalMac(), RemoteMac(), EthernetHeader.EtherTypeIpv4);
        if (EthernetHeader.TryWrite(frame, header) != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build unsupported EtherType frame.");
        }

        return frame;
    }

    private static byte[] CreateTruncatedReplyFrame()
    {
        byte[] fullFrame = CreateReplyFrame(
            ethernetSource: RemoteMac(),
            ethernetDestination: LocalMac(),
            arpSenderMac: RemoteMac(),
            arpSenderAddress: TargetAddress(),
            arpTargetMac: LocalMac(),
            arpTargetAddress: LocalAddress(),
            operation: ArpPacket.OperationReply);
        byte[] truncated = new byte[fullFrame.Length - 1];
        Array.Copy(fullFrame, truncated, truncated.Length);
        return truncated;
    }

    private static byte[] CreateReplyFrame(
        MacAddress ethernetSource,
        MacAddress ethernetDestination,
        MacAddress arpSenderMac,
        Ipv4Address arpSenderAddress,
        MacAddress arpTargetMac,
        Ipv4Address arpTargetAddress,
        ushort operation)
    {
        byte[] frame = new byte[ArpNeighborResolutionTransaction.RequestFrameLength];
        EthernetHeader header = new EthernetHeader(
            ethernetDestination,
            ethernetSource,
            EthernetHeader.EtherTypeArp);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(frame, header);
        PacketParseStatus arpStatus = ArpPacket.TryWrite(
            frame.AsSpan(EthernetHeader.HeaderLength, ArpPacket.PacketLength),
            new ArpPacket(operation, arpSenderMac, arpSenderAddress, arpTargetMac, arpTargetAddress),
            out int arpBytesWritten);

        if (ethernetStatus != PacketParseStatus.Success
            || arpStatus != PacketParseStatus.Success
            || arpBytesWritten != ArpPacket.PacketLength)
        {
            throw new InvalidOperationException("Failed to build deterministic ARP reply frame.");
        }

        return frame;
    }

    private static ArpNeighborResolutionTransaction CreateTransaction()
    {
        return ArpNeighborResolutionTransaction.Create(
            LocalMac(),
            LocalAddress(),
            TargetAddress());
    }

    private static MacAddress LocalMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x10);
    }

    private static MacAddress RemoteMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x20);
    }

    private static MacAddress BroadcastMac()
    {
        return new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
    }

    private static MacAddress ZeroMac()
    {
        return new MacAddress(0x00, 0x00, 0x00, 0x00, 0x00, 0x00);
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 10);
    }

    private static Ipv4Address TargetAddress()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }
}
