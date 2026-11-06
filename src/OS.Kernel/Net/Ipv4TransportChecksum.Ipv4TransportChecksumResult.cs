namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Net;

public readonly struct Ipv4TransportChecksumResult
{
    private Ipv4TransportChecksumResult(Ipv4TransportChecksumStatus status, byte protocol, int transportLength, ushort checksum, bool isValid)
    {
        IsInitialized = true;
        Status = status;
        Protocol = protocol;
        TransportLength = transportLength;
        Checksum = checksum;
        IsValid = isValid;
    }

    public bool IsInitialized
    {
        get;
    }
    public Ipv4TransportChecksumStatus Status
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == Ipv4TransportChecksumStatus.Success;
    public byte Protocol
    {
        get;
    }
    public int TransportLength
    {
        get;
    }
    public ushort Checksum
    {
        get;
    }
    public bool IsValid
    {
        get;
    }

    public static Ipv4TransportChecksumResult Success(byte protocol, int transportLength, ushort checksum, bool isValid)
    {
        return new Ipv4TransportChecksumResult(Ipv4TransportChecksumStatus.Success, protocol, transportLength, checksum, isValid);
    }

    public static Ipv4TransportChecksumResult Fail(Ipv4TransportChecksumStatus status, byte protocol, int transportLength)
    {
        return new Ipv4TransportChecksumResult(status, protocol, transportLength, checksum: 0, isValid: false);
    }
}
