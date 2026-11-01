namespace OS.Drivers;
using OS.Net;

public enum VirtioNetReceiveRegisterWindowDispatchStatus
{
    Success = 0,
    SlabCompletionFailed = 1,
    SelectedBufferRangeInvalid = 2,
    DispatchFailed = 3
}
