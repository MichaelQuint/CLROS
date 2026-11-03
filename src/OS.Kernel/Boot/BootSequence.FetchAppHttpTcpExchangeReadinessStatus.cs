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
public enum FetchAppHttpTcpExchangeReadinessStatus
{
    Ready = 0,
    NotInitialized = 1,
    ExchangeFailed = 2,
    DiagnosticFormattingFailure = 3
}
#endif
