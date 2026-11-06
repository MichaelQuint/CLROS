namespace OS.Kernel.Runtime;
/// <summary>A caller obligation, not proof of mapping, exclusivity or GC support.</summary>

public enum CheckedReferenceBulkProfile : byte
{
    Uninitialized = 0,
    NonCollectingExclusiveRetainedPage = 1
}
