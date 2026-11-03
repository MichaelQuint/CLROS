using OS.Std.Text;

namespace OS.Manifest;

public enum NativeManifestEnvironmentDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    SourceRangeInvalid = 2,
    DestinationTooSmall = 3
}
