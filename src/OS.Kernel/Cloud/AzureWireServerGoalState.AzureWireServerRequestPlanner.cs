using System.Text;

namespace OS.Cloud;

public static class AzureWireServerRequestPlanner
{
    public const string PlatformHost = "168.63.129.16";
    public const ushort PlatformPort = 80;
    public const string GoalStatePath = "/machine?comp=goalstate";
    public const string HealthPath = "/machine?comp=health";
    public const string HealthContentType = "text/xml; charset=utf-8";
    public static AzureWireServerHttpRequestPlan CreateGoalStateRequest()
    {
        return new AzureWireServerHttpRequestPlan("GET", PlatformHost, PlatformPort, GoalStatePath, string.Empty, string.Empty);
    }

    public static AzureWireServerRequestPlanResult CreateReadyHealthRequest(AzureWireServerGoalState goalState)
    {
        AzureWireServerHealthBodyResult body = AzureWireServerHealthFormatter.FormatReady(goalState);
        if (!body.Succeeded)
        {
            return AzureWireServerRequestPlanResult.Fail(AzureWireServerRequestPlanStatus.InvalidGoalState);
        }

        return AzureWireServerRequestPlanResult.Success(new AzureWireServerHttpRequestPlan("POST", PlatformHost, PlatformPort, HealthPath, HealthContentType, body.Body));
    }
}
