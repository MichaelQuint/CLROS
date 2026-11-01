using OS.Boot;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
public enum GCStaticRootStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    RootStorageRejected = 2,
    ModuleRejected = 3,
    MetadataRejected = 4,
    ProtectedAlias = 5,
    RootNotEmpty = 6,
    NotPublished = 7,
    RootChanged = 8,
    HeapRejected = 9,
    PostCommitRejected = 10
}
