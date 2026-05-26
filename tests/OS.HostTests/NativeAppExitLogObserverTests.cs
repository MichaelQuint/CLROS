using System.Runtime.CompilerServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Pal;

internal static class NativeAppExitLogObserverTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("appmodel.exit observer finds stdout marker", TestFindsStdoutMarker, ref failures);
        Run("appmodel.exit observer ignores non-machine-readable records", TestIgnoresNonMachineReadableRecords, ref failures);
        Run("appmodel.exit observer reports malformed marker", TestReportsMalformedMarker, ref failures);
        Run("appmodel.exit observer reports no marker", TestReportsNoMarker, ref failures);
        Run("appmodel.exit observer reports short record buffer", TestReportsShortRecordBuffer, ref failures);
        Run("appmodel.exit observer reports invalid log table", TestReportsInvalidLogTable, ref failures);
        Run("appmodel.exit observer default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("appmodel.exit observer formatter formats success marker", TestFormatterFormatsSuccessMarker, ref failures);
        Run("appmodel.exit observer formatter reports short destination", TestFormatterReportsShortDestination, ref failures);
        Run("appmodel.exit observer formatter rejects default result", TestFormatterRejectsDefaultResult, ref failures);
        Run("appmodel.exit observer formatter uses shared ascii text builder", TestFormatterUsesSharedAsciiTextBuilder, ref failures);
        Run("boot.sequence app exit observer ready", TestBootSequenceAppExitObserverReady, ref failures);
        Run(
            "boot.sequence app exit observer rejects observer failure",
            TestBootSequenceAppExitObserverRejectsObserverFailure,
            ref failures);
        Run(
            "boot.sequence app exit observer rejects default result",
            TestBootSequenceAppExitObserverRejectsDefaultResult,
            ref failures);
        Run(
            "boot.sequence app exit observer preserves default markers",
            TestBootSequenceAppExitObserverPreservesDefaultMarkers,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Native app exit log observer host tests failed: " + failures);
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

    private static bool TestFindsStdoutMarker()
    {
        OSHostAppLogTable table = new(recordCapacity: 3, maxRecordBytes: 128);
        table.Write(
            OSHostAppLogConstants.StreamStderr,
            Ascii("APP_EXIT code=99 status=OSHOST_STATUS_INTERNAL_ERROR"),
            OSHostAppLogConstants.FlagMachineReadable);
        table.Write(
            OSHostAppLogConstants.StreamStdout,
            Ascii("APP_EXIT code=7 status=OSHOST_STATUS_INTERNAL_ERROR"),
            OSHostAppLogConstants.FlagMachineReadable);

        Span<byte> scratch = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];
        NativeAppExitLogObserverResult result =
            NativeAppExitLogObserver.Observe(table, scratch);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == NativeAppExitLogObserverStatus.Success
            && result.RecordIndex == 1
            && result.StreamId == OSHostAppLogConstants.StreamStdout
            && result.Flags == OSHostAppLogConstants.FlagMachineReadable
            && result.Sequence == 1
            && result.RecordsScanned == 2
            && result.BytesRead == (uint)"APP_EXIT code=7 status=OSHOST_STATUS_INTERNAL_ERROR".Length
            && result.RequiredBytes == result.BytesRead
            && result.LogStatus == OSHostStatus.Ok
            && result.ParseAttempted
            && result.ParseStatus == NativeAppExitParseStatus.Success
            && result.ExitCode == 7
            && result.ExitStatus == OSHostStatus.InternalError
            && !result.LauncherFailure;
    }

    private static bool TestIgnoresNonMachineReadableRecords()
    {
        OSHostAppLogTable table = new(recordCapacity: 3, maxRecordBytes: 128);
        table.Write(
            OSHostAppLogConstants.StreamStdout,
            Ascii("APP_EXIT code=0 status=OSHOST_STATUS_OK"),
            flags: 0);
        table.Write(
            OSHostAppLogConstants.StreamStderr,
            Ascii("APP_EXIT code=0 status=OSHOST_STATUS_OK"),
            OSHostAppLogConstants.FlagMachineReadable);

        Span<byte> scratch = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];
        NativeAppExitLogObserverResult result =
            NativeAppExitLogObserver.Observe(table, scratch);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppExitLogObserverStatus.NoExitMarker
            && result.RecordsScanned == 2
            && result.RecordIndex == NativeAppExitLogObserverResult.NoRecordIndex
            && !result.ParseAttempted
            && result.LogStatus == OSHostStatus.NotFound;
    }

    private static bool TestReportsMalformedMarker()
    {
        OSHostAppLogTable table = new(recordCapacity: 2, maxRecordBytes: 128);
        table.Write(
            OSHostAppLogConstants.StreamStdout,
            Ascii("APP_EXIT code=abc status=OSHOST_STATUS_OK"),
            OSHostAppLogConstants.FlagMachineReadable);

        Span<byte> scratch = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];
        NativeAppExitLogObserverResult result =
            NativeAppExitLogObserver.Observe(table, scratch);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppExitLogObserverStatus.MalformedExitMarker
            && result.RecordIndex == 0
            && result.StreamId == OSHostAppLogConstants.StreamStdout
            && result.Sequence == 0
            && result.BytesRead == (uint)"APP_EXIT code=abc status=OSHOST_STATUS_OK".Length
            && result.LogStatus == OSHostStatus.Ok
            && result.ParseAttempted
            && result.ParseStatus == NativeAppExitParseStatus.MalformedCode
            && result.ExitStatus == OSHostStatus.InvalidArgument;
    }

    private static bool TestReportsNoMarker()
    {
        OSHostAppLogTable table = new(recordCapacity: 2, maxRecordBytes: 128);
        table.Write(
            OSHostAppLogConstants.StreamStdout,
            Ascii("APP_EVENT name=started"),
            OSHostAppLogConstants.FlagMachineReadable);

        Span<byte> scratch = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];
        NativeAppExitLogObserverResult result =
            NativeAppExitLogObserver.Observe(table, scratch);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppExitLogObserverStatus.NoExitMarker
            && result.RecordsScanned == 1
            && result.RecordIndex == NativeAppExitLogObserverResult.NoRecordIndex
            && !result.ParseAttempted
            && result.ExitStatus == OSHostStatus.NotFound;
    }

    private static bool TestReportsShortRecordBuffer()
    {
        OSHostAppLogTable table = new(recordCapacity: 1, maxRecordBytes: 128);
        byte[] marker = Ascii("APP_EXIT code=0 status=OSHOST_STATUS_OK");
        table.Write(
            OSHostAppLogConstants.StreamStdout,
            marker,
            OSHostAppLogConstants.FlagMachineReadable);

        Span<byte> scratch = stackalloc byte[8];
        NativeAppExitLogObserverResult result =
            NativeAppExitLogObserver.Observe(table, scratch);

        return result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppExitLogObserverStatus.RecordBufferTooSmall
            && result.RecordIndex == 0
            && result.StreamId == OSHostAppLogConstants.StreamStdout
            && result.Sequence == 0
            && result.BytesRead == 0
            && result.RequiredBytes == (uint)marker.Length
            && result.LogStatus == OSHostStatus.BufferTooSmall
            && !result.ParseAttempted;
    }

    private static bool TestReportsInvalidLogTable()
    {
        Span<byte> scratch = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];
        NativeAppExitLogObserverResult nullTable =
            NativeAppExitLogObserver.Observe(null, scratch);
        NativeAppExitLogObserverResult emptyTable =
            NativeAppExitLogObserver.Observe(new OSHostAppLogTable(0, 128), scratch);

        return nullTable.IsInitialized
            && !nullTable.Succeeded
            && nullTable.Status == NativeAppExitLogObserverStatus.InvalidLogTable
            && nullTable.LogStatus == OSHostStatus.InvalidArgument
            && emptyTable.IsInitialized
            && !emptyTable.Succeeded
            && emptyTable.Status == NativeAppExitLogObserverStatus.InvalidLogTable;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        NativeAppExitLogObserverResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == NativeAppExitLogObserverStatus.Success
            && result.RecordIndex == 0
            && result.BytesRead == 0
            && result.ParseStatus == NativeAppExitParseStatus.Success
            && result.ExitStatus == OSHostStatus.Ok
            && !RuntimeHelpers.IsReferenceOrContainsReferences<NativeAppExitLogObserverResult>();
    }

    private static bool TestFormatterFormatsSuccessMarker()
    {
        NativeAppExitLogObserverResult observer = CreateSuccessfulObserverResult(
            out byte[] marker);
        Span<byte> destination = stackalloc byte[512];

        NativeAppExitLogObserverDiagnosticFormatResult query =
            NativeAppExitLogObserverDiagnosticFormatter.QueryRequiredBytes(observer);
        NativeAppExitLogObserverDiagnosticFormatResult format =
            NativeAppExitLogObserverDiagnosticFormatter.FormatResult(observer, destination);

        string text = Encoding.ASCII.GetString(destination[..format.BytesWritten]);
        string expected = "APP_EXIT_OBSERVER status=Success recordsScanned=2 recordIndex=1 streamId="
            + OSHostAppLogConstants.StreamStdout
            + " flags=" + OSHostAppLogConstants.FlagMachineReadable
            + " sequence=1 bytesRead=" + marker.Length
            + " requiredBytes=" + marker.Length
            + " logStatus=OSHOST_STATUS_OK parseAttempted=true parseStatus=Success"
            + " exitCode=7 exitStatus=OSHOST_STATUS_INTERNAL_ERROR launcherFailure=false";

        return query.IsInitialized
            && !query.Succeeded
            && query.Status == NativeAppExitLogObserverDiagnosticFormatStatus.Success
            && query.ObserverStatus == NativeAppExitLogObserverStatus.Success
            && query.BytesWritten == 0
            && query.RequiredBytes == expected.Length
            && query.AvailableBytes == 0
            && format.Succeeded
            && format.ObserverStatus == NativeAppExitLogObserverStatus.Success
            && format.BytesWritten == expected.Length
            && format.RequiredBytes == expected.Length
            && format.AvailableBytes == destination.Length
            && text == expected;
    }

    private static bool TestFormatterReportsShortDestination()
    {
        NativeAppExitLogObserverResult observer = CreateSuccessfulObserverResult(
            out _);
        Span<byte> destination = stackalloc byte[16];
        destination.Fill(0xA5);

        NativeAppExitLogObserverDiagnosticFormatResult query =
            NativeAppExitLogObserverDiagnosticFormatter.QueryRequiredBytes(observer);
        NativeAppExitLogObserverDiagnosticFormatResult format =
            NativeAppExitLogObserverDiagnosticFormatter.FormatResult(observer, destination);

        return query.IsInitialized
            && query.Status == NativeAppExitLogObserverDiagnosticFormatStatus.Success
            && query.RequiredBytes > destination.Length
            && !query.Succeeded
            && format.IsInitialized
            && !format.Succeeded
            && format.Status == NativeAppExitLogObserverDiagnosticFormatStatus.DestinationTooSmall
            && format.ObserverStatus == NativeAppExitLogObserverStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == query.RequiredBytes
            && format.AvailableBytes == destination.Length
            && destination[0] == 0xA5
            && destination[^1] == 0xA5;
    }

    private static bool TestFormatterRejectsDefaultResult()
    {
        Span<byte> destination = stackalloc byte[64];
        destination.Fill(0xCC);

        NativeAppExitLogObserverResult observer = default;
        NativeAppExitLogObserverDiagnosticFormatResult format =
            NativeAppExitLogObserverDiagnosticFormatter.FormatResult(observer, destination);
        NativeAppExitLogObserverDiagnosticFormatResult query =
            NativeAppExitLogObserverDiagnosticFormatter.QueryRequiredBytes(observer);
        NativeAppExitLogObserverDiagnosticFormatResult defaultFormat = default;

        return format.IsInitialized
            && !format.Succeeded
            && format.Status == NativeAppExitLogObserverDiagnosticFormatStatus.InvalidResult
            && format.ObserverStatus == NativeAppExitLogObserverStatus.Success
            && format.BytesWritten == 0
            && format.RequiredBytes == 0
            && format.AvailableBytes == destination.Length
            && query.IsInitialized
            && !query.Succeeded
            && query.Status == NativeAppExitLogObserverDiagnosticFormatStatus.InvalidResult
            && query.RequiredBytes == 0
            && !defaultFormat.IsInitialized
            && !defaultFormat.Succeeded
            && defaultFormat.Status == NativeAppExitLogObserverDiagnosticFormatStatus.Success
            && defaultFormat.ObserverStatus == NativeAppExitLogObserverStatus.Success
            && defaultFormat.RequiredBytes == 0
            && destination[0] == 0xCC
            && destination[^1] == 0xCC
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                NativeAppExitLogObserverDiagnosticFormatResult>();
    }

    private static bool TestFormatterUsesSharedAsciiTextBuilder()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Runtime",
                "NativeAppExitLogObserverDiagnosticFormatter.cs"));

        return source.Contains(
                "AsciiDecimalFormatter.CountInt32Digits(result.RecordsScanned)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiDecimalFormatter.CountUInt64Digits(result.RecordIndex)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendBytes(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && source.Contains(
                "AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value)",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static int GetUInt64DecimalByteCount",
                StringComparison.Ordinal)
            && !source.Contains(
                "private static void AppendUInt64",
                StringComparison.Ordinal)
            && !source.Contains(
                "destination[offset + index] = (byte)('0' +",
                StringComparison.Ordinal);
    }

    private static bool TestBootSequenceAppExitObserverReady()
    {
        NativeAppExitLogObserverResult observer = CreateSuccessfulObserverResult(out _);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAppExitObserverReadiness(
            observer,
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppExitObserverReadyMarker(observer);

        return observer.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker == "APP_EXIT_OBSERVER_READY status=Ready initialized=true succeeded=true observer=Success recordsScanned=2 recordIndex=1 streamId=1 flags=1 sequence=1 bytesRead=51 requiredBytes=51 logStatus=Ok parseAttempted=true parseStatus=Success exitCode=7 exitStatus=InternalError launcherFailure=false";
    }

    private static bool TestBootSequenceAppExitObserverRejectsObserverFailure()
    {
        NativeAppExitLogObserverResult observer = NativeAppExitLogObserverResult.NoExitMarker(
            recordsScanned: 2);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAppExitObserverReadiness(
            observer,
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppExitObserverNotReadyMarker(
            AppExitObserverReadinessStatus.ObserverFailure,
            observer);

        return observer.IsInitialized
            && !observer.Succeeded
            && observer.Status == NativeAppExitLogObserverStatus.NoExitMarker
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(AppExitObserverReadinessStatus.ObserverFailure),
                StringComparison.Ordinal)
            && result.Reason.Contains(
                nameof(NativeAppExitLogObserverStatus.NoExitMarker),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker == "APP_EXIT_OBSERVER_NOT_READY status=ObserverFailure initialized=true succeeded=false observer=NoExitMarker recordsScanned=2 recordIndex=4294967295 streamId=0 flags=0 sequence=0 bytesRead=0 requiredBytes=0 logStatus=NotFound parseAttempted=false parseStatus=InvalidPrefix exitCode=0 exitStatus=NotFound launcherFailure=false";
    }

    private static bool TestBootSequenceAppExitObserverRejectsDefaultResult()
    {
        NativeAppExitLogObserverResult observer = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunNativeAppExitObserverReadiness(
            observer,
            serial,
            events);
        string expectedMarker = BootSequence.CreateAppExitObserverNotReadyMarker(
            AppExitObserverReadinessStatus.NotInitialized,
            observer);

        return !observer.IsInitialized
            && !observer.Succeeded
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(AppExitObserverReadinessStatus.NotInitialized),
                StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && events.Snapshot()[0] == expectedMarker
            && expectedMarker == "APP_EXIT_OBSERVER_NOT_READY status=NotInitialized initialized=false succeeded=false observer=Success recordsScanned=0 recordIndex=0 streamId=0 flags=0 sequence=0 bytesRead=0 requiredBytes=0 logStatus=Ok parseAttempted=false parseStatus=Success exitCode=0 exitStatus=Ok launcherFailure=false";
    }

    private static bool TestBootSequenceAppExitObserverPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(32);

        BootResult result = BootSequence.Run(serial, events);
        if (!result.Succeeded)
        {
            return false;
        }

        for (int index = 0; index < serial.Count; index++)
        {
            string line = serial.LineAt(index);
            if (line.StartsWith(BootSequence.AppExitObserverReadyPrefix, StringComparison.Ordinal) ||
                line.StartsWith(BootSequence.AppExitObserverNotReadyPrefix, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static NativeAppExitLogObserverResult CreateSuccessfulObserverResult(
        out byte[] marker)
    {
        OSHostAppLogTable table = new(recordCapacity: 3, maxRecordBytes: 128);
        table.Write(
            OSHostAppLogConstants.StreamStderr,
            Ascii("APP_EXIT code=99 status=OSHOST_STATUS_INTERNAL_ERROR"),
            OSHostAppLogConstants.FlagMachineReadable);
        marker = Ascii("APP_EXIT code=7 status=OSHOST_STATUS_INTERNAL_ERROR");
        table.Write(
            OSHostAppLogConstants.StreamStdout,
            marker,
            OSHostAppLogConstants.FlagMachineReadable);

        Span<byte> scratch = stackalloc byte[NativeAppExitStatusFormatter.MaximumMarkerBytes];
        return NativeAppExitLogObserver.Observe(table, scratch);
    }

    private static byte[] Ascii(string value)
    {
        return Encoding.ASCII.GetBytes(value);
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }
    }
}
