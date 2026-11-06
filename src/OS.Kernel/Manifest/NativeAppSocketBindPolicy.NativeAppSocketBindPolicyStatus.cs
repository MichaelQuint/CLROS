using OS.Pal;

namespace OS.Manifest;

public enum NativeAppSocketBindPolicyStatus
{
    Success = 0,
    LaunchPlanFailed = 1,
    MissingListenEndpoint = 2,
    InvalidListenHost = 3,
    InvalidBindEndpoint = 4,
    PortMismatch = 5,
    AddressMismatch = 6
}
