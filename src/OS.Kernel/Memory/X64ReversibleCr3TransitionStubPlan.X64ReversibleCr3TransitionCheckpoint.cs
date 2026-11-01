namespace OS.Kernel.Memory;
/// <summary>
/// Last ordered checkpoint written into the caller-owned state record.
/// </summary>

public enum X64ReversibleCr3TransitionCheckpoint : byte
{
    Uninitialized = 0,
    EntryValidated = 1,
    InterruptsDisabled = 2,
    PgeCleared = 3,
    OwnedCr3Loaded = 4,
    CanaryUpdated = 5,
    FirmwareCr3Restored = 6,
    Cr4Restored = 7,
    RflagsRestored = 8,
    Returned = 9
}
