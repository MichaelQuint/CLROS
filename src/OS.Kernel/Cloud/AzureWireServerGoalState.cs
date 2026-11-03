using System.Text;

namespace OS.Cloud;

public readonly struct AzureWireServerGoalState
{
    public AzureWireServerGoalState(string containerId, string instanceId, string incarnation)
    {
        ContainerId = containerId;
        InstanceId = instanceId;
        Incarnation = incarnation;
    }

    public string ContainerId
    {
        get;
    }
    public string InstanceId
    {
        get;
    }
    public string Incarnation
    {
        get;
    }
    public bool IsValid => !string.IsNullOrWhiteSpace(ContainerId) && !string.IsNullOrWhiteSpace(InstanceId) && !string.IsNullOrWhiteSpace(Incarnation);
}
