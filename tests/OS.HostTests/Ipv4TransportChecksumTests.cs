using System.Runtime.CompilerServices;
using OS.Net;

internal static class Ipv4TransportChecksumTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.transport.checksum computes udp ipv4 pseudo header", TestComputesUdpIpv4PseudoHeader, ref failures);
        Run("net.transport.checksum computes tcp ipv4 pseudo header", TestComputesTcpIpv4PseudoHeader, ref failures);
        Run("net.transport.checksum validates segmented payload", TestValidatesSegmentedPayload, ref failures);
        Run("net.transport.checksum rejects corrupted checksum", TestRejectsCorruptedChecksum, ref failures);
        Run("net.transport.checksum rejects invalid inputs", TestRejectsInvalidInputs, ref failures);
        Run("net.transport.checksum default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.transport.checksum preserves primitive boundary", TestPreservesPrimitiveBoundary, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "IPv4 transport checksum host tests failed: " + failures);
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

    private static bool TestComputesUdpIpv4PseudoHeader()
    {
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] datagram = CreateUdpDatagram(payload, checksum: 0);

        Ipv4TransportChecksumStatus status = Ipv4TransportChecksum.Compute(
            LocalAddress(),
            RemoteAddress(),
            Ipv4TransportChecksum.ProtocolUdp,
            datagram,
            out Ipv4TransportChecksumResult result);

        return status == Ipv4TransportChecksumStatus.Success
            && result.Succeeded
            && result.Protocol == Ipv4TransportChecksum.ProtocolUdp
            && result.TransportLength == UdpHeader.HeaderLength + payload.Length
            && result.Checksum == 0xE4E9
            && !result.IsValid;
    }

    private static bool TestComputesTcpIpv4PseudoHeader()
    {
        byte[] payload = { 0x64, 0x61, 0x74, 0x61 };
        byte[] segment = CreateTcpSegment(payload, checksum: 0);

        Ipv4TransportChecksumStatus status = Ipv4TransportChecksum.Compute(
            LocalAddress(),
            RemoteAddress(),
            Ipv4TransportChecksum.ProtocolTcp,
            segment,
            out Ipv4TransportChecksumResult result);

        return status == Ipv4TransportChecksumStatus.Success
            && result.Succeeded
            && result.Protocol == Ipv4TransportChecksum.ProtocolTcp
            && result.TransportLength == TcpHeader.MinHeaderLength + payload.Length
            && result.Checksum == 0x59A9;
    }

    private static bool TestValidatesSegmentedPayload()
    {
        byte[] payload = { 0x64, 0x61, 0x74, 0x61 };
        byte[] header = CreateTcpSegment(ReadOnlySpan<byte>.Empty, checksum: 0);
        ReadOnlySpan<byte> firstPayload = payload.AsSpan(0, 2);
        ReadOnlySpan<byte> secondPayload = payload.AsSpan(2, 2);

        Ipv4TransportChecksumStatus computeStatus = Ipv4TransportChecksum.Compute(
            LocalAddress(),
            RemoteAddress(),
            Ipv4TransportChecksum.ProtocolTcp,
            header,
            firstPayload,
            secondPayload,
            out Ipv4TransportChecksumResult computed);
        WriteChecksum(header, offset: 16, computed.Checksum);
        Ipv4TransportChecksumStatus validateStatus = Ipv4TransportChecksum.Validate(
            LocalAddress(),
            RemoteAddress(),
            Ipv4TransportChecksum.ProtocolTcp,
            header,
            payload,
            out Ipv4TransportChecksumResult validation);

        return computeStatus == Ipv4TransportChecksumStatus.Success
            && computed.Checksum == 0x59A9
            && validateStatus == Ipv4TransportChecksumStatus.Success
            && validation.Checksum == 0
            && validation.IsValid
            && validation.TransportLength == TcpHeader.MinHeaderLength + payload.Length;
    }

    private static bool TestRejectsCorruptedChecksum()
    {
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] datagram = CreateUdpDatagram(payload, checksum: 0xE4E9);
        datagram[^1] ^= 0x01;

        Ipv4TransportChecksumStatus status = Ipv4TransportChecksum.Validate(
            LocalAddress(),
            RemoteAddress(),
            Ipv4TransportChecksum.ProtocolUdp,
            datagram,
            out Ipv4TransportChecksumResult result);

        return status == Ipv4TransportChecksumStatus.Success
            && result.Succeeded
            && !result.IsValid
            && result.Checksum != 0;
    }

    private static bool TestRejectsInvalidInputs()
    {
        byte[] datagram = CreateUdpDatagram(new byte[] { 0x70 }, checksum: 0);
        Ipv4TransportChecksumStatus protocolStatus = Ipv4TransportChecksum.Compute(
            LocalAddress(),
            RemoteAddress(),
            protocol: 1,
            datagram,
            out Ipv4TransportChecksumResult protocolResult);
        Ipv4TransportChecksumStatus emptyStatus = Ipv4TransportChecksum.Compute(
            LocalAddress(),
            RemoteAddress(),
            Ipv4TransportChecksum.ProtocolUdp,
            ReadOnlySpan<byte>.Empty,
            out Ipv4TransportChecksumResult emptyResult);
        byte[] tooLong = new byte[ushort.MaxValue + 1];
        Ipv4TransportChecksumStatus lengthStatus = Ipv4TransportChecksum.Compute(
            LocalAddress(),
            RemoteAddress(),
            Ipv4TransportChecksum.ProtocolTcp,
            tooLong,
            out Ipv4TransportChecksumResult lengthResult);

        return protocolStatus == Ipv4TransportChecksumStatus.InvalidProtocol
            && !protocolResult.Succeeded
            && protocolResult.Protocol == 1
            && emptyStatus == Ipv4TransportChecksumStatus.InvalidLength
            && !emptyResult.Succeeded
            && emptyResult.TransportLength == 0
            && lengthStatus == Ipv4TransportChecksumStatus.InvalidLength
            && !lengthResult.Succeeded
            && lengthResult.TransportLength == tooLong.Length;
    }

    private static bool TestPreservesPrimitiveBoundary()
    {
        return Ipv4TransportChecksum.RequiresCallerOwnedTransportBuffers
            && !Ipv4TransportChecksum.AllocatesBuffers
            && !Ipv4TransportChecksum.ParsesPackets
            && !Ipv4TransportChecksum.ReadsNic
            && !Ipv4TransportChecksum.DispatchesSockets
            && !RuntimeHelpers.IsReferenceOrContainsReferences<Ipv4TransportChecksumResult>();
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        Ipv4TransportChecksumResult result = default;
        Ipv4TransportChecksumResult failed = Ipv4TransportChecksumResult.Fail(
            Ipv4TransportChecksumStatus.InvalidLength,
            Ipv4TransportChecksum.ProtocolUdp,
            transportLength: 0);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == Ipv4TransportChecksumStatus.Success
            && result.Protocol == 0
            && result.TransportLength == 0
            && result.Checksum == 0
            && !result.IsValid
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == Ipv4TransportChecksumStatus.InvalidLength
            && failed.Protocol == Ipv4TransportChecksum.ProtocolUdp
            && failed.TransportLength == 0
            && failed.Checksum == 0
            && !failed.IsValid
            && !RuntimeHelpers.IsReferenceOrContainsReferences<Ipv4TransportChecksumResult>();
    }

    private static byte[] CreateUdpDatagram(ReadOnlySpan<byte> payload, ushort checksum)
    {
        byte[] datagram = new byte[UdpHeader.HeaderLength + payload.Length];
        PacketParseStatus status = UdpHeader.TryWrite(
            datagram,
            sourcePort: 12345,
            destinationPort: 8080,
            payload,
            out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != datagram.Length)
        {
            throw new InvalidOperationException("Failed to build UDP datagram fixture.");
        }

        WriteChecksum(datagram, offset: 6, checksum);
        return datagram;
    }

    private static byte[] CreateTcpSegment(ReadOnlySpan<byte> payload, ushort checksum)
    {
        byte[] segment = new byte[TcpHeader.MinHeaderLength + payload.Length];
        PacketParseStatus status = TcpHeader.TryWrite(
            segment,
            sourcePort: 12345,
            destinationPort: 8080,
            sequenceNumber: 0x10203040,
            acknowledgementNumber: 0x50607080,
            flags: 0x18,
            windowSize: 0x4000,
            checksum,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            payload,
            out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != segment.Length)
        {
            throw new InvalidOperationException("Failed to build TCP segment fixture.");
        }

        return segment;
    }

    private static void WriteChecksum(byte[] segment, int offset, ushort checksum)
    {
        segment[offset] = (byte)(checksum >> 8);
        segment[offset + 1] = (byte)checksum;
    }

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 10);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 20);
    }
}
