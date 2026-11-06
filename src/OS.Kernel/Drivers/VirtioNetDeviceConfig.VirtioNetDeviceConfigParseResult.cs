namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public readonly struct VirtioNetDeviceConfigParseResult
{
    private VirtioNetDeviceConfigParseResult(VirtioNetDeviceConfigStatus status, VirtioNetDeviceConfig config, int inputLength, int bytesConsumed, bool isInitialized)
    {
        Status = status;
        Config = config;
        InputLength = inputLength;
        BytesConsumed = bytesConsumed;
        IsInitialized = isInitialized;
    }

    public VirtioNetDeviceConfigStatus Status
    {
        get;
    }
    public VirtioNetDeviceConfig Config
    {
        get;
    }
    public int InputLength
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
    public bool Succeeded => IsInitialized && Status == VirtioNetDeviceConfigStatus.Success;

    public static VirtioNetDeviceConfigParseResult Success(VirtioNetDeviceConfig config, int inputLength, int bytesConsumed)
    {
        return new VirtioNetDeviceConfigParseResult(VirtioNetDeviceConfigStatus.Success, config, inputLength, bytesConsumed, isInitialized: true);
    }

    public static VirtioNetDeviceConfigParseResult Fail(VirtioNetDeviceConfigStatus status, int inputLength)
    {
        return new VirtioNetDeviceConfigParseResult(status, default, inputLength, 0, isInitialized: true);
    }
}
