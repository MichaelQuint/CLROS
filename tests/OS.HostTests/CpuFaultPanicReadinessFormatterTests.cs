using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;

internal static class CpuFaultPanicReadinessFormatterTests
{

    [HostTest("diagnostics.cpu_fault_panic formatter formats ready marker", HostLane.Fast, 0)]
    private static bool TestFormatsReadyMarker()
    {
        Span<byte> destination = stackalloc byte[CpuFaultPanicReadinessFormatter.MaximumMarkerBytes];

        CpuFaultPanicReadinessMarkerFormatResult result =
            CpuFaultPanicReadinessFormatter.TryFormatReadyMarker(destination);

        string marker = Encoding.ASCII.GetString(destination[..result.BytesWritten]);

        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == CpuFaultPanicReadinessMarkerFormatStatus.Success
            && result.BytesWritten == BootSequence.CpuFaultPanicReady.Length
            && result.RequiredBytes == BootSequence.CpuFaultPanicReady.Length
            && marker == BootSequence.CpuFaultPanicReady);
    }

    [HostTest("diagnostics.cpu_fault_panic formatter formats not-ready marker", HostLane.Fast, 1)]
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

        return HostCheck.Result(!readiness.IsInitialized
            && !readiness.Succeeded
            && result.Succeeded
            && result.BytesWritten == expected.Length
            && result.RequiredBytes == expected.Length
            && marker == expected
            && marker.StartsWith(BootSequence.CpuFaultPanicNotReadyPrefix, StringComparison.Ordinal)
            && marker.Contains("initialized=false", StringComparison.Ordinal)
            && marker.EndsWith("flushed=0", StringComparison.Ordinal));
    }

    [HostTest("diagnostics.cpu_fault_panic formatter reports short destination", HostLane.Fast, 2)]
    private static bool TestReportsShortDestination()
    {
        CpuFaultPanicReadinessResult readiness = default;
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xA5);

        CpuFaultPanicReadinessMarkerFormatResult query =
            CpuFaultPanicReadinessFormatter.QueryNotReadyMarkerRequiredBytes(readiness);
        CpuFaultPanicReadinessMarkerFormatResult result =
            CpuFaultPanicReadinessFormatter.TryFormatNotReadyMarker(readiness, destination);

        return HostCheck.Result(query.IsInitialized
            && query.Status == CpuFaultPanicReadinessMarkerFormatStatus.Success
            && query.RequiredBytes > destination.Length
            && !query.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == CpuFaultPanicReadinessMarkerFormatStatus.DestinationTooSmall
            && result.BytesWritten == 0
            && result.RequiredBytes == query.RequiredBytes
            && destination[0] == 0xA5
            && destination[^1] == 0xA5);
    }

    [HostTest("diagnostics.cpu_fault_panic formatter rejects invalid status", HostLane.Fast, 3)]
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

        return HostCheck.Result(result.IsInitialized
            && !result.Succeeded
            && result.Status == CpuFaultPanicReadinessMarkerFormatStatus.InvalidStatus
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && destination[0] == 0xCC
            && destination[^1] == 0xCC);
    }

    [HostTest("diagnostics.cpu_fault_panic formatter uses shared ascii text builder", HostLane.Fast, 4)]
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

        return HostCheck.Result(source.Contains("using OS.Std.Text;", StringComparison.Ordinal)
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
            && !source.Contains("destination[cursor] = (byte)('0' +", StringComparison.Ordinal));
    }

    [HostTest("diagnostics.cpu_fault_panic formatter default result is not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        CpuFaultPanicReadinessMarkerFormatResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == CpuFaultPanicReadinessMarkerFormatStatus.Success
            && result.BytesWritten == 0
            && result.RequiredBytes == 0
            && result.Flags == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<CpuFaultPanicReadinessMarkerFormatResult>());
    }
}
