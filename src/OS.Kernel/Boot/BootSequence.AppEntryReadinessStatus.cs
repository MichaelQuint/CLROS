#if !NOSTDLIB_EFI
using System.Text;
using OS.Drivers;
using OS.Manifest;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Kernel.Runtime;
using OS.Net;
using OS.Pal;
using OS.Storage;

#endif
namespace OS.Boot;
#if !NOSTDLIB_EFI
public enum AppEntryReadinessStatus
{
    Ready = 0,
    AppPackageFailed = 1,
    AppSelectionFailed = 2,
    PayloadMappingFailed = 3,
    ExecutableMemoryFailed = 4,
    InvocationPlanFailed = 5,
    StagingPreflightFailed = 6
}
#endif
#if !NOSTDLIB_EFI
#endif
