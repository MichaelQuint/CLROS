using OS.Std.Text;

namespace OS.Storage;

public enum VirtioBlockPackageFileStagingPreflightDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}
