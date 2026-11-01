namespace OS.Net;
public enum DhcpLeaseConfigurationStatus
{
    Success = 0,
    MissingAddress = 1,
    MissingServerIdentifier = 2,
    ZeroLeaseTime = 3,
    InvalidSubnetMask = 4
}
