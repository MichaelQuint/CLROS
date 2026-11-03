namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetHeaderParseResult
{
    private VirtioNetHeaderParseResult(VirtioNetHeaderStatus status, VirtioNetHeader header, int bytesConsumed, bool isInitialized)
    {
        Status = status;
        Header = header;
        BytesConsumed = bytesConsumed;
        IsInitialized = isInitialized;
    }

    public VirtioNetHeaderStatus Status
    {
        get;
    }
    public VirtioNetHeader Header
    {
        get;
    }
    public int BytesConsumed
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetHeaderStatus.Success;

    public static VirtioNetHeaderParseResult Success(VirtioNetHeader header, int bytesConsumed)
    {
        return new VirtioNetHeaderParseResult(VirtioNetHeaderStatus.Success, header, bytesConsumed, isInitialized: true);
    }

    public static VirtioNetHeaderParseResult Fail(VirtioNetHeaderStatus status, VirtioNetHeader header, int bytesConsumed)
    {
        return new VirtioNetHeaderParseResult(status, header, bytesConsumed, isInitialized: true);
    }
}
