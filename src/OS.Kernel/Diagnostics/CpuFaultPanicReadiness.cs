namespace OS.Kernel.Diagnostics;

public enum CpuFaultPanicReadinessStatus
{
    Ready = 0,
    FaultFrameInvalid = 1,
    PanicReportFailed = 2
}

public readonly struct CpuFaultPanicReadinessResult
{
    private CpuFaultPanicReadinessResult(
        bool isInitialized,
        CpuFaultPanicReadinessStatus status,
        CpuFaultEntryFrameLayoutStatus frameStatus,
        PanicSerialReporterStatus reporterStatus,
        int vector,
        int requiredFrameBytes,
        int bytesFormatted,
        int bytesFlushed)
    {
        IsInitialized = isInitialized;
        Status = status;
        FrameStatus = frameStatus;
        ReporterStatus = reporterStatus;
        Vector = vector;
        RequiredFrameBytes = requiredFrameBytes;
        BytesFormatted = bytesFormatted;
        BytesFlushed = bytesFlushed;
    }

    public bool IsInitialized { get; }

    public CpuFaultPanicReadinessStatus Status { get; }

    public CpuFaultEntryFrameLayoutStatus FrameStatus { get; }

    public PanicSerialReporterStatus ReporterStatus { get; }

    public int Vector { get; }

    public int RequiredFrameBytes { get; }

    public int BytesFormatted { get; }

    public int BytesFlushed { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == CpuFaultPanicReadinessStatus.Ready &&
        FrameStatus == CpuFaultEntryFrameLayoutStatus.Ok &&
        ReporterStatus == PanicSerialReporterStatus.Ok &&
        BytesFormatted > 0 &&
        BytesFlushed == BytesFormatted;

    public static CpuFaultPanicReadinessResult Success(
        CpuFaultEntryFrameLayoutResult frame,
        PanicSerialReporterResult reporter)
    {
        return new CpuFaultPanicReadinessResult(
            true,
            CpuFaultPanicReadinessStatus.Ready,
            frame.Status,
            reporter.Status,
            frame.Vector,
            frame.RequiredBytes,
            reporter.BytesFormatted,
            reporter.BytesFlushed);
    }

    public static CpuFaultPanicReadinessResult FailFrame(CpuFaultEntryFrameLayoutResult frame)
    {
        return new CpuFaultPanicReadinessResult(
            true,
            CpuFaultPanicReadinessStatus.FaultFrameInvalid,
            frame.Status,
            PanicSerialReporterStatus.Ok,
            frame.Vector,
            frame.RequiredBytes,
            0,
            0);
    }

    public static CpuFaultPanicReadinessResult FailReporter(
        CpuFaultEntryFrameLayoutResult frame,
        PanicSerialReporterResult reporter)
    {
        return new CpuFaultPanicReadinessResult(
            true,
            CpuFaultPanicReadinessStatus.PanicReportFailed,
            frame.Status,
            reporter.Status,
            frame.Vector,
            frame.RequiredBytes,
            reporter.BytesFormatted,
            reporter.BytesFlushed);
    }
}

public static class CpuFaultPanicReadiness
{
    public static CpuFaultPanicReadinessResult RequireReady(
        ReadOnlySpan<byte> faultEntryFrame,
        Span<byte> panicScratch,
        IPanicSerialByteSink? sink,
        ReadOnlySpan<char> subsystem,
        ReadOnlySpan<char> reason)
    {
        CpuFaultEntryFrameLayoutResult frame = CpuFaultEntryFrameLayout.TryRead(faultEntryFrame);
        if (!frame.Succeeded)
        {
            return CpuFaultPanicReadinessResult.FailFrame(frame);
        }

        PanicSerialReporterResult report = PanicSerialReporter.ReportFaultSummary(
            sink,
            panicScratch,
            subsystem,
            reason,
            frame.Frame);
        if (!report.Succeeded)
        {
            return CpuFaultPanicReadinessResult.FailReporter(frame, report);
        }

        return CpuFaultPanicReadinessResult.Success(frame, report);
    }
}
