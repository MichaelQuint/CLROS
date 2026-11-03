namespace OS.Boot;
/// <summary>
/// Identifies the explicit pre-hierarchy relationship between the physical
/// allocation/reservation domain and the dereferenceable virtual-pointer
/// domain used by stage one.
/// </summary>

public enum M01StageOneAddressMappingProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedIdentityMappings = 1
}
