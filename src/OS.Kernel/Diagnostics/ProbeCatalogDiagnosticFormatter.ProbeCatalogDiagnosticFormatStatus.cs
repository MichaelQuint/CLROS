using OS.Std.Text;

namespace OS.Kernel.Diagnostics;
public enum ProbeCatalogDiagnosticFormatStatus
{
    Success = 0,
    InvalidDefinition = 1,
    DestinationTooSmall = 2,
    RequiredBytesOverflow = 3
}
