using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct VirtioPciCapabilityListParseResult
{
    private VirtioPciCapabilityListParseResult(VirtioPciCapabilityListParseStatus status, VirtioPciCapabilitySet capabilities, byte failingOffset, byte failingCapabilityId, byte parsedVirtioCapabilityCount, bool isInitialized)
    {
        Status = status;
        Capabilities = capabilities;
        FailingOffset = failingOffset;
        FailingCapabilityId = failingCapabilityId;
        ParsedVirtioCapabilityCount = parsedVirtioCapabilityCount;
        IsInitialized = isInitialized;
    }

    public VirtioPciCapabilityListParseStatus Status
    {
        get;
    }
    public VirtioPciCapabilitySet Capabilities
    {
        get;
    }
    public byte FailingOffset
    {
        get;
    }
    public byte FailingCapabilityId
    {
        get;
    }
    public byte ParsedVirtioCapabilityCount
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioPciCapabilityListParseStatus.Success;

    public bool TryCopyRequiredCapabilities(Span<VirtioPciCapabilityMetadata> destination)
    {
        if (!Succeeded || destination.Length < VirtioPciCapabilityListParser.RequiredCapabilityCount)
        {
            return false;
        }

        destination[0] = Capabilities.CommonConfiguration;
        destination[1] = Capabilities.NotifyConfiguration;
        destination[2] = Capabilities.IsrStatus;
        destination[3] = Capabilities.DeviceConfiguration;
        return true;
    }

    public static VirtioPciCapabilityListParseResult Success(VirtioPciCapabilitySet capabilities, byte parsedVirtioCapabilityCount)
    {
        return new VirtioPciCapabilityListParseResult(VirtioPciCapabilityListParseStatus.Success, capabilities, failingOffset: 0, failingCapabilityId: 0, parsedVirtioCapabilityCount, isInitialized: true);
    }

    public static VirtioPciCapabilityListParseResult Fail(VirtioPciCapabilityListParseStatus status, byte failingOffset, byte failingCapabilityId, byte parsedVirtioCapabilityCount)
    {
        return new VirtioPciCapabilityListParseResult(status, default, failingOffset, failingCapabilityId, parsedVirtioCapabilityCount, isInitialized: true);
    }
}
