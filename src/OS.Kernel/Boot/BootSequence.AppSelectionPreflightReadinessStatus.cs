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
public enum AppSelectionPreflightReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    IncompleteReadSession = 2,
    InvalidBlockDevice = 3,
    ScratchPlanFailed = 4,
    SelectionLoadFailed = 5
}
#endif
#if !NOSTDLIB_EFI
#endif
