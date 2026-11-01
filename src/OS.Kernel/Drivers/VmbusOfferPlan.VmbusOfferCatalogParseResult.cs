using OS.Std.Binary;

namespace OS.Drivers;
public readonly struct VmbusOfferCatalogParseResult
{
    private VmbusOfferCatalogParseResult(bool isInitialized, VmbusOfferPlanStatus status, int offerCount, int parsedOfferCount, int requiredOfferCapacity, int failingOfferIndex, VmbusOfferPlanStatus failingOfferStatus, uint duplicateChannelId)
    {
        IsInitialized = isInitialized;
        Status = status;
        OfferCount = offerCount;
        ParsedOfferCount = parsedOfferCount;
        RequiredOfferCapacity = requiredOfferCapacity;
        FailingOfferIndex = failingOfferIndex;
        FailingOfferStatus = failingOfferStatus;
        DuplicateChannelId = duplicateChannelId;
    }

    public const int NoFailingOfferIndex = -1;
    public VmbusOfferPlanStatus Status { get; }
    public int OfferCount { get; }
    public int ParsedOfferCount { get; }
    public int RequiredOfferCapacity { get; }
    public int FailingOfferIndex { get; }
    public VmbusOfferPlanStatus FailingOfferStatus { get; }
    public uint DuplicateChannelId { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VmbusOfferPlanStatus.Success;

    public static VmbusOfferCatalogParseResult Success(int offerCount, int parsedOfferCount)
    {
        return new VmbusOfferCatalogParseResult(isInitialized: true, VmbusOfferPlanStatus.Success, offerCount, parsedOfferCount, parsedOfferCount, NoFailingOfferIndex, VmbusOfferPlanStatus.Success, duplicateChannelId: 0);
    }

    public static VmbusOfferCatalogParseResult Fail(VmbusOfferPlanStatus status, int offerCount, int parsedOfferCount, int requiredOfferCapacity, int failingOfferIndex, VmbusOfferPlanStatus failingOfferStatus, uint duplicateChannelId)
    {
        return new VmbusOfferCatalogParseResult(isInitialized: true, status, offerCount, parsedOfferCount, requiredOfferCapacity, failingOfferIndex, failingOfferStatus, duplicateChannelId);
    }
}
