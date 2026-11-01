namespace OS.Drivers;
using OS.Std.Binary;

public enum EnaAdminDescriptorStatus
{
    Success = 0,
    BufferTooSmall = 1,
    CommandIdOutOfRange = 2,
    PhysicalAddressOutOfRange = 3,
    CommandIdMismatch = 4,
    PhaseMismatch = 5,
    CompletionStatusNotSuccess = 6,
    CompletionEntryNotInitialized = 7
}
