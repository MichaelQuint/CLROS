using OS.Boot;

namespace OS.Kernel.Runtime;
/// <summary>Finite registered-image observations; neither kind grants mapping or write authority.</summary>

public enum TypeManagerRegisteredRangeKind : byte
{
    Uninitialized = 0,
    ImmutableMetadata = 1,
    MutableData = 2,
    ReadableSectionData = 3,
    ExecutableEntry = 4
}
