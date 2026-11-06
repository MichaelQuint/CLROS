namespace OS.Boot;
/// <summary>
/// Identifies the only accepted arena-allocation provenance. It is a caller
/// attestation and is not evidence that this type called firmware.
/// </summary>

public enum M01LiveAllocatorAllocationProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedSuccessfulFirmwarePageAllocation = 1
}
