using System.Text;

namespace OS.Cloud;

public readonly struct AzureWireServerRequestPlanResult
{
    private AzureWireServerRequestPlanResult(AzureWireServerRequestPlanStatus status, AzureWireServerHttpRequestPlan request)
    {
        Status = status;
        Request = request;
    }

    public AzureWireServerRequestPlanStatus Status
    {
        get;
    }
    public AzureWireServerHttpRequestPlan Request
    {
        get;
    }
    public bool Succeeded => Status == AzureWireServerRequestPlanStatus.Success;

    public static AzureWireServerRequestPlanResult Success(AzureWireServerHttpRequestPlan request)
    {
        return new AzureWireServerRequestPlanResult(AzureWireServerRequestPlanStatus.Success, request);
    }

    public static AzureWireServerRequestPlanResult Fail(AzureWireServerRequestPlanStatus status)
    {
        return new AzureWireServerRequestPlanResult(status, default);
    }
}
