using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Pal;

internal static class PalStatusCategoryTests
{

    [HostTest("pal.status_categories maps canonical statuses", HostLane.Fast, 0)]
    private static bool TestMapsCanonicalStatuses()
    {
        foreach ((OSHostStatus status, OSHostStatusCategory category) in ExpectedCategories())
        {
            if (OSHostStatusCategoryFormatter.Categorize(status) != category)
            {
                Console.WriteLine("unexpected category for " + status);
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(true);
    }

    [HostTest("pal.status_categories formats category names", HostLane.Fast, 1)]
    private static bool TestFormatsCategoryNames()
    {
        Span<byte> destination = stackalloc byte[64];

        foreach ((OSHostStatusCategory category, string expectedName) in ExpectedNames())
        {
            destination.Fill(0);
            OSHostStatusCategoryFormatResult result =
                OSHostStatusCategoryFormatter.FormatCategory(category, destination);
            string actualName = Encoding.ASCII.GetString(destination[..(int)result.BytesWritten]);

            if (!result.Succeeded
                || result.Status != OSHostStatus.Ok
                || result.Category != category
                || result.RequiredBytes != (uint)expectedName.Length
                || actualName != expectedName)
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(true);
    }

    [HostTest("pal.status_categories reports required bytes", HostLane.Fast, 2)]
    private static bool TestReportsRequiredBytes()
    {
        OSHostStatusCategoryFormatResult result =
            OSHostStatusCategoryFormatter.QueryRequiredBytes(OSHostStatus.TimedOut);

        return HostCheck.Result(result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.Ok
            && result.Category == OSHostStatusCategory.RuntimeUnavailable
            && result.BytesWritten == 0
            && result.RequiredBytes == (uint)"OSHOST_STATUS_CATEGORY_RUNTIME_UNAVAILABLE".Length);
    }

    [HostTest("pal.status_categories rejects short buffer", HostLane.Fast, 3)]
    private static bool TestRejectsShortBuffer()
    {
        Span<byte> destination = stackalloc byte[3];
        destination.Fill(0xCC);

        OSHostStatusCategoryFormatResult result =
            OSHostStatusCategoryFormatter.Format(OSHostStatus.OutOfMemory, destination);

        return HostCheck.Result(result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.BufferTooSmall
            && result.Category == OSHostStatusCategory.ResourceError
            && result.BytesWritten == 0
            && result.RequiredBytes == (uint)"OSHOST_STATUS_CATEGORY_RESOURCE_ERROR".Length
            && destination[0] == 0xCC
            && destination[1] == 0xCC
            && destination[2] == 0xCC);
    }

    [HostTest("pal.status_categories maps unknown status", HostLane.Fast, 4)]
    private static bool TestMapsUnknownStatus()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xAA);

        OSHostStatusCategory category = OSHostStatusCategoryFormatter.Categorize((OSHostStatus)999);
        OSHostStatusCategoryFormatResult format =
            OSHostStatusCategoryFormatter.Format((OSHostStatus)(-1), destination);
        string actualName = Encoding.ASCII.GetString(destination[..(int)format.BytesWritten]);
        OSHostStatusCategoryFormatResult invalidCategory =
            OSHostStatusCategoryFormatter.FormatCategory((OSHostStatusCategory)99, destination);

        return HostCheck.Result(category == OSHostStatusCategory.Unknown
            && format.Succeeded
            && format.Status == OSHostStatus.Ok
            && format.Category == OSHostStatusCategory.Unknown
            && actualName == "OSHOST_STATUS_CATEGORY_UNKNOWN"
            && invalidCategory.IsInitialized
            && !invalidCategory.Succeeded
            && invalidCategory.Status == OSHostStatus.InvalidArgument
            && invalidCategory.Category == OSHostStatusCategory.Unknown);
    }

    [HostTest("pal.status_categories default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostStatusCategoryFormatResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.Ok
            && result.Category == OSHostStatusCategory.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.Flags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostStatusCategoryFormatResult>());
    }

    [HostTest("pal.status_categories uses shared ascii text builder", HostLane.Fast, 6)]
    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Pal", "OSHostStatusCategory.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, 0, name)",
                StringComparison.Ordinal)
            && !source.Contains("name.CopyTo(destination)", StringComparison.Ordinal));
    }

    private static (OSHostStatus Status, OSHostStatusCategory Category)[] ExpectedCategories()
    {
        return
        [
            (OSHostStatus.Ok, OSHostStatusCategory.Success),
            (OSHostStatus.InvalidArgument, OSHostStatusCategory.CallerError),
            (OSHostStatus.NotSupported, OSHostStatusCategory.Unsupported),
            (OSHostStatus.NotFound, OSHostStatusCategory.CallerError),
            (OSHostStatus.AccessDenied, OSHostStatusCategory.CallerError),
            (OSHostStatus.BufferTooSmall, OSHostStatusCategory.CallerError),
            (OSHostStatus.OutOfMemory, OSHostStatusCategory.ResourceError),
            (OSHostStatus.WouldBlock, OSHostStatusCategory.RuntimeUnavailable),
            (OSHostStatus.TimedOut, OSHostStatusCategory.RuntimeUnavailable),
            (OSHostStatus.Interrupted, OSHostStatusCategory.RuntimeUnavailable),
            (OSHostStatus.EndOfFile, OSHostStatusCategory.ResourceError),
            (OSHostStatus.ConnectionReset, OSHostStatusCategory.ResourceError),
            (OSHostStatus.ConnectionRefused, OSHostStatusCategory.ResourceError),
            (OSHostStatus.AddressInUse, OSHostStatusCategory.CallerError),
            (OSHostStatus.AddressNotAvailable, OSHostStatusCategory.CallerError),
            (OSHostStatus.NetworkDown, OSHostStatusCategory.RuntimeUnavailable),
            (OSHostStatus.TryAgain, OSHostStatusCategory.RuntimeUnavailable),
            (OSHostStatus.IoError, OSHostStatusCategory.ResourceError),
            (OSHostStatus.InternalError, OSHostStatusCategory.Unknown),
            (OSHostStatus.Closed, OSHostStatusCategory.CallerError)
        ];
    }

    private static (OSHostStatusCategory Category, string Name)[] ExpectedNames()
    {
        return
        [
            (OSHostStatusCategory.Success, "OSHOST_STATUS_CATEGORY_SUCCESS"),
            (OSHostStatusCategory.CallerError, "OSHOST_STATUS_CATEGORY_CALLER_ERROR"),
            (OSHostStatusCategory.ResourceError, "OSHOST_STATUS_CATEGORY_RESOURCE_ERROR"),
            (OSHostStatusCategory.Unsupported, "OSHOST_STATUS_CATEGORY_UNSUPPORTED"),
            (OSHostStatusCategory.RuntimeUnavailable, "OSHOST_STATUS_CATEGORY_RUNTIME_UNAVAILABLE"),
            (OSHostStatusCategory.Unknown, "OSHOST_STATUS_CATEGORY_UNKNOWN")
        ];
    }
}
