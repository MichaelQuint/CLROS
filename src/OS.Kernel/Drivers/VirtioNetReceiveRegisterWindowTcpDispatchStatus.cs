namespace OS.Drivers;

using OS.Net;

public enum VirtioNetReceiveRegisterWindowTcpDispatchStatus
{
    Success = 0,
    ReceiveDispatchFailed = 1,
    SelectedBufferRangeInvalid = 2,
    UnsupportedProtocol = 3,
    BadTcpChecksum = 4,
    TcpStreamFailed = 5,
    TcpDispatchFailed = 6
}
