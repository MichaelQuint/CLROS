using OS.Boot;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
public enum GCStaticMetadataStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ModuleRejected = 2,
    SectionAbsent = 3,
    SectionEmpty = 4,
    UnsupportedEntryCount = 5,
    CallerBoundsRejected = 6,
    EntryRejected = 7,
    CellRejected = 8,
    ProtectedAlias = 9,
    UnsupportedForm = 10,
    MethodTableRejected = 11,
    RelatedTypeRejected = 12,
    DescriptorRejected = 13
}
