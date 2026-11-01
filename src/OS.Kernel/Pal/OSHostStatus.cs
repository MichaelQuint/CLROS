using System.Runtime.InteropServices;

namespace OS.Pal;
public enum OSHostStatus : int
{
    Ok = 0,
    InvalidArgument = 1,
    NotSupported = 2,
    NotFound = 3,
    AccessDenied = 4,
    BufferTooSmall = 5,
    OutOfMemory = 6,
    WouldBlock = 7,
    TimedOut = 8,
    Interrupted = 9,
    EndOfFile = 10,
    ConnectionReset = 11,
    ConnectionRefused = 12,
    AddressInUse = 13,
    AddressNotAvailable = 14,
    NetworkDown = 15,
    TryAgain = 16,
    IoError = 17,
    InternalError = 18,
    Closed = 19
}
