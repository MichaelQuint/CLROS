namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Net;

public static class Ipv4TransportChecksum
{
    public const byte ProtocolTcp = 6;
    public const byte ProtocolUdp = Ipv4Header.ProtocolUdp;
    public const int PseudoHeaderLength = 12;
    public const bool RequiresCallerOwnedTransportBuffers = true;
    public const bool AllocatesBuffers = false;
    public const bool ParsesPackets = false;
    public const bool ReadsNic = false;
    public const bool DispatchesSockets = false;
    public static Ipv4TransportChecksumStatus Compute(Ipv4Address source, Ipv4Address destination, byte protocol, ReadOnlySpan<byte> transportSegment, out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(source, destination, protocol, transportSegment, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, validate: false, out result);
    }

    public static Ipv4TransportChecksumStatus Compute(Ipv4Address source, Ipv4Address destination, byte protocol, ReadOnlySpan<byte> transportHeader, ReadOnlySpan<byte> payload, out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(source, destination, protocol, transportHeader, payload, ReadOnlySpan<byte>.Empty, validate: false, out result);
    }

    public static Ipv4TransportChecksumStatus Compute(Ipv4Address source, Ipv4Address destination, byte protocol, ReadOnlySpan<byte> transportHeader, ReadOnlySpan<byte> firstPayload, ReadOnlySpan<byte> secondPayload, out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(source, destination, protocol, transportHeader, firstPayload, secondPayload, validate: false, out result);
    }

    public static Ipv4TransportChecksumStatus Validate(Ipv4Address source, Ipv4Address destination, byte protocol, ReadOnlySpan<byte> transportSegment, out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(source, destination, protocol, transportSegment, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, validate: true, out result);
    }

    public static Ipv4TransportChecksumStatus Validate(Ipv4Address source, Ipv4Address destination, byte protocol, ReadOnlySpan<byte> transportHeader, ReadOnlySpan<byte> payload, out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(source, destination, protocol, transportHeader, payload, ReadOnlySpan<byte>.Empty, validate: true, out result);
    }

    private static Ipv4TransportChecksumStatus ComputeCore(Ipv4Address source, Ipv4Address destination, byte protocol, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, ReadOnlySpan<byte> third, bool validate, out Ipv4TransportChecksumResult result)
    {
        if (protocol != ProtocolTcp && protocol != ProtocolUdp)
        {
            result = Ipv4TransportChecksumResult.Fail(Ipv4TransportChecksumStatus.InvalidProtocol, protocol, transportLength: first.Length + second.Length + third.Length);
            return result.Status;
        }

        int transportLength = first.Length + second.Length + third.Length;
        if (transportLength == 0 || transportLength > ushort.MaxValue)
        {
            result = Ipv4TransportChecksumResult.Fail(Ipv4TransportChecksumStatus.InvalidLength, protocol, transportLength);
            return result.Status;
        }

        Span<byte> pseudoHeader = stackalloc byte[PseudoHeaderLength];
        source.TryWrite(pseudoHeader, 0);
        destination.TryWrite(pseudoHeader, 4);
        pseudoHeader[8] = 0;
        pseudoHeader[9] = protocol;
        EndianPrimitives.WriteUInt16BigEndian(pseudoHeader.Slice(10, sizeof(ushort)), (ushort)transportLength);
        InternetChecksumAccumulator accumulator = InternetChecksum.CreateAccumulator();
        accumulator.Update(pseudoHeader);
        accumulator.Update(first);
        accumulator.Update(second);
        accumulator.Update(third);
        ushort checksum = accumulator.Finish();
        result = Ipv4TransportChecksumResult.Success(protocol, transportLength, checksum, validate && checksum == InternetChecksum.ValidChecksumResult);
        return result.Status;
    }
}
