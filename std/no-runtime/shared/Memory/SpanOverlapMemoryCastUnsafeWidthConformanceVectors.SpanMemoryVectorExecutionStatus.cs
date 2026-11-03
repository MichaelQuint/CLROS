namespace OS.Std.Memory;

public enum SpanMemoryVectorExecutionStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ExpectedRejection = 2
}
