using OS.Boot;

namespace OS.Kernel.Runtime;
public enum TypeManagerModuleStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ImageAuthorityRejected = 2,
    ImageFormatRejected = 3,
    ModuleListRejected = 4,
    MetadataRejected = 5,
    UnsupportedFormat = 6,
    StorageRejected = 7,
    StorageOverlap = 8,
    AlreadyRegistered = 9,
    RegistrationChanged = 10,
    SectionAbsent = 11,
    RangeRejected = 12,
    RangePermissionRejected = 13,
    RangeProtectedOverlap = 14,
    UnsupportedRangeKind = 15
}
