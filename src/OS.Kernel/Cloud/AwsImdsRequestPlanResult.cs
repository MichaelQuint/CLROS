namespace OS.Cloud;

public readonly struct AwsImdsRequestPlanResult
{
    private AwsImdsRequestPlanResult(AwsImdsRequestPlanStatus status, AwsImdsV2HttpRequestPlan request)
    {
        Status = status;
        Request = request;
    }

    public AwsImdsRequestPlanStatus Status
    {
        get;
    }
    public AwsImdsV2HttpRequestPlan Request
    {
        get;
    }
    public bool Succeeded => Status == AwsImdsRequestPlanStatus.Success;

    public static AwsImdsRequestPlanResult Success(AwsImdsV2HttpRequestPlan request)
    {
        return new AwsImdsRequestPlanResult(AwsImdsRequestPlanStatus.Success, request);
    }

    public static AwsImdsRequestPlanResult Fail(AwsImdsRequestPlanStatus status)
    {
        return new AwsImdsRequestPlanResult(status, default);
    }
}
