namespace OS.Kernel.Runtime;
public enum CheckedReferenceBulkStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    HeapAuthorityRejected = 2,
    UnsupportedProfile = 3,
    InvalidAlignment = 4,
    InvalidLength = 5,
    InvalidRange = 6
}
