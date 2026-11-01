namespace OS.Kernel.Diagnostics;
public readonly struct PanicSerialReporterResult
{
    private PanicSerialReporterResult(bool isInitialized, PanicSerialReporterStatus status, int bytesFormatted, int bytesFlushed)
    {
        IsInitialized = isInitialized;
        Status = status;
        BytesFormatted = bytesFormatted;
        BytesFlushed = bytesFlushed;
    }

    public bool IsInitialized { get; }
    public PanicSerialReporterStatus Status { get; }
    public int BytesFormatted { get; }
    public int BytesFlushed { get; }
    public bool Succeeded => IsInitialized && Status == PanicSerialReporterStatus.Ok && BytesFormatted > 0 && BytesFlushed == BytesFormatted;

    public static PanicSerialReporterResult Success(int bytesFormatted, int bytesFlushed)
    {
        return new PanicSerialReporterResult(true, PanicSerialReporterStatus.Ok, bytesFormatted, bytesFlushed);
    }

    public static PanicSerialReporterResult Fail(PanicSerialReporterStatus status, int bytesFormatted, int bytesFlushed)
    {
        return new PanicSerialReporterResult(true, status, bytesFormatted, bytesFlushed);
    }
}
