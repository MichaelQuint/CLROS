using OS.Std.Binary;

namespace OS.Boot;
public enum AcpiRsdpValidationStatus
{
    Success = 0,
    SourceTooShort = 1,
    InvalidSignature = 2,
    InvalidOemId = 3,
    InvalidBaseChecksum = 4,
    UnsupportedRevision = 5,
    ExtendedHeaderTooShort = 6,
    ExtendedLengthSmallerThanBase = 7,
    ExtendedLengthSmallerThanHeader = 8,
    SourceTooShortForExtendedLength = 9,
    InvalidExtendedChecksum = 10
}
