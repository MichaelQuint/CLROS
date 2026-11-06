namespace OS.Kernel.Diagnostics;

public enum FaultEntryStubPlanStatus
{
    Ok = 0,
    UnsupportedVectorMetadata = 1,
    UnsupportedErrorCodeMetadata = 2,
    OutputBufferTooSmall = 3
}
