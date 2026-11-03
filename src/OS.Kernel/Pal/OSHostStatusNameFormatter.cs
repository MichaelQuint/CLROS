using OS.Std.Text;

namespace OS.Pal;

public static class OSHostStatusNameFormatter
{
    public static OSHostStatusNameFormatResult Format(OSHostStatus status, Span<byte> destination)
    {
        ReadOnlySpan<byte> name = GetCanonicalName(status);
        if (name.Length == 0)
        {
            return OSHostStatusNameFormatResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        if (destination.Length < name.Length)
        {
            return new OSHostStatusNameFormatResult(OSHostStatus.BufferTooSmall, bytesWritten: 0, requiredBytes: (uint)name.Length);
        }

        AsciiTextBuilderResult builderResult = AsciiTextBuilder.AppendBytes(destination, 0, name);
        if (!builderResult.Succeeded)
        {
            return OSHostStatusNameFormatResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return new OSHostStatusNameFormatResult(OSHostStatus.Ok, bytesWritten: (uint)builderResult.BytesWritten, requiredBytes: (uint)name.Length);
    }

    public static OSHostStatusNameFormatResult QueryRequiredBytes(OSHostStatus status)
    {
        ReadOnlySpan<byte> name = GetCanonicalName(status);
        if (name.Length == 0)
        {
            return OSHostStatusNameFormatResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return new OSHostStatusNameFormatResult(OSHostStatus.Ok, bytesWritten: 0, requiredBytes: (uint)name.Length);
    }

    private static ReadOnlySpan<byte> GetCanonicalName(OSHostStatus status)
    {
        return status switch
        {
            OSHostStatus.Ok => "OSHOST_STATUS_OK"u8,
            OSHostStatus.InvalidArgument => "OSHOST_STATUS_INVALID_ARGUMENT"u8,
            OSHostStatus.NotSupported => "OSHOST_STATUS_NOT_SUPPORTED"u8,
            OSHostStatus.NotFound => "OSHOST_STATUS_NOT_FOUND"u8,
            OSHostStatus.AccessDenied => "OSHOST_STATUS_ACCESS_DENIED"u8,
            OSHostStatus.BufferTooSmall => "OSHOST_STATUS_BUFFER_TOO_SMALL"u8,
            OSHostStatus.OutOfMemory => "OSHOST_STATUS_OUT_OF_MEMORY"u8,
            OSHostStatus.WouldBlock => "OSHOST_STATUS_WOULD_BLOCK"u8,
            OSHostStatus.TimedOut => "OSHOST_STATUS_TIMED_OUT"u8,
            OSHostStatus.Interrupted => "OSHOST_STATUS_INTERRUPTED"u8,
            OSHostStatus.EndOfFile => "OSHOST_STATUS_END_OF_FILE"u8,
            OSHostStatus.ConnectionReset => "OSHOST_STATUS_CONNECTION_RESET"u8,
            OSHostStatus.ConnectionRefused => "OSHOST_STATUS_CONNECTION_REFUSED"u8,
            OSHostStatus.AddressInUse => "OSHOST_STATUS_ADDRESS_IN_USE"u8,
            OSHostStatus.AddressNotAvailable => "OSHOST_STATUS_ADDRESS_NOT_AVAILABLE"u8,
            OSHostStatus.NetworkDown => "OSHOST_STATUS_NETWORK_DOWN"u8,
            OSHostStatus.TryAgain => "OSHOST_STATUS_TRY_AGAIN"u8,
            OSHostStatus.IoError => "OSHOST_STATUS_IO_ERROR"u8,
            OSHostStatus.InternalError => "OSHOST_STATUS_INTERNAL_ERROR"u8,
            OSHostStatus.Closed => "OSHOST_STATUS_CLOSED"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }
}
