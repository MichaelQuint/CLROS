using System.Text;

namespace OS.Cloud;

public enum AzureWireServerGoalStateStatus
{
    Success = 0,
    EmptyInput = 1,
    InputTooLarge = 2,
    MissingContainerId = 3,
    MissingInstanceId = 4,
    MissingIncarnation = 5
}
