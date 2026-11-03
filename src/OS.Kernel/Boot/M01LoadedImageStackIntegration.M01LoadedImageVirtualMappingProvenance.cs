namespace OS.Boot;
/// <summary>
/// Identifies the explicitly attested pre-hierarchy mapping used to compare
/// loaded-image virtual storage with virtual pointers.
/// </summary>

public enum M01LoadedImageVirtualMappingProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedIdentityMapping = 1
}
