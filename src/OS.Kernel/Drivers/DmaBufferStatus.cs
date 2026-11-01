using OS.Kernel.Platform;

namespace OS.Drivers;
public enum DmaBufferStatus : byte
{
    Uninitialized,
    Success,
    InvalidStorage,
    InvalidWindow,
    InvalidEpoch,
    AlreadyInitialized,
    MetadataNotEmpty,
    InvalidPool,
    InvalidPageCount,
    Exhausted,
    GenerationExhausted,
    InvalidToken,
    StaleToken,
    InvalidOwnership
}
