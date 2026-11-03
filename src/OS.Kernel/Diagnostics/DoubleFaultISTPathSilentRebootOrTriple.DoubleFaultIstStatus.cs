namespace OS.Kernel.Diagnostics;

public enum DoubleFaultIstStatus : byte
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
    LayoutOverflow,
    IdtMismatch,
    SlotMismatch,
    ObservationMismatch
}
