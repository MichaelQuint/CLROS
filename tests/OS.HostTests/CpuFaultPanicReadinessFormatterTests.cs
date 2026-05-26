using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static class CpuFaultPanicReadinessFormatterTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostics.cpu_fault_panic formatter formats ready marker", TestFormatsReadyMarker, ref failures);
        Run("diagnostics.cpu_fault_panic formatter formats not-ready marker", TestFormatsNotReadyMarker, ref failures);
        Run("diagnostics.cpu_fault_panic formatter reports short destination", TestReportsShortDestination, ref failures);
        Run("diagnostics.cpu_fault_panic formatter rejects invalid status", TestRejectsInvalidStatus, ref failures);
        Run("diagnostics.cpu_fault_panic formatter uses shared ascii text builder", TestUsesSharedAsciiTextBuilder, ref failures);
        Run("diagnostics.cpu_fault_panic formatter default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("CPU fault panic readiness formatter tests failed: " + failures);
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

    private static bool TestFormatsReadyMarker()
    {
        Span<byte> destination = stackalloc byte[CpuFaultPanicReadinessFormatter.MaximumMarkerBytes];

        CpuFaultPanicReadinessMarkerFormatResult result =
            CpuFaultPanicReadinessFormatter.TryFormatReadyMarker(destination);

        string marker = Encoding.ASCII.GetString(destination[..result.BytesWritten]);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == CpuFaultPanicReadinessMarkerFormatStatus.Success
            && result.BytesWritten == BootSequence.CpuFaultPanicReady.Length
            && result.RequiredBytes == BootSequence.CpuFaultPanicReady.Length
            && marker == BootSequence.CpuFaultPanicReady;
    }

    private static bool TestFormatsNotReadyMarker()
    {
        CpuFaultPanicReadinessResult readiness = default;
        Span<byte> destination = stackalloc byte[CpuFaultPanicReadinessFormatter.MaximumMarkerBytes];

        CpuFaultPanicReadinessMarkerFormatResult result =
            CpuFaultPanicReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        string marker = Encoding.ASCII.GetString(destination[..result.BytesWritten]);
        string expected = BootSequence.CreateCpuFaultPanicNotReadyMarker(
            CpuFaultPanicBootReadinessStatus.PanicReadinessFailed,
            readiness);

        return !readiness.IsInitialized
            && !readiness.Succeeded
            && result.Succeeded
            && result.BytesWritten == expected.Length
            && result.RequiredBytes == expected.Length
            && marker == expected
            && marker.StartsWith(BootSequence.CpuFaultPanicNotReadyPrefix, StringComparison.Ordinal)
            && marker.Contains("initialized=false", StringComparison.Ordinal)
            && marker.EndsWith("flushed=0", StringComparison.Ordinal);
    }

    private static bool TestReportsShortDestination()
    {
        CpuFaultPanicReadinessResult readiness = default;
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xA5);

        CpuFaultPanicReadinessMarkerFormatResult query =
            CpuFaultPanicReadinessFormatter.QueryNotReadyMarkerRequiredBytes(readiness);
        CpuFaultPanicReadinessMarkerFormatResult result =
            CpuFaultPanicReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        return query.IsInitialized
            && query.Status == CpuFaultPanicReadinessMarkerFormatStatus.Success
            && query.RequiredBytes > destination.Length
            && !query.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == CpuFaultPanicReadinessMarkerFormatStatus.DestinationTooSmall
            && result.BytesWritten == 0
            && result.RequiredBytes == query.RequiredBytes
            && destination[0] == 0xA5
            && destination[^1] == 0xA5;
    }

    private static bool TestRejectsInvalidStatus()
    {
        CpuFaultEntryFrameLayoutResult frame =
            CpuFaultEntryFrameLayoutResult.Fail(CpuFaultEntryFrameLayoutStatus.BufferTooSmall);
        PanicSerialReporterResult reporter = PanicSerialReporterResult.Fail(
            (PanicSerialReporterStatus)999,
            bytesFormatted: 8,
            bytesFlushed: 0);
        CpuFaultPanicReadinessResult readiness =
            CpuFaultPanicReadinessResult.FailReporter(frame, reporter);
        Span<byte> destination = stackalloc byte[128];
        destination.Fill(0xCC);

        CpuFaultPanicReadinessMarkerFormatResult result =
            CpuFaultPanicReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == CpuFaultPanicReadinessMarkerFormatStatus.InvalidStatus
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && destination[0] == 0xCC
            && destination[^1] == 0xCC;
    }

    private static bool TestUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Diagnostics",
                "CpuFaultPanicReadinessFormatter.cs"));

        return source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountInt32Digits(readiness.Vector)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountInt32Digits(readiness.RequiredFrameBytes)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountInt32Digits(readiness.BytesFormatted)", StringComparison.Ordinal)
            && source.Contains("AsciiDecimalFormatter.CountInt32Digits(readiness.BytesFlushed)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, offset, source)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendBytes(destination, 0, ReadyMarker)", StringComparison.Ordinal)
            && source.Contains("AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)", StringComparison.Ordinal)
            && !source.Contains("AsciiDecimalFormatter.FormatInt32(", StringComparison.Ordinal)
            && !source.Contains("destination.Slice(offset)", StringComparison.Ordinal)
            && !source.Contains("ReadyMarker.CopyTo(destination)", StringComparison.Ordinal)
            && !source.Contains("source.CopyTo(destination", StringComparison.Ordinal)
            && !source.Contains("private static int CountSignedDigits(", StringComparison.Ordinal)
            && !source.Contains("private static int CountUnsignedDigits(", StringComparison.Ordinal)
            && !source.Contains("destination[offset] = (byte)'-';", StringComparison.Ordinal)
            && !source.Contains("destination[cursor] = (byte)('0' +", StringComparison.Ordinal);
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        CpuFaultPanicReadinessMarkerFormatResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == CpuFaultPanicReadinessMarkerFormatStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.Flags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<CpuFaultPanicReadinessMarkerFormatResult>();
    }
}
