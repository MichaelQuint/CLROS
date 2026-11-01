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
public enum AppSelectionReadinessStatus
{
    Ready = 0,
    ManifestFailed = 1,
    PackageFailed = 2,
    LaunchPlanFailed = 3,
    LaunchDescriptorFailed = 4
}
#endif
#if !NOSTDLIB_EFI
#endif
