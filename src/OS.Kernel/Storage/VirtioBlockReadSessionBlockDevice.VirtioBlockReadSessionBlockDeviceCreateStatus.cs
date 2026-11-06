namespace OS.Storage;

public enum VirtioBlockReadSessionBlockDeviceCreateStatus
{
    Success = 0,
    IncompleteReadSession = 1,
    InvalidBlockGeometry = 2,
    MismatchedCompletedByteCount = 3,
    BackingBytesTooSmall = 4
}
