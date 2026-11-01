namespace OS.Kernel.Diagnostics;
public enum UnexpectedVectorPanicStatus : byte
{
    Uninitialized,
    Success,
    InvalidRequest,
    InvalidStorage,
    StorageOverlap,
    ContextMismatch,
    NormalizerMismatch,
    IdtMismatch,
    StreamMismatch,
    InvalidCapture,
    ControlledVector,
    StateMismatch,
    BackendNotReady,
    FormatRejected,
    DrainRejected,
    DrainTimeout,
    ConfigurationRejected,
    MetadataReadRejected,
    MetadataTimeout,
    MetadataWriteRejected,
    PanicRejected
}
