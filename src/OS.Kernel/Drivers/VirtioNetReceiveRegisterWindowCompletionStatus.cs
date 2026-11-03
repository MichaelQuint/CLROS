namespace OS.Drivers;

using OS.Net;

public enum VirtioNetReceiveRegisterWindowCompletionStatus
{
    Success = 0,
    ReadinessFailed = 1,
    CompletionFailed = 2,
    ClassificationFailed = 3
}
