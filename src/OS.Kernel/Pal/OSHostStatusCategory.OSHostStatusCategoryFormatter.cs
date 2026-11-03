using OS.Std.Text;

namespace OS.Pal;

public static class OSHostStatusCategoryFormatter
{
    public static OSHostStatusCategory Categorize(OSHostStatus status)
    {
        return status switch
        {
            OSHostStatus.Ok => OSHostStatusCategory.Success,
            OSHostStatus.InvalidArgument or OSHostStatus.NotFound or OSHostStatus.AccessDenied or OSHostStatus.BufferTooSmall or OSHostStatus.AddressInUse or OSHostStatus.AddressNotAvailable or OSHostStatus.Closed => OSHostStatusCategory.CallerError,
            OSHostStatus.OutOfMemory or OSHostStatus.EndOfFile or OSHostStatus.ConnectionReset or OSHostStatus.ConnectionRefused or OSHostStatus.IoError => OSHostStatusCategory.ResourceError,
            OSHostStatus.NotSupported => OSHostStatusCategory.Unsupported,
            OSHostStatus.WouldBlock or OSHostStatus.TimedOut or OSHostStatus.Interrupted or OSHostStatus.NetworkDown or OSHostStatus.TryAgain => OSHostStatusCategory.RuntimeUnavailable,
            OSHostStatus.InternalError => OSHostStatusCategory.Unknown,
            _ => OSHostStatusCategory.Unknown
        };
    }

    public static OSHostStatusCategoryFormatResult Format(OSHostStatus status, Span<byte> destination)
    {
        return FormatCategory(Categorize(status), destination);
    }

    public static OSHostStatusCategoryFormatResult QueryRequiredBytes(OSHostStatus status)
    {
        return QueryCategoryRequiredBytes(Categorize(status));
    }

    public static OSHostStatusCategoryFormatResult FormatCategory(OSHostStatusCategory category, Span<byte> destination)
    {
        ReadOnlySpan<byte> name = GetCanonicalName(category);
        if (name.Length == 0)
        {
            return OSHostStatusCategoryFormatResult.FromStatus(OSHostStatus.InvalidArgument, OSHostStatusCategory.Unknown);
        }

        if (destination.Length < name.Length)
        {
            return new OSHostStatusCategoryFormatResult(OSHostStatus.BufferTooSmall, category, bytesWritten: 0, requiredBytes: (uint)name.Length);
        }

        AsciiTextBuilderResult builderResult = AsciiTextBuilder.AppendBytes(destination, 0, name);
        if (!builderResult.Succeeded)
        {
            return OSHostStatusCategoryFormatResult.FromStatus(OSHostStatus.InvalidArgument, OSHostStatusCategory.Unknown);
        }

        return new OSHostStatusCategoryFormatResult(OSHostStatus.Ok, category, bytesWritten: (uint)builderResult.BytesWritten, requiredBytes: (uint)name.Length);
    }

    public static OSHostStatusCategoryFormatResult QueryCategoryRequiredBytes(OSHostStatusCategory category)
    {
        ReadOnlySpan<byte> name = GetCanonicalName(category);
        if (name.Length == 0)
        {
            return OSHostStatusCategoryFormatResult.FromStatus(OSHostStatus.InvalidArgument, OSHostStatusCategory.Unknown);
        }

        return new OSHostStatusCategoryFormatResult(OSHostStatus.Ok, category, bytesWritten: 0, requiredBytes: (uint)name.Length);
    }

    private static ReadOnlySpan<byte> GetCanonicalName(OSHostStatusCategory category)
    {
        return category switch
        {
            OSHostStatusCategory.Success => "OSHOST_STATUS_CATEGORY_SUCCESS"u8,
            OSHostStatusCategory.CallerError => "OSHOST_STATUS_CATEGORY_CALLER_ERROR"u8,
            OSHostStatusCategory.ResourceError => "OSHOST_STATUS_CATEGORY_RESOURCE_ERROR"u8,
            OSHostStatusCategory.Unsupported => "OSHOST_STATUS_CATEGORY_UNSUPPORTED"u8,
            OSHostStatusCategory.RuntimeUnavailable => "OSHOST_STATUS_CATEGORY_RUNTIME_UNAVAILABLE"u8,
            OSHostStatusCategory.Unknown => "OSHOST_STATUS_CATEGORY_UNKNOWN"u8,
            _ => ReadOnlySpan<byte>.Empty
        };
    }
}
