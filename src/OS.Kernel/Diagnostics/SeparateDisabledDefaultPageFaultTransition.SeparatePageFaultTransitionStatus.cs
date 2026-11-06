namespace OS.Kernel.Diagnostics;

public enum SeparatePageFaultTransitionStatus : byte
{
    Uninitialized,
    Success,
    InvalidRequest,
    InvalidMode,
    InvalidPat,
    InvalidSnapshot,
    InvalidStorage,
    StorageOverlap,
    ContextMismatch,
    StreamMismatch,
    LayoutOverflow
}
