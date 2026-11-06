namespace OS.Std.Text;

public enum AsciiArgumentStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidInput = 2,
    InputTooLong = 3,
    InvalidCharacter = 4,
    BackslashNotSupported = 5,
    UnbalancedQuotes = 6,
    TooManyArguments = 7,
    InvalidOutput = 8,
    OutputTooSmall = 9,
    InvalidSlices = 10,
    SlicesTooSmall = 11,
    OverlappingStorage = 12
}
