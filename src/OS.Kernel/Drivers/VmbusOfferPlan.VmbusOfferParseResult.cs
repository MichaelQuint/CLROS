using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct VmbusOfferParseResult
{
    private VmbusOfferParseResult(bool isInitialized, VmbusOfferPlanStatus status, VmbusOfferDescriptor descriptor, int expectedLength, int actualLength)
    {
        IsInitialized = isInitialized;
        Status = status;
        Descriptor = descriptor;
        ExpectedLength = expectedLength;
        ActualLength = actualLength;
    }

    public VmbusOfferPlanStatus Status
    {
        get;
    }
    public VmbusOfferDescriptor Descriptor
    {
        get;
    }
    public int ExpectedLength
    {
        get;
    }
    public int ActualLength
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VmbusOfferPlanStatus.Success;

    public static VmbusOfferParseResult Success(VmbusOfferDescriptor descriptor)
    {
        return new VmbusOfferParseResult(isInitialized: true, VmbusOfferPlanStatus.Success, descriptor, VmbusOfferPlan.OfferMessageLength, VmbusOfferPlan.OfferMessageLength);
    }

    public static VmbusOfferParseResult Fail(VmbusOfferPlanStatus status, int actualLength)
    {
        return new VmbusOfferParseResult(isInitialized: true, status, default, VmbusOfferPlan.OfferMessageLength, actualLength);
    }
}
