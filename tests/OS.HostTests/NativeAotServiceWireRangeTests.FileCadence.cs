using System.Security.Cryptography;
using System.Text;
using OS.Tooling;

internal static partial class NativeAotServiceWireRangeTests
{
    // Synthetic controls only. Reuses existing independent frame,
    // checksum, PCAP and nonuniform timestamp builders; no process/network work.
    private static Vector FileCadencePackets(bool wrap = false, bool retry = false,
        bool earlySend = false, bool omitLastAck = false, bool incomplete = false,
        bool changedBody = false, bool chunked = false, bool regressingAck = false,
        bool bodyHole = false)
    {
        var original = NativeAotSocketIntegrationTests.Packets();
        var frames = original.Frames.Take(8).Select(frame => frame.ToArray()).ToList();
        const ushort port = 43000;
        uint client = wrap ? uint.MaxValue - 4 : 1001;
        uint server = wrap ? uint.MaxValue - 10 : 2001;
        void Add(bool outgoing, uint sequence, uint ack, byte flags, ReadOnlySpan<byte> payload = default)
            => frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, port, sequence, ack, flags, payload));
        byte[] request = "GET /file HTTP/1.1\r\nHost: bench\r\n\r\n"u8.ToArray();
        byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 6\r\n" +
            (chunked ? "Transfer-Encoding: chunked\r\n" : "") + "\r\n");
        byte[] body = changedBody ? "abcdeX"u8.ToArray() : "abcdef"u8.ToArray();
        byte[] first = head.Concat(body.Take(2)).ToArray();
        Add(false, unchecked(client - 1), 0, 2);                         // 9
        Add(true, unchecked(server - 1), client, 0x12);                  // 10
        Add(false, client, server, 0x10);                               // 11
        Add(false, client, server, 0x18, request.AsSpan(0, 10));          // 12
        Add(false, unchecked(client + 10), server, 0x18, request.AsSpan(10)); // 13
        client = unchecked(client + (uint)request.Length);
        Add(true, server, client, 0x18, first);                         // 14
        uint firstEnd = unchecked(server + (uint)first.Length);
        if (retry)
        {
            Add(true, server, client, 0x18, first);                     // 15: full retry
            Add(true, unchecked(server + (uint)head.Length + 1), client, 0x18, "bc"u8); // 16
            Add(false, client, unchecked(firstEnd + 1), 0x10);          // 17
            Add(true, unchecked(firstEnd + 1), client, 0x18, "def"u8);  // 18
            Add(false, client, unchecked(server + (uint)head.Length + 6), 0x10); // 19
            Add(true, unchecked(server + (uint)head.Length), client, 0x18, body); // 20: precovered retry
        }
        else
        {
            byte[] tail = bodyHole ? body.AsSpan(4, 2).ToArray() : body.AsSpan(2, incomplete ? 2 : 4).ToArray();
            uint tailStart = bodyHole ? unchecked(firstEnd + 2) : firstEnd;
            if (earlySend)
            {
                Add(true, tailStart, client, 0x18, tail);               // 15
                Add(false, client, firstEnd, 0x10);                    // 16
            }
            else
            {
                Add(false, client, firstEnd, 0x10);                    // 15
                if (regressingAck)
                {
                    Add(false, client, firstEnd, 0x10);
                    Add(false, client, server, 0x10);
                }
                Add(true, tailStart, client, 0x18, tail);               // 16 normally
            }
            if (!omitLastAck)
                Add(false, client, unchecked(firstEnd + (uint)tail.Length), 0x10); // 17
        }
        // Deliberate capture EOF, without FIN/reset; no close helper is called.
        return new(frames, original.Anchor, original.Arp, port);
    }

    private static QemuTranscripts.FileWireCadence FileCadenceRead(Vector vector)
    {
        var target = new QemuTranscripts.FileWireTarget(vector.Port, "/file", 6,
            Convert.ToHexString(SHA256.HashData("abcdef"u8)));
        return QemuTranscripts.InspectNativeAotFileWireCadenceValues(
            Capture(vector), vector.Anchor, vector.Arp, [vector.Port], [target]).Single();
    }

    [HostTest("file wire cadence preserves original request entity ACK and next progress witnesses without FIN")]
    private static void FileCadenceNormal()
    {
        var result = FileCadenceRead(FileCadencePackets());
        HostCheck.True(result.EntityComplete && result.BodyStatus == "CompleteBodyHashVerified" &&
            result.VerifiedBodySha256 == result.Target.Sha256 && result.ObservedEntityBytes == 6);
        HostCheck.True(result.RequestFirst.Record == 12 && result.RequestComplete.Record == 13 &&
            result.FirstResponseData?.Record == 14 && result.FirstEntityByte?.Record == 14 &&
            result.LastEntityByte?.Record == 16);
        HostCheck.True(result.RequestCompleteToFirstResponseMicroseconds == 99 &&
            result.RequestLastPayloadRecord == 13 && result.RequestLastPayloadToFirstResponseMicroseconds == 99 &&
            result.CompleteEntitySpanMicroseconds == 198);
        HostCheck.Equal(2, result.Packets.Count);
        var first = result.Packets[0];
        HostCheck.True(first.Record == 14 && first.FirstCoveringAckRecord == 15 &&
            first.DataToAckMicroseconds == 99 && first.NextProgressRecord == 16 &&
            first.AckToNextProgressMicroseconds == 99 && !first.NextProgressBeforeAck);
        HostCheck.True(first.EntityProgressBytes == 2 && result.Packets[1].EntityProgressBytes == 4);
        HostCheck.True(result.ClientFinRecord is null && result.ServerFinRecord is null &&
            result.ResetRecord is null && result.CaptureEndedAtPcapEof && result.CaptureEndedWithOpenFlow);
    }

    [HostTest("file wire cadence handles sequence wrap full partial and already covered retransmissions")]
    private static void FileCadenceWrapAndRetries()
    {
        var result = FileCadenceRead(FileCadencePackets(wrap: true, retry: true));
        HostCheck.True(result.EntityComplete && result.ObservedEntityBytes == 6);
        HostCheck.Equal(5, result.Packets.Count);
        HostCheck.True(result.Packets[0].CrossesSequenceZero && result.Packets[0].StreamOffset == 0);
        var full = result.Packets[1];
        HostCheck.True(full.NovelTcpBytes == 0 && full.DuplicateTcpBytes == full.TcpPayloadBytes &&
            full.RetransmissionBeforeCoveringAck && full.FirstCoveringAckRecord == 17);
        var partial = result.Packets[2];
        HostCheck.True(partial.NovelTcpBytes == 1 && partial.DuplicateTcpBytes == 1 &&
            partial.EntityProgressBytes == 1 && partial.FirstCoveringAckRecord == 17);
        var precovered = result.Packets[4];
        HostCheck.True(precovered.AlreadyCoveredBeforeTransmission && precovered.NovelTcpBytes == 0 &&
            precovered.FirstCoveringAckRecord is null && precovered.DataToAckMicroseconds is null &&
            !precovered.MissingCoveringAck && !precovered.RetransmissionBeforeCoveringAck);
        HostCheck.True(result.Packets[0].NextProgressRecord == 16 && result.Packets[0].NextProgressBeforeAck);
    }

    [HostTest("file wire cadence preserves presend negative delta missing ACK and incomplete body at EOF")]
    private static void FileCadenceMissingAndPresend()
    {
        var complete = FileCadenceRead(FileCadencePackets(earlySend: true, omitLastAck: true));
        var first = complete.Packets[0];
        HostCheck.True(complete.EntityComplete && first.FirstCoveringAckRecord == 16 &&
            first.NextProgressRecord == 15 && first.NextProgressBeforeAck &&
            first.AckToNextProgressMicroseconds == -99);
        HostCheck.True(complete.Packets[1].MissingCoveringAck && complete.Packets[1].MissingNextProgress &&
            complete.Packets[1].DataToAckMicroseconds is null &&
            complete.Packets[1].AckToNextProgressMicroseconds is null);
        var partial = FileCadenceRead(FileCadencePackets(earlySend: true, omitLastAck: true, incomplete: true));
        HostCheck.True(!partial.EntityComplete && partial.ObservedEntityBytes == 4 &&
            partial.BodyStatus == "IncompleteNoFullBodyHash" && partial.VerifiedBodySha256 is null &&
            partial.CompleteEntitySpanMicroseconds is null && partial.CaptureEndedWithOpenFlow);
    }

    [HostTest("file wire cadence rejects complete changed body and unsupported HTTP transfer framing")]
    private static void FileCadenceChangedAndMalformed()
    {
        Reject(() => FileCadenceRead(FileCadencePackets(changedBody: true)), "file_wire_body_sha256");
        Reject(() => FileCadenceRead(FileCadencePackets(chunked: true)), "file_wire_response");
    }

    [HostTest("file wire cadence retains first coverage through duplicate and regressing ACKs")]
    private static void FileCadenceRegressingAcks()
    {
        var result = FileCadenceRead(FileCadencePackets(regressingAck: true));
        HostCheck.True(result.EntityComplete && result.IncomingAckPackets == 7 && result.NonAdvancingAckPackets == 5);
        var first = result.Packets[0];
        HostCheck.True(first.FirstCoveringAckRecord == 15 && first.DataToAckMicroseconds == 99 &&
            first.NextProgressRecord == 18 && !first.NextProgressBeforeAck);
    }

    [HostTest("file wire cadence reports internal body holes without hashing zero filled backing")]
    private static void FileCadenceBodyHole()
    {
        var result = FileCadenceRead(FileCadencePackets(earlySend: true, omitLastAck: true, bodyHole: true));
        HostCheck.True(!result.EntityComplete && result.ObservedEntityBytes == 4 &&
            result.VerifiedBodySha256 is null && result.CompleteEntitySpanMicroseconds is null);
        HostCheck.True(result.LastEntityByte is { } last && last.StreamOffset == result.ResponseHeaderBytes + 4 &&
            last.StreamOffset + last.ByteCount == result.ResponseHeaderBytes + result.Target.BodyLength);
    }
}
