using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Pal;

internal static class PalStatusCategoryTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.status_categories maps canonical statuses", TestMapsCanonicalStatuses, ref failures);
        Run("pal.status_categories formats category names", TestFormatsCategoryNames, ref failures);
        Run("pal.status_categories reports required bytes", TestReportsRequiredBytes, ref failures);
        Run("pal.status_categories rejects short buffer", TestRejectsShortBuffer, ref failures);
        Run("pal.status_categories maps unknown status", TestMapsUnknownStatus, ref failures);
        Run("pal.status_categories default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("pal.status_categories uses shared ascii text builder", TestUsesSharedAsciiTextBuilder, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL status-category host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestMapsCanonicalStatuses()
    {
        foreach ((OSHostStatus status, OSHostStatusCategory category) in ExpectedCategories())
        {
            if (OSHostStatusCategoryFormatter.Categorize(status) != category)
            {
                Console.WriteLine("unexpected category for " + status);
                return false;
            }
        }

        return true;
    }

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
                return false;
            }
        }

        return true;
    }

    private static bool TestReportsRequiredBytes()
    {
        OSHostStatusCategoryFormatResult result =
            OSHostStatusCategoryFormatter.QueryRequiredBytes(OSHostStatus.TimedOut);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.Ok
            && result.Category == OSHostStatusCategory.RuntimeUnavailable
            && result.BytesWritten == 0
            && result.RequiredBytes == (uint)"OSHOST_STATUS_CATEGORY_RUNTIME_UNAVAILABLE".Length;
    }

    private static bool TestRejectsShortBuffer()
    {
        Span<byte> destination = stackalloc byte[3];
        destination.Fill(0xCC);

        OSHostStatusCategoryFormatResult result =
            OSHostStatusCategoryFormatter.Format(OSHostStatus.OutOfMemory, destination);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.BufferTooSmall
            && result.Category == OSHostStatusCategory.ResourceError
            && result.BytesWritten == 0
            && result.RequiredBytes == (uint)"OSHOST_STATUS_CATEGORY_RESOURCE_ERROR".Length
            && destination[0] == 0xCC
            && destination[1] == 0xCC
            && destination[2] == 0xCC;
    }

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

        return category == OSHostStatusCategory.Unknown
            && format.Succeeded
            && format.Status == OSHostStatus.Ok
            && format.Category == OSHostStatusCategory.Unknown
            && actualName == "OSHOST_STATUS_CATEGORY_UNKNOWN"
            && invalidCategory.IsInitialized
            && !invalidCategory.Succeeded
            && invalidCategory.Status == OSHostStatus.InvalidArgument
            && invalidCategory.Category == OSHostStatusCategory.Unknown;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostStatusCategoryFormatResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.Ok
            && result.Category == OSHostStatusCategory.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.Flags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostStatusCategoryFormatResult>();
    }

    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Pal", "OSHostStatusCategory.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, 0, name)",
                StringComparison.Ordinal)
            && !source.Contains("name.CopyTo(destination)", StringComparison.Ordinal);
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
