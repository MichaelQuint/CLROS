namespace OS.Net;

using OS.Std.Binary;

public enum DhcpMessageStatus
{
    Success = 0,
    BufferTooSmall,
    TruncatedFixedHeader,
    MissingMagicCookie,
    UnsupportedOperation,
    UnsupportedHardwareType,
    UnsupportedHardwareAddressLength,
    MalformedOptions,
    UnsupportedMessageType
}
