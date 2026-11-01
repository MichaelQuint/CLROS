namespace OS.Boot;
/// <summary>
/// Identifies the only allocation provenance accepted by stage one. This is
/// a caller attestation and is not evidence that this type called firmware.
/// </summary>

public enum M01StageOneAllocationProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedSuccessfulFirmwarePageAllocation = 1
}
#if !NOSTDLIB_EFI
#endif
#if !NOSTDLIB_EFI
#endif
