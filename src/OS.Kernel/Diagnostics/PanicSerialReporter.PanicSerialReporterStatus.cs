namespace OS.Kernel.Diagnostics;
public enum PanicSerialReporterStatus
{
    Ok = 0,
    BufferOverflow = 1,
    InvalidAscii = 2,
    SinkFailure = 3
}
