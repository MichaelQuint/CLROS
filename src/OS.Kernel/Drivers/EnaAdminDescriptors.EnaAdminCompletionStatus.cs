namespace OS.Drivers;
using OS.Std.Binary;

public enum EnaAdminCompletionStatus : byte
{
    Success = 0,
    ResourceAllocationFailure = 1,
    BadOpcode = 2,
    UnsupportedOpcode = 3,
    MalformedRequest = 4,
    IllegalParameter = 5,
    UnknownError = 6,
    ResourceBusy = 7
}
