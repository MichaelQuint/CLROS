namespace OS.Drivers;
public enum VirtqueueDescriptorStatus : byte
{
    Uninitialized,
    Success,
    InvalidStorage,
    InvalidSize,
    InvalidEpoch,
    AlreadyInitialized,
    MetadataNotEmpty,
    InvalidPool,
    Exhausted,
    GenerationExhausted,
    InvalidToken,
    StaleToken,
    InvalidOwnership
}
