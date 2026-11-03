using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiSdtParseStatus
{
    Success = 0,
    SourceTooShort = 1,
    UnsupportedSignature = 2,
    DeclaredLengthTooSmall = 3,
    SourceTooShortForDeclaredLength = 4,
    InvalidChecksum = 5,
    InvalidPointerArrayLength = 6,
    ZeroTablePointer = 7,
    DestinationTooSmall = 8
}
