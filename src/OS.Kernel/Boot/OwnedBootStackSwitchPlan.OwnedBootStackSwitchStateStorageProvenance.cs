namespace OS.Boot;
/// <summary>
/// Records the caller's provenance attestation for the canonical mutable
/// state storage bound to one immutable plan.
/// </summary>

public enum OwnedBootStackSwitchStateStorageProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedStaticOwnedData = 1
}
