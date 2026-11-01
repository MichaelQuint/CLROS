using OS.Boot;
using OS.Manifest;
using OS.Pal;

namespace OS.Kernel.Runtime;
public enum NativeAppLifecyclePlanStatus
{
    Success = 0,
    InvocationPlanFailed = 1,
    InvalidAppIdentity = 2,
    LogSinkNotReady = 3
}
