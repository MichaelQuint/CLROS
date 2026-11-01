namespace OS.Net;
public readonly struct Ipv4IcmpEchoFrameInfo
{
    public Ipv4IcmpEchoFrameInfo(MacAddress destinationMac, MacAddress sourceMac, Ipv4Address sourceAddress, Ipv4Address destinationAddress, byte timeToLive = 64, ushort identification = 0, ushort flagsAndFragmentOffset = 0)
    {
        DestinationMac = destinationMac;
        SourceMac = sourceMac;
        SourceAddress = sourceAddress;
        DestinationAddress = destinationAddress;
        TimeToLive = timeToLive;
        Identification = identification;
        FlagsAndFragmentOffset = flagsAndFragmentOffset;
    }

    public MacAddress DestinationMac { get; }
    public MacAddress SourceMac { get; }
    public Ipv4Address SourceAddress { get; }
    public Ipv4Address DestinationAddress { get; }
    public byte TimeToLive { get; }
    public ushort Identification { get; }
    public ushort FlagsAndFragmentOffset { get; }
}
