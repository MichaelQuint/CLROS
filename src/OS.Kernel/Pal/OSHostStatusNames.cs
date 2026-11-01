using System.Runtime.InteropServices;

namespace OS.Pal;
public static class OSHostStatusNames
{
    public const string Unknown = "OSHOST_STATUS_UNKNOWN";
    public static bool IsDefined(int statusCode)
    {
        return statusCode is >= (int)OSHostStatus.Ok and <= (int)OSHostStatus.Closed;
    }

    public static string GetConstantName(OSHostStatus status)
    {
        return GetConstantName((int)status);
    }

    public static string GetConstantName(int statusCode)
    {
        return statusCode switch
        {
            (int)OSHostStatus.Ok => "OSHOST_STATUS_OK",
            (int)OSHostStatus.InvalidArgument => "OSHOST_STATUS_INVALID_ARGUMENT",
            (int)OSHostStatus.NotSupported => "OSHOST_STATUS_NOT_SUPPORTED",
            (int)OSHostStatus.NotFound => "OSHOST_STATUS_NOT_FOUND",
            (int)OSHostStatus.AccessDenied => "OSHOST_STATUS_ACCESS_DENIED",
            (int)OSHostStatus.BufferTooSmall => "OSHOST_STATUS_BUFFER_TOO_SMALL",
            (int)OSHostStatus.OutOfMemory => "OSHOST_STATUS_OUT_OF_MEMORY",
            (int)OSHostStatus.WouldBlock => "OSHOST_STATUS_WOULD_BLOCK",
            (int)OSHostStatus.TimedOut => "OSHOST_STATUS_TIMED_OUT",
            (int)OSHostStatus.Interrupted => "OSHOST_STATUS_INTERRUPTED",
            (int)OSHostStatus.EndOfFile => "OSHOST_STATUS_END_OF_FILE",
            (int)OSHostStatus.ConnectionReset => "OSHOST_STATUS_CONNECTION_RESET",
            (int)OSHostStatus.ConnectionRefused => "OSHOST_STATUS_CONNECTION_REFUSED",
            (int)OSHostStatus.AddressInUse => "OSHOST_STATUS_ADDR_IN_USE",
            (int)OSHostStatus.AddressNotAvailable => "OSHOST_STATUS_ADDR_NOT_AVAILABLE",
            (int)OSHostStatus.NetworkDown => "OSHOST_STATUS_NETWORK_DOWN",
            (int)OSHostStatus.TryAgain => "OSHOST_STATUS_TRY_AGAIN",
            (int)OSHostStatus.IoError => "OSHOST_STATUS_IO_ERROR",
            (int)OSHostStatus.InternalError => "OSHOST_STATUS_INTERNAL_ERROR",
            (int)OSHostStatus.Closed => "OSHOST_STATUS_CLOSED",
            _ => Unknown
        };
    }
}
