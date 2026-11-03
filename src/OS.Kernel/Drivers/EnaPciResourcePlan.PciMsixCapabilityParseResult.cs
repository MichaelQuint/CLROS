using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct PciMsixCapabilityParseResult
{
    private PciMsixCapabilityParseResult(bool isInitialized, PciMsixCapabilityParseStatus status, byte capabilityOffset, PciMsixCapabilitySnapshot capability)
    {
        IsInitialized = isInitialized;
        Status = status;
        CapabilityOffset = capabilityOffset;
        Capability = capability;
    }

    public PciMsixCapabilityParseStatus Status
    {
        get;
    }
    public byte CapabilityOffset
    {
        get;
    }
    public PciMsixCapabilitySnapshot Capability
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PciMsixCapabilityParseStatus.Success;

    public static PciMsixCapabilityParseResult Success(byte capabilityOffset, PciMsixCapabilitySnapshot capability)
    {
        return new PciMsixCapabilityParseResult(isInitialized: true, PciMsixCapabilityParseStatus.Success, capabilityOffset, capability);
    }

    public static PciMsixCapabilityParseResult Fail(PciMsixCapabilityParseStatus status)
    {
        return new PciMsixCapabilityParseResult(isInitialized: true, status, capabilityOffset: 0, PciMsixCapabilitySnapshot.Missing);
    }
}
