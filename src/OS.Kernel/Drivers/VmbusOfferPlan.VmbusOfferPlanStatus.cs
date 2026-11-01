using OS.Std.Binary;

namespace OS.Drivers;
public enum VmbusOfferPlanStatus
{
    Success = 0,
    MalformedLength = 1,
    UnsupportedMessageType = 2,
    ZeroInterfaceTypeGuid = 3,
    ZeroInstanceGuid = 4,
    UnsupportedInterfaceTypeGuid = 5,
    ZeroChannelId = 6,
    MetadataTooLong = 7,
    MetadataContainsInvalidAscii = 8,
    DestinationTooSmall = 9,
    OfferParseFailed = 10,
    DuplicateChannelId = 11
}
