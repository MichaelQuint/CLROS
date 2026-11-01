namespace OS.Kernel.Diagnostics;
public static class CpuFaultPanicReadiness
{
    public static CpuFaultPanicReadinessResult RequireReady(ReadOnlySpan<byte> faultEntryFrame, Span<byte> panicScratch, IPanicSerialByteSink? sink, ReadOnlySpan<char> subsystem, ReadOnlySpan<char> reason)
    {
        CpuFaultEntryFrameLayoutResult frame = CpuFaultEntryFrameLayout.TryRead(faultEntryFrame);
        if (!frame.Succeeded)
        {
            return CpuFaultPanicReadinessResult.FailFrame(frame);
        }

        PanicSerialReporterResult report = PanicSerialReporter.ReportFaultSummary(sink, panicScratch, subsystem, reason, frame.Frame);
        if (!report.Succeeded)
        {
            return CpuFaultPanicReadinessResult.FailReporter(frame, report);
        }

        return CpuFaultPanicReadinessResult.Success(frame, report);
    }
}
