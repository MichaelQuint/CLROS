namespace OS.Drivers;
public enum VirtioNetTransmitDescriptorFreeListStatus
{
    Success = 0,
    QueuePlanFailed = 1,
    StateTableTooSmall = 2,
    InvalidDescriptorState = 3,
    NoFreeDescriptorPair = 4
}
