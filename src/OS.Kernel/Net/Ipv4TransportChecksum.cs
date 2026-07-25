namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Net;

public enum Ipv4TransportChecksumStatus
{
    Success = 0,
    InvalidProtocol = 1,
    InvalidLength = 2
}

public readonly struct Ipv4TransportChecksumResult
{
    private Ipv4TransportChecksumResult(
        Ipv4TransportChecksumStatus status,
        byte protocol,
        int transportLength,
        ushort checksum,
        bool isValid)
    {
        IsInitialized = true;
        Status = status;
        Protocol = protocol;
        TransportLength = transportLength;
        Checksum = checksum;
        IsValid = isValid;
    }

    public bool IsInitialized { get; }

    public Ipv4TransportChecksumStatus Status { get; }

    public bool Succeeded => IsInitialized && Status == Ipv4TransportChecksumStatus.Success;

    public byte Protocol { get; }

    public int TransportLength { get; }

    public ushort Checksum { get; }

    public bool IsValid { get; }

    public static Ipv4TransportChecksumResult Success(
        byte protocol,
        int transportLength,
        ushort checksum,
        bool isValid)
    {
        return new Ipv4TransportChecksumResult(
            Ipv4TransportChecksumStatus.Success,
            protocol,
            transportLength,
            checksum,
            isValid);
    }

    public static Ipv4TransportChecksumResult Fail(
        Ipv4TransportChecksumStatus status,
        byte protocol,
        int transportLength)
    {
        return new Ipv4TransportChecksumResult(
            status,
            protocol,
            transportLength,
            checksum: 0,
            isValid: false);
    }
}

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

    public static Ipv4TransportChecksumStatus Compute(
        Ipv4Address source,
        Ipv4Address destination,
        byte protocol,
        ReadOnlySpan<byte> transportSegment,
        out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(
            source,
            destination,
            protocol,
            transportSegment,
            ReadOnlySpan<byte>.Empty,
            ReadOnlySpan<byte>.Empty,
            validate: false,
            out result);
    }

    public static Ipv4TransportChecksumStatus Compute(
        Ipv4Address source,
        Ipv4Address destination,
        byte protocol,
        ReadOnlySpan<byte> transportHeader,
        ReadOnlySpan<byte> payload,
        out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(
            source,
            destination,
            protocol,
            transportHeader,
            payload,
            ReadOnlySpan<byte>.Empty,
            validate: false,
            out result);
    }

    public static Ipv4TransportChecksumStatus Compute(
        Ipv4Address source,
        Ipv4Address destination,
        byte protocol,
        ReadOnlySpan<byte> transportHeader,
        ReadOnlySpan<byte> firstPayload,
        ReadOnlySpan<byte> secondPayload,
        out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(
            source,
            destination,
            protocol,
            transportHeader,
            firstPayload,
            secondPayload,
            validate: false,
            out result);
    }

    public static Ipv4TransportChecksumStatus Validate(
        Ipv4Address source,
        Ipv4Address destination,
        byte protocol,
        ReadOnlySpan<byte> transportSegment,
        out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(
            source,
            destination,
            protocol,
            transportSegment,
            ReadOnlySpan<byte>.Empty,
            ReadOnlySpan<byte>.Empty,
            validate: true,
            out result);
    }

    public static Ipv4TransportChecksumStatus Validate(
        Ipv4Address source,
        Ipv4Address destination,
        byte protocol,
        ReadOnlySpan<byte> transportHeader,
        ReadOnlySpan<byte> payload,
        out Ipv4TransportChecksumResult result)
    {
        return ComputeCore(
            source,
            destination,
            protocol,
            transportHeader,
            payload,
            ReadOnlySpan<byte>.Empty,
            validate: true,
            out result);
    }

    private static Ipv4TransportChecksumStatus ComputeCore(
        Ipv4Address source,
        Ipv4Address destination,
        byte protocol,
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third,
        bool validate,
        out Ipv4TransportChecksumResult result)
    {
        if (protocol != ProtocolTcp && protocol != ProtocolUdp)
        {
            result = Ipv4TransportChecksumResult.Fail(
                Ipv4TransportChecksumStatus.InvalidProtocol,
                protocol,
                transportLength: first.Length + second.Length + third.Length);
            return result.Status;
        }

        int transportLength = first.Length + second.Length + third.Length;
        if (transportLength == 0 || transportLength > ushort.MaxValue)
        {
            result = Ipv4TransportChecksumResult.Fail(
                Ipv4TransportChecksumStatus.InvalidLength,
                protocol,
                transportLength);
            return result.Status;
        }

        Span<byte> pseudoHeader = stackalloc byte[PseudoHeaderLength];
        source.TryWrite(pseudoHeader, 0);
        destination.TryWrite(pseudoHeader, 4);
        pseudoHeader[8] = 0;
        pseudoHeader[9] = protocol;
        EndianPrimitives.WriteUInt16BigEndian(
            pseudoHeader.Slice(10, sizeof(ushort)),
            (ushort)transportLength);

        InternetChecksumAccumulator accumulator = InternetChecksum.CreateAccumulator();
        accumulator.Update(pseudoHeader);
        accumulator.Update(first);
        accumulator.Update(second);
        accumulator.Update(third);

        ushort checksum = accumulator.Finish();
        result = Ipv4TransportChecksumResult.Success(
            protocol,
            transportLength,
            checksum,
            validate && checksum == InternetChecksum.ValidChecksumResult);
        return result.Status;
    }
}
