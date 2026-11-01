using System.Text;

namespace OS.Cloud;
public readonly struct AzureWireServerGoalStateParseResult
{
    private AzureWireServerGoalStateParseResult(AzureWireServerGoalStateStatus status, AzureWireServerGoalState goalState)
    {
        Status = status;
        GoalState = goalState;
    }

    public AzureWireServerGoalStateStatus Status { get; }
    public AzureWireServerGoalState GoalState { get; }
    public bool Succeeded => Status == AzureWireServerGoalStateStatus.Success;

    public static AzureWireServerGoalStateParseResult Success(AzureWireServerGoalState goalState)
    {
        return new AzureWireServerGoalStateParseResult(AzureWireServerGoalStateStatus.Success, goalState);
    }

    public static AzureWireServerGoalStateParseResult Fail(AzureWireServerGoalStateStatus status)
    {
        return new AzureWireServerGoalStateParseResult(status, default);
    }
}
