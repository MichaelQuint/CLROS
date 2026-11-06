using OS.Kernel.Platform;
using OS.Std.Text;

namespace OS.Kernel.Diagnostics;

public enum OwnedSerialDiagnosticWriteStatus
{
    Success = 0,
    MarkerFormatFailed = 1,
    SerialWriteFailed = 2,
    MissingSerial = 3
}
