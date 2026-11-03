using OS.Manifest;

namespace OS.Kernel.Runtime;

public enum NativeAppArgumentHandoffPlanStatus
{
    Success = 0,
    InvocationPlanNotInitialized = 1,
    ArgumentVectorNotInitialized = 2,
    InvocationPlanFailed = 3,
    ArgumentVectorFailed = 4,
    ImpossibleArgumentCount = 5,
    InvalidArgumentMetadata = 6,
    ParsedArgumentBytesMismatch = 7,
    ParsedArgumentBytesOverflow = 8
}
