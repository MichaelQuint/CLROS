using OS.Std.Binary;

namespace OS.Manifest;

public readonly struct NativeAppPayloadHeaderResult
{
    private NativeAppPayloadHeaderResult(NativeAppPayloadHeaderStatus status, NativeAppPayloadHeader header, bool isInitialized)
    {
        Status = status;
        Header = header;
        IsInitialized = isInitialized;
    }

    public NativeAppPayloadHeaderStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public NativeAppPayloadHeader Header
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NativeAppPayloadHeaderStatus.Success;

    public static NativeAppPayloadHeaderResult Success(NativeAppPayloadHeader header)
    {
        return new NativeAppPayloadHeaderResult(NativeAppPayloadHeaderStatus.Success, header, isInitialized: true);
    }

    public static NativeAppPayloadHeaderResult Fail(NativeAppPayloadHeaderStatus status)
    {
        return new NativeAppPayloadHeaderResult(status, default, isInitialized: true);
    }
}
