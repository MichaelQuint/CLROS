using OS.Drivers;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiMcfgParseStatus
{
    Success = 0,
    SourceTooShort = 1,
    InvalidSignature = 2,
    DeclaredLengthTooSmall = 3,
    SourceTooShortForDeclaredLength = 4,
    InvalidReservedBytes = 5,
    TruncatedAllocationEntry = 6,
    DestinationTooSmall = 7,
    InvalidBusRange = 8
}
