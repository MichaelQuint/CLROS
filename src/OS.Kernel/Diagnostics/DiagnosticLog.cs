namespace OS.Kernel.Diagnostics;

public sealed partial class DiagnosticLog
{
    public const string InfoPrefix = "[info]";
    public const string WarnPrefix = "[warn]";
    public const string PanicPrefix = "[PANIC]";
    private readonly ISerialSink _serial;
    private readonly BootEventRingBuffer? _events;
    public DiagnosticLog(ISerialSink serial) : this(serial, null)
    {
    }

    public DiagnosticLog(ISerialSink serial, BootEventRingBuffer? events)
    {
        _serial = serial ?? new DiscardSerialSink();
        _events = events;
    }

    public void Info(string? message)
    {
        Write(DiagnosticLogLevel.Info, message);
    }

    public void Warn(string? message)
    {
        Write(DiagnosticLogLevel.Warn, message);
    }

    public void Panic(string? message)
    {
        Write(DiagnosticLogLevel.Panic, message);
    }

    public void Write(DiagnosticLogLevel level, string? message)
    {
        string line = PrefixFor(level) + " " + (message ?? string.Empty);
        _events?.Record(line);
        _serial.WriteLine(line);
    }

    private static string PrefixFor(DiagnosticLogLevel level)
    {
        switch (level)
        {
            case DiagnosticLogLevel.Info:
                return InfoPrefix;
            case DiagnosticLogLevel.Warn:
                return WarnPrefix;
            case DiagnosticLogLevel.Panic:
                return PanicPrefix;
            default:
                return InfoPrefix;
        }
    }
}
