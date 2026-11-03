using System.Runtime.InteropServices;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiBootstrapCopyStatus
{
    NotStarted = 0,
    Success,
    AliasedBuffers,
    InvalidScratchLength,
    InvalidSourceRange,
    OverlappingSourceRanges,
    InvalidRsdp,
    RootAddressMismatch,
    InvalidRoot,
    DuplicateRootPointer,
    InvalidChild,
    MissingMcfg,
    MissingMadt,
    MissingHpet,
    DestinationTooSmall
}
