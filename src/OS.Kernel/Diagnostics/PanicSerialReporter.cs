namespace OS.Kernel.Diagnostics;
public static class PanicSerialReporter
{
    public static PanicSerialReporterResult ReportFaultSummary(IPanicSerialByteSink? sink, Span<byte> scratch, ReadOnlySpan<char> subsystem, ReadOnlySpan<char> reason, in CpuFaultFrame frame)
    {
        if (sink is null)
        {
            return PanicSerialReporterResult.Fail(PanicSerialReporterStatus.SinkFailure, 0, 0);
        }

        PanicTextBuffer buffer = new PanicTextBuffer(scratch);
        PanicTextBufferStatus formatStatus = buffer.WriteFaultSummary(subsystem, reason, frame);
        if (formatStatus != PanicTextBufferStatus.Ok)
        {
            return PanicSerialReporterResult.Fail(MapFormatStatus(formatStatus), buffer.Written, 0);
        }

        return Flush(sink, buffer.WrittenSpan);
    }

    private static PanicSerialReporterResult Flush(IPanicSerialByteSink sink, ReadOnlySpan<byte> bytes)
    {
        int bytesFlushed = 0;
        for (int index = 0; index < bytes.Length; index++)
        {
            if (!sink.TryWriteByte(bytes[index]))
            {
                return PanicSerialReporterResult.Fail(PanicSerialReporterStatus.SinkFailure, bytes.Length, bytesFlushed);
            }

            bytesFlushed++;
        }

        return PanicSerialReporterResult.Success(bytes.Length, bytesFlushed);
    }

    private static PanicSerialReporterStatus MapFormatStatus(PanicTextBufferStatus status)
    {
        return status switch
        {
            PanicTextBufferStatus.Ok => PanicSerialReporterStatus.Ok,
            PanicTextBufferStatus.Overflow => PanicSerialReporterStatus.BufferOverflow,
            PanicTextBufferStatus.InvalidAscii => PanicSerialReporterStatus.InvalidAscii,
            _ => PanicSerialReporterStatus.InvalidAscii
        };
    }
}
