using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Pal;

internal static class PalStatusNameFormatterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("pal.status_names formats all canonical statuses", TestFormatsAllCanonicalStatuses, ref failures);
        Run("pal.status_names reports required bytes", TestReportsRequiredBytes, ref failures);
        Run("pal.status_names rejects short buffer", TestRejectsShortBuffer, ref failures);
        Run("pal.status_names rejects unknown status", TestRejectsUnknownStatus, ref failures);
        Run("pal.status_names preserves caller buffer on failure", TestPreservesCallerBufferOnFailure, ref failures);
        Run("pal.status_names default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("pal.status_names uses shared ascii text builder", TestUsesSharedAsciiTextBuilder, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("PAL status-name formatter host tests failed: " + failures);
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

    private static bool TestFormatsAllCanonicalStatuses()
    {
        Span<byte> destination = stackalloc byte[64];

        foreach ((OSHostStatus status, string expectedName) in ExpectedNames())
        {
            destination.Fill(0);
            OSHostStatusNameFormatResult result = OSHostStatusNameFormatter.Format(status, destination);
            string actualName = Encoding.ASCII.GetString(destination[..(int)result.BytesWritten]);

            if (!result.Succeeded
                || result.Status != OSHostStatus.Ok
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
        OSHostStatusNameFormatResult result =
            OSHostStatusNameFormatter.QueryRequiredBytes(OSHostStatus.AddressNotAvailable);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.Ok
            && result.BytesWritten == 0
            && result.RequiredBytes == (uint)"OSHOST_STATUS_ADDRESS_NOT_AVAILABLE".Length;
    }

    private static bool TestRejectsShortBuffer()
    {
        Span<byte> destination = stackalloc byte[3];
        destination.Fill(0xCC);

        OSHostStatusNameFormatResult result =
            OSHostStatusNameFormatter.Format(OSHostStatus.BufferTooSmall, destination);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.BufferTooSmall
            && result.BytesWritten == 0
            && result.RequiredBytes == (uint)"OSHOST_STATUS_BUFFER_TOO_SMALL".Length
            && destination[0] == 0xCC
            && destination[1] == 0xCC
            && destination[2] == 0xCC;
    }

    private static bool TestRejectsUnknownStatus()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xAA);

        OSHostStatusNameFormatResult format =
            OSHostStatusNameFormatter.Format((OSHostStatus)999, destination);
        OSHostStatusNameFormatResult query =
            OSHostStatusNameFormatter.QueryRequiredBytes((OSHostStatus)(-1));

        return format.IsInitialized
            && !format.Succeeded
            && format.Status == OSHostStatus.InvalidArgument
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && query.Status == OSHostStatus.InvalidArgument
            && query.RequiredBytes == 0
            && destination[0] == 0xAA;
    }

    private static bool TestPreservesCallerBufferOnFailure()
    {
        Span<byte> shortDestination = stackalloc byte[2];
        shortDestination[0] = 0x31;
        shortDestination[1] = 0x32;

        Span<byte> unknownDestination = stackalloc byte[2];
        unknownDestination[0] = 0x41;
        unknownDestination[1] = 0x42;

        OSHostStatusNameFormatResult shortResult =
            OSHostStatusNameFormatter.Format(OSHostStatus.Ok, shortDestination);
        OSHostStatusNameFormatResult unknownResult =
            OSHostStatusNameFormatter.Format((OSHostStatus)128, unknownDestination);

        return shortResult.Status == OSHostStatus.BufferTooSmall
            && unknownResult.Status == OSHostStatus.InvalidArgument
            && shortDestination[0] == 0x31
            && shortDestination[1] == 0x32
            && unknownDestination[0] == 0x41
            && unknownDestination[1] == 0x42;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        OSHostStatusNameFormatResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == OSHostStatus.Ok
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.Flags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<OSHostStatusNameFormatResult>();
    }

    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(repositoryRoot, "src", "OS.Kernel", "Pal", "OSHostStatusNameFormatter.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, 0, name)",
                StringComparison.Ordinal)
            && !source.Contains("name.CopyTo(destination)", StringComparison.Ordinal);
    }

    private static (OSHostStatus Status, string Name)[] ExpectedNames()
    {
        return
        [
            (OSHostStatus.Ok, "OSHOST_STATUS_OK"),
            (OSHostStatus.InvalidArgument, "OSHOST_STATUS_INVALID_ARGUMENT"),
            (OSHostStatus.NotSupported, "OSHOST_STATUS_NOT_SUPPORTED"),
            (OSHostStatus.NotFound, "OSHOST_STATUS_NOT_FOUND"),
            (OSHostStatus.AccessDenied, "OSHOST_STATUS_ACCESS_DENIED"),
            (OSHostStatus.BufferTooSmall, "OSHOST_STATUS_BUFFER_TOO_SMALL"),
            (OSHostStatus.OutOfMemory, "OSHOST_STATUS_OUT_OF_MEMORY"),
            (OSHostStatus.WouldBlock, "OSHOST_STATUS_WOULD_BLOCK"),
            (OSHostStatus.TimedOut, "OSHOST_STATUS_TIMED_OUT"),
            (OSHostStatus.Interrupted, "OSHOST_STATUS_INTERRUPTED"),
            (OSHostStatus.EndOfFile, "OSHOST_STATUS_END_OF_FILE"),
            (OSHostStatus.ConnectionReset, "OSHOST_STATUS_CONNECTION_RESET"),
            (OSHostStatus.ConnectionRefused, "OSHOST_STATUS_CONNECTION_REFUSED"),
            (OSHostStatus.AddressInUse, "OSHOST_STATUS_ADDRESS_IN_USE"),
            (OSHostStatus.AddressNotAvailable, "OSHOST_STATUS_ADDRESS_NOT_AVAILABLE"),
            (OSHostStatus.NetworkDown, "OSHOST_STATUS_NETWORK_DOWN"),
            (OSHostStatus.TryAgain, "OSHOST_STATUS_TRY_AGAIN"),
            (OSHostStatus.IoError, "OSHOST_STATUS_IO_ERROR"),
            (OSHostStatus.InternalError, "OSHOST_STATUS_INTERNAL_ERROR"),
            (OSHostStatus.Closed, "OSHOST_STATUS_CLOSED")
        ];
    }
}
