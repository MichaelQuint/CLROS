using OS.Boot;

namespace OS.Kernel.Runtime;

public enum X64FrameUnwindStatus : byte
{
    Uninitialized,
    Success,
    UnsupportedProfile,
    MetadataRejected,
    ModuleRejected,
    UnsupportedFrame,
    UnsupportedEpilog,
    CodeRangeRejected,
    StackRangeRejected,
    StorageOverlap,
    ArithmeticOverflow,
    FrameTransitionRejected,
    ReturnPcRejected
}
