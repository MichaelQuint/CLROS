namespace OS.Boot;
/// <summary>
/// Identifies the only accepted refreshed-map provenance. It is a caller
/// attestation over an owned map copy and canonical reservation ledger.
/// </summary>

public enum M01LiveAllocatorMapLedgerProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedPostArenaAllocationOwnedMap = 1
}
