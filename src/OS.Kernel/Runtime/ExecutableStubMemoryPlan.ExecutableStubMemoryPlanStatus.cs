namespace OS.Kernel.Runtime;
public enum ExecutableStubMemoryPlanStatus
{
    Success = 0,
    InvalidPayloadLength = 1,
    InvalidPageSize = 2,
    InvalidAlignment = 3,
    AlignmentGreaterThanPageSize = 4,
    AccidentalWritableExecutable = 5,
    PayloadLengthOverflow = 6
}
