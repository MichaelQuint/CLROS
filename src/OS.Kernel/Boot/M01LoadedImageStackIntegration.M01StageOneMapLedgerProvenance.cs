namespace OS.Boot;
/// <summary>
/// Identifies the only map/ledger provenance accepted by stage one. This is
/// a caller attestation over an owned map copy and canonical ledger.
/// </summary>

public enum M01StageOneMapLedgerProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedPostAllocationOwnedMap = 1
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
