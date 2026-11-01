using OS.Pal;

namespace OS.Kernel.Runtime;
public enum NativeAppExitLogObserverStatus
{
    Success = 0,
    NoExitMarker = 1,
    MalformedExitMarker = 2,
    InvalidLogTable = 3,
    InvalidRecord = 4,
    RecordBufferTooSmall = 5
}
