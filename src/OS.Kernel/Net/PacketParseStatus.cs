namespace OS.Net;

public enum PacketParseStatus
{
    Success = 0,
    BufferTooSmall,
    InvalidLength,
    UnsupportedEtherType,
    UnsupportedIpVersion,
    UnsupportedIpHeaderLength,
    UnsupportedIpProtocol,
    BadChecksum,
    TruncatedPayload,
    UnsupportedHardwareType,
    UnsupportedProtocolType,
    UnsupportedHardwareAddressLength,
    UnsupportedProtocolAddressLength,
    UnsupportedOperation,
    UnsupportedIcmpType,
    UnsupportedIcmpCode
}
