namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetReceivedFrameParseResult
{
    private VirtioNetReceivedFrameParseResult(VirtioNetHeaderStatus status, VirtioNetHeader header, VirtioNetReceivedFrameMetadata metadata, bool isInitialized)
    {
        Status = status;
        Header = header;
        Metadata = metadata;
        IsInitialized = isInitialized;
    }

    public VirtioNetHeaderStatus Status { get; }
    public VirtioNetHeader Header { get; }
    public VirtioNetReceivedFrameMetadata Metadata { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetHeaderStatus.Success;

    public static VirtioNetReceivedFrameParseResult Success(VirtioNetHeader header, VirtioNetReceivedFrameMetadata metadata)
    {
        return new VirtioNetReceivedFrameParseResult(VirtioNetHeaderStatus.Success, header, metadata, isInitialized: true);
    }

    public static VirtioNetReceivedFrameParseResult Fail(VirtioNetHeaderStatus status, VirtioNetHeader header)
    {
        return new VirtioNetReceivedFrameParseResult(status, header, default, isInitialized: true);
    }
}
