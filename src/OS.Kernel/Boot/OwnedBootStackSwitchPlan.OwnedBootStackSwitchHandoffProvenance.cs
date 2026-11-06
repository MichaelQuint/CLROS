namespace OS.Boot;
/// <summary>
/// Records the caller's provenance attestation for the handoff storage.
/// The planner validates range separation, but a later live coordinator must
/// prove that the storage is actually static and remains owned.
/// </summary>

public enum OwnedBootStackSwitchHandoffProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedStaticOwnedData = 1
}
