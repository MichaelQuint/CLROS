using OS.Std.Binary;

namespace OS.Kernel.Compatibility.LinuxX64;

public enum LinuxElfLoadStatus : byte
{
    Uninitialized = 0,
    Ready = 1,
    InvalidSource = 2,
    InvalidIdentification = 3,
    InvalidHeader = 4,
    InvalidProgramTable = 5,
    InterpreterRequired = 6,
    DynamicLinking = 7,
    ExecutableStack = 8,
    UnknownProgramType = 9,
    InvalidFileExtent = 10,
    InvalidLoad = 11,
    InvalidAlignment = 12,
    TooManyLoads = 13,
    OverlappingLoads = 14,
    InvalidImageSpan = 15,
    MemoryLimitExceeded = 16,
    InvalidEntry = 17,
    ProgramHeadersNotMapped = 18,
    DuplicateMetadata = 19,
    InvalidProgramHeaders = 20,
    InvalidTls = 21,
    InvalidRelro = 22,
    InvalidEhFrame = 23,
    InvalidResourceLimits = 24,
    InvalidProperties = 25
}
